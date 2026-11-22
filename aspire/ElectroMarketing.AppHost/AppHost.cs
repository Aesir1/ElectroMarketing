using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using ElectroMarketing.AppHost;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.User.json", optional: true).AddEnvironmentVariables().AddCommandLine(args);

const string realm = "electromarketing";

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var minioPassword = builder.AddParameter("minio-password", secret: true);
var rabbitPassword = builder.AddParameter("rabbitmq-password", secret: true);
var webClientSecret = builder.AddParameter("web-client-secret", secret: true);
var authSecret = builder.AddParameter("auth-secret", secret: true);
var seedAdminPassword = builder.AddParameter("seed-admin-password", secret: true);
var seedUserPassword = builder.AddParameter("seed-user-password", secret: true);

// HTTPS for the web front end uses the ASP.NET Core dev certificate (trust it once: dotnet dev-certs https --trust).
var devCert = DevCertificate.Export(
    Path.Combine(builder.AppHostDirectory, ".certs"),
    builder.Configuration["Parameters:dev-cert-password"] ?? throw new InvalidOperationException("Parameter dev-cert-password is missing."));

// pg_stat_statements must be preloaded at start-up; postgres-exporter reads query rate and slow queries from it.
var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 5432)
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpointProxySupport(false)
    .WithDataVolume("electromarketing-postgres")
    .WithArgs("-c", "shared_preload_libraries=pg_stat_statements", "-c", "pg_stat_statements.max=1000", "-c", "pg_stat_statements.track=top")
    .WithStackLabels("postgres")
    .WithPgAdmin(p => p.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5050).WithStackLabels("pgadmin"));
var appDb = postgres.AddDatabase("app");
var authDb = postgres.AddDatabase("auth");
var keycloakDb = postgres.AddDatabase("keycloak-db", databaseName: "keycloak");

var keycloak = builder.AddKeycloak("keycloak", 8080, adminPassword: keycloakAdminPassword)
    .WithStackLabels("keycloak")
    .WithEnvironment("KC_METRICS_ENABLED", "true")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpointProxySupport(false)
    .WithoutHttpsCertificate()
    .WithRealmImport("./keycloak/realms")
    .WithBindMount("./keycloak/themes", "/opt/keycloak/themes", isReadOnly: true)
    .WithEnvironment("KC_DB", "postgres")
    .WithEnvironment("KC_DB_URL", $"jdbc:postgresql://{postgres.Resource.Name}:5432/{keycloakDb.Resource.DatabaseName}")
    .WithEnvironment("KC_DB_USERNAME", "postgres")
    .WithEnvironment("KC_DB_PASSWORD", postgresPassword)
    .WithEnvironment("WEB_CLIENT_SECRET", webClientSecret)
    .WithEnvironment("WEB_BASE_URL", "https://localhost:3000")
    .WaitFor(keycloakDb);
var keycloakHttp = keycloak.GetEndpoint("http");
var issuer = ReferenceExpression.Create($"{keycloakHttp}/realms/{realm}");

// minio/minio is no longer published on Docker Hub; Chainguard's build is free and multi-arch.
var minio = builder.AddMinioContainer("minio", rootPassword: minioPassword, port: 9000)
    .WithStackLabels("minio")
    .WithEnvironment("MINIO_PROMETHEUS_AUTH_TYPE", "public")
    .WithImageRegistry("cgr.dev")
    .WithImage("chainguard/minio")
    .WithImageTag("latest")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpointProxySupport(false)
    .WithEndpoint("console", e => e.Port = 9001)
    .WithDataVolume("electromarketing-minio");

var rabbitmq = builder.AddRabbitMQ("rabbitmq", password: rabbitPassword, port: 5672)
    .WithStackLabels("rabbitmq")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithEndpointProxySupport(false)
    .WithManagementPlugin(port: 15672)
    .WithDataVolume("electromarketing-rabbitmq");

// Grafana, Prometheus, Loki, Tempo, Alloy, cAdvisor and postgres-exporter (see Observability.cs). Every .NET
// service and the web front end also send OTLP to Alloy via OTEL_COLLECTOR_ENDPOINT.
var observability = builder.AddObservability(postgres, appDb);

var migrate = builder.AddProject<Projects.ElectroMarketing_DbUtils>("dbutils-migrate")
    .WithEnvironment("OTEL_COLLECTOR_ENDPOINT", observability.OtlpHttp)
    .WithArgs("migrate", "--context", "All")
    .WithReference(appDb)
    .WithReference(authDb)
    .WithReference(minio)
    .WaitFor(appDb)
    .WaitFor(authDb);

// Destructive: drops and reseeds App, Auth and the Keycloak dev users. Start it from the dashboard.
builder.AddProject<Projects.ElectroMarketing_DbUtils>("dbutils-seed")
    .WithEnvironment("OTEL_COLLECTOR_ENDPOINT", observability.OtlpHttp)
    .WithArgs("reseed")
    .WithReference(appDb)
    .WithReference(authDb)
    .WithReference(minio)
    .WithReference(keycloak)
    .WithEnvironment("Seed__KeycloakAdminPassword", keycloakAdminPassword)
    .WithEnvironment("Seed__AdminPassword", seedAdminPassword)
    .WithEnvironment("Seed__UserPassword", seedUserPassword)
    .WithEnvironment("Seed__LoginTheme", "electromarketing")
    .WithExplicitStart()
    .WaitFor(appDb)
    .WaitFor(keycloak);

// Dev SMTP: every campaign e-mail lands in the Mailpit inbox (UI on the "ui" endpoint).
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.33.1")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "ui")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp")
    .WithLifetime(ContainerLifetime.Persistent);
var smtp = mailpit.GetEndpoint("smtp");

var api = builder.AddProject<Projects.ElectroMarketing_Api>("api", launchProfileName: "https")
    .WithEnvironment("OTEL_COLLECTOR_ENDPOINT", observability.OtlpHttp)
    .WithReference(appDb)
    .WithReference(authDb)
    .WithReference(keycloak)
    .WithReference(rabbitmq)
    .WithReference(minio)
    .WithEnvironment("Messaging__ManagementUrl", rabbitmq.GetEndpoint("management"))
    .WithEnvironment("Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WaitForCompletion(migrate)
    .WaitFor(keycloak)
    .WaitFor(rabbitmq)
    .WaitFor(minio)
    .WaitFor(mailpit);

api.WithEnvironment("Tracking__BaseUrl", api.GetEndpoint("https"));

var repoRoot = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", ".."));

// Next.js serves HTTPS with the dev certificate. --experimental-https-ca is required too: without it Next drops
// NODE_EXTRA_CA_CERTS when it forks its server process, and the BFF could not call the Api over HTTPS.
var web = builder.AddNextJsApp("web", "../../web")
    .WithEnvironment("OTEL_COLLECTOR_ENDPOINT", observability.OtlpHttp)
    .WithBun()
    .WithEndpoint("http", e =>
    {
        e.UriScheme = "https";
        e.Port = 3000;
        e.IsProxied = false;
    })
    .WithArgs("--experimental-https", "--experimental-https-key", devCert.KeyPath, "--experimental-https-cert", devCert.PemPath, "--experimental-https-ca", devCert.PemPath)
    .WithEnvironment("API_URL", api.GetEndpoint("https"))
    .WithEnvironment("AUTH_URL", "https://localhost:3000")
    .WithEnvironment("AUTH_TRUST_HOST", "true")
    .WithEnvironment("AUTH_SECRET", authSecret)
    .WithEnvironment("AUTH_KEYCLOAK_ID", "web")
    .WithEnvironment("AUTH_KEYCLOAK_SECRET", webClientSecret)
    .WithEnvironment("AUTH_KEYCLOAK_ISSUER", issuer)
    .WithEnvironment("BRANDING_FILE", "config/branding.json")
    .WithExternalHttpEndpoints()
    .WithCommand(
        "regenerate-graphql",
        "Regenerate GraphQL types",
        async context =>
        {
            var export = await RunAsync("dotnet", "run --project src/ElectroMarketing.Api -- schema export --output web/schema.graphql", repoRoot, context.CancellationToken);
            if (export is not null)
            {
                return CommandResults.Failure(export);
            }

            var codegen = await RunAsync("bun", "run codegen", Path.Combine(repoRoot, "web"), context.CancellationToken);
            return codegen is null ? CommandResults.Success() : CommandResults.Failure(codegen);
        },
        new CommandOptions { IconName = "ArrowSync", Description = "Export the Api schema and run bun run codegen" })
    .WaitFor(api)
    .WaitFor(keycloak);

observability
    .Probe("api", api, "http", "/health")
    .Probe("web", web, "http", "/");

await builder.Build().RunAsync();

// Returns null on success, otherwise the tail of the output.
static async Task<string?> RunAsync(string file, string arguments, string workingDirectory, CancellationToken ct)
{
    using var process = Process.Start(new ProcessStartInfo(file, arguments)
    {
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    })!;
    var output = process.StandardOutput.ReadToEndAsync(ct);
    var error = process.StandardError.ReadToEndAsync(ct);
    await process.WaitForExitAsync(ct);
    if (process.ExitCode == 0)
    {
        return null;
    }

    var log = await error + await output;
    return $"{file} exited {process.ExitCode}: {log[^Math.Min(log.Length, 1000)..]}";
}
