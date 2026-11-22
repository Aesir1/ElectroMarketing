# ElectroMarketing

A marketing app, built in four milestones:

1. CSV contact import into categories, with per-file provenance and upsert.
2. Contact lists (from an import lot, or hand-picked through a full-text search).
3. Campaigns over E-Mail / SMS / Post (PDF + QR) with short random tracking links.
4. Insights per campaign (rates, timing, response latency).

Scaffolded from the DataHub platform template: Documents (uploads/downloads streamed through the Api; MinIO is
never public), Keycloak auth, outbox + RabbitMQ and a queue admin page.

```
browser ──▶ web (Next.js, Auth.js) ──/api/graphql (BFF, bearer)──▶ Api (HotChocolate) ──▶ PostgreSQL
```

## Quickstart

1. Install Docker, the .NET 10 SDK, [Bun](https://bun.sh) and Node.js 22. `bun` must be on the PATH your IDE sees
   (for a desktop-launched Rider that is `~/.profile`, e.g. link it into `~/.local/bin`).
2. `dotnet tool restore && (cd web && bun install)` and trust the HTTPS dev certificate once:
   `dotnet dev-certs https --trust` (on Linux this covers .NET and the Chrome/Firefox NSS stores; curl needs
   `--cacert ~/.aspnet/dev-certs/trust/*.pem`).
3. `dotnet run --project aspire/ElectroMarketing.AppHost` (launch profile `https`, dashboard https://localhost:17180) and
   open the dashboard link it prints. The `http` profile still exists for the dashboard only.
4. In the dashboard start **dbutils-seed** (resets and seeds App, Auth and the Keycloak dev users).
5. Open https://localhost:3000 and sign in as `user@local.test` / `34b11a79a75c2d2324e7e3e72c6fb36a` (or `admin@local.test` / `d605432b54bd51c214494ac5fc1fd9f8`
   for the queue admin page). These are dev-only values; passwords and secrets are AppHost parameters in
   `aspire/ElectroMarketing.AppHost/appsettings.json`; override them with user secrets or a git-ignored
   `appsettings.User.json`.

## Resources (Aspire AppHost)

| Resource | Port | Notes |
| --- | --- | --- |
| postgres (+ `app`, `auth`, `keycloak`) | 5432, pgAdmin 5050 | persistent volume |
| keycloak | 8080 | realm `electromarketing` imported from `aspire/ElectroMarketing.AppHost/keycloak/realms`, theme `electromarketing` |
| minio | 9000 / 9001 | image `cgr.dev/chainguard/minio` (MinIO no longer publishes to Docker Hub) |
| rabbitmq | 5672 / 15672 | management plugin |
| dbutils-migrate | — | `migrate --context All`, runs before the Api |
| dbutils-seed | — | `reseed`, explicit start (destructive) |
| api | https 5101, http 5100 | GraphQL at `/graphql`, `/health`, `/alive` |
| web | https 3000 | dashboard command **Regenerate GraphQL types** |

HTTPS uses the ASP.NET Core dev certificate. The AppHost exports it on start to the git-ignored
`aspire/ElectroMarketing.AppHost/.certs/` (PEM for Next.js). Keycloak, MinIO and
RabbitMQ stay on plain HTTP on localhost.

## Observability

The AppHost also starts the Grafana stack from DieWikinger (`aspire/ElectroMarketing.AppHost/Observability.cs`,
configuration in `docker/` at the repository root, shared with `compose.observability.yaml`):

| Resource | Role |
| --- | --- |
| grafana | http://localhost:3001 (`admin` / parameter `grafana-admin-password`); dashboards and alert rules in folder *ElectroMarketing* |
| alloy | OTLP collector on localhost:4317 (gRPC) / 4318 (HTTP); tails container logs; probes api, web, Keycloak, MinIO |
| prometheus · loki · tempo | metrics · logs · traces (retention 30 d · 7 d · 2 d) |
| cadvisor · postgres-exporter | per-container resources · PostgreSQL (`pg_stat_statements` is preloaded) |

The .NET services and the web front end export OTLP twice: to the Aspire dashboard as before, and to Alloy
(`OTEL_COLLECTOR_ENDPOINT`). Prometheus also scrapes MinIO, RabbitMQ and Keycloak. Every container carries
`com.docker.compose.project=electromarketing` / `com.docker.compose.service=<resource>` labels, which is what
cAdvisor, Alloy, the dashboards and the alert rules select on. Aspire only recreates a persistent container
when its image or environment changes; bump `Observability.StackRevision` after changing container arguments.

## Production (Docker Compose)

`compose.prod.yaml` (+ the included `compose.observability.yaml`) runs everything behind Caddy, which
obtains the certificates. Configuration: `.env` (copy `.env.example`), `docker/` (Caddy, init scripts,
observability) and the Keycloak realm/theme under `aspire/ElectroMarketing.AppHost/keycloak/`.

| Public host | Service | Notes |
| --- | --- | --- |
| `APP_DOMAIN` | web | the Api is not public; web's BFF routes (`/api/graphql`, `/api/documents/*`, `/api/branding/*`) call it |
| `AUTH_DOMAIN` | keycloak | `/admin` answers 404; the admin console is on the tunnel |
| `TRACKING_DOMAIN` | api | only `/o/*`, `/u/*`, `/landing/*` and the 7-character short-link path; everything else answers 404. Also the base of the campaign tracking links (`Tracking__BaseUrl`) |

MinIO has no public host and is not on Caddy's network. All buckets are private: document bytes go
browser → web → Api → MinIO (the Api checks ownership, type and the 25 MB limit) and branding assets are
served by the Api at `/api/branding/<key>`.

Networks: `ingress` (Caddy only; 80/443 and the only route to the Internet), `proxy` and `backend`
(internal: no gateway, no NAT), `observability` (internal), and `tunnel` (Grafana and the Keycloak admin
console, published on 127.0.0.1 only, IP masquerading off). On a server:
`ssh -L 3300:localhost:3300 -L 8081:localhost:8081 <server>`, then Grafana at http://localhost:3300 and
the Keycloak admin console at http://localhost:8081/admin/.

### Rate limiting (Caddy)

Caddy is a custom build (`docker/caddy/Dockerfile`, `xcaddy` + [mholt/caddy-ratelimit](https://github.com/mholt/caddy-ratelimit))
because stock Caddy has no rate limiting. Zones in `docker/caddy/Caddyfile`, all keyed on `{client_ip}`:

| Zone | Host | Paths | Limit | Body limit |
| --- | --- | --- | --- | --- |
| `app` | `APP_DOMAIN` | all | 1000 req / 10s | 26MB (documents ≤ 25MB) |
| `auth_login` | `AUTH_DOMAIN` | `/realms/electromarketing/protocol/openid-connect/token`, `/realms/electromarketing/login-actions/*` | 30 req / 1m | 1MB |
| `tracking` | `TRACKING_DOMAIN` | `/o/*`, `/u/*`, `/landing/*`, `/<7 chars>` | 120 req / 1m | 4KB |

Existing deployments: `TRACKING_BASE_URL` is gone; set `TRACKING_DOMAIN` in `.env` instead and add a DNS record for it pointing at the server.

How it behaves: a **sliding window per IP, not a ban**. Requests over the limit get `429 Too Many
Requests` with a `Retry-After` header; as soon as enough of the window has slid past, the same IP is
served again — nothing is remembered beyond the window, there is no block list to expire or reset.
Counters live in Caddy's memory, so a container restart clears them.

Why these numbers: Keycloak saturates first under load (~600 VU knee, load tests 2026-10-15) and a
real user needs a handful of token requests per minute — 30/1m kills password sprays and login floods
at the edge. Caveat (noted in the Caddyfile): per-IP keys treat a NAT'd
office as one client — per-user keys would need auth at the edge.

Least privilege: the api connects as a DML-only role (`docker/postgres-init`), migrations run as the owner
in the one-shot `dbutils-migrate`, and the api gets its own MinIO account scoped to its
buckets (`docker/minio-init`). `dbutils-seed` upserts master data and the realm's login theme and default
role; it creates no users.

Local test on this machine (`*.localhost` resolves to 127.0.0.1, `CADDY_TLS=internal`):

```bash
cp .env.example .env                                  # change the secrets for anything but a local test
docker compose -f compose.prod.yaml build
docker compose -f compose.prod.yaml up -d
# Trust Caddy's local CA in Chrome/Chromium (NSS) once:
docker compose -f compose.prod.yaml cp caddy:/data/caddy/pki/authorities/local/root.crt ./caddy-root.crt
certutil -d sql:$HOME/.pki/nssdb -A -t "C,," -n "Caddy Local Authority (ElectroMarketing)" -i ./caddy-root.crt
```

Then open https://app.electromarketing.localhost, or create users in the admin console
(http://localhost:8081/admin/). Caddy needs ports 80 (redirect to HTTPS, ACME) and 443; if another
web server holds 80 locally, stop it or move `HTTP_PORT`.

## Solution layout

```
src/     Domain · Application · Infrastructure · Auth · DbUtils · Api · ServiceDefaults
aspire/  ElectroMarketing.AppHost
tests/   <Project>.Tests for every production project + ElectroMarketing.IntegrationTests (Aspire.Hosting.Testing)
web/     Next.js (App Router, Bun, Tailwind v4, Auth.js, GraphQL Code Generator)
```

References point inward only; `tests/ElectroMarketing.Domain.Tests/ArchitectureTests.cs` fails the build otherwise.
Package versions live in `Directory.Packages.props`; analyzers and test packages in `Directory.Build.props`.

## Database utility

```bash
dotnet run --project src/ElectroMarketing.DbUtils -- migrate --context All [--check]
dotnet run --project src/ElectroMarketing.DbUtils -- reset --context App [--force]
dotnet run --project src/ElectroMarketing.DbUtils -- seed --fixtures --master-data --keycloak-users
dotnet run --project src/ElectroMarketing.DbUtils -- maintain --vacuum-analyze --purge-soft-deleted --older-than 90d --orphan-objects [--delete]
dotnet run --project src/ElectroMarketing.DbUtils -- reseed
```

Schema changes are code-first only:

```bash
dotnet ef migrations add <Name> --context AppDbContext --project src/ElectroMarketing.Infrastructure --startup-project src/ElectroMarketing.DbUtils --output-dir Persistence/Migrations
dotnet ef migrations add <Name> --context AuthDbContext --project src/ElectroMarketing.Auth --startup-project src/ElectroMarketing.DbUtils --output-dir Persistence/Migrations
```

Raw SQL (views, functions) goes in `src/ElectroMarketing.Infrastructure/Persistence/Sql/*.sql` (embedded) and is
applied from a migration with `migrationBuilder.Sql(SqlScripts.Read("<file>.sql"))`.

## How to add a feature

Example: an `Invoice` entity with GraphQL CRUD and a page.

1. **Entity** – `src/ElectroMarketing.Domain/Invoices/Invoice.cs`, deriving from `AuditableEntity` (UUID v7 id,
   audit columns, `xmin` row version) and optionally `ISoftDelete` / `IPublishesChanges` (domain events via
   the outbox).
2. **Persistence** – add `DbSet<Invoice>` to `AppDbContext`, an `IEntityTypeConfiguration<Invoice>` in
   `Persistence/Configurations`, then `dotnet ef migrations add AddInvoices ...` (above).
3. **Repository** – nothing to do: `IRepository<Invoice, Guid>` is registered open-generic. Only when behaviour
   differs, add `IInvoiceRepository : IRepository<Invoice, Guid>` in Application and
   `InvoiceRepository : Repository<Invoice, Guid>` in Infrastructure overriding the virtual members, and register it in `Infrastructure/DependencyInjection.cs`.
4. **Use case** – `InvoiceService : CrudService<Invoice, Guid, CreateInvoiceDto, UpdateInvoiceDto>` in
   Application with DataAnnotations on the DTOs; register it in `Application/DependencyInjection.cs`.
5. **Permissions** – add `Permissions.Invoices.Read/Write` in `src/ElectroMarketing.Auth/Permissions.cs` (a policy is
   generated per constant) and grant them in `src/ElectroMarketing.DbUtils/MasterData/auth-roles.json`.
6. **GraphQL** – `src/ElectroMarketing.Api/GraphQl/Invoices/InvoiceQueries.cs` (`[ExtendObjectType(typeof(RootQuery))]`,
   `[UsePaging][UseProjection][UseFiltering][UseSorting]` over `IRepository.Query()`) and
   `InvoiceMutations.cs` (`[ExtendObjectType(typeof(RootMutation))]`, `[Authorize(Policy = ...)]`,
   `[Error<ValidationError>]` etc.). They are registered by the source generator; `Program.cs` never changes.
   Reference fields use a `[DataLoader]`.
7. **Contract** – `dotnet run --project src/ElectroMarketing.Api -- schema export --output web/schema.graphql`, then
   `cd web && bun run codegen` (or the dashboard command). Commit both; CI fails on any diff.
8. **Page** – write the operation with `graphql(...)` in the component, call it through `execute()` from
   `web/src/lib/graphql.ts`, add the page under `web/src/app/app/`.
9. **Tests** – unit tests in `tests/ElectroMarketing.Application.Tests`, repository overrides in
   `tests/ElectroMarketing.Infrastructure.Tests` (Testcontainers), GraphQL in `tests/ElectroMarketing.Api.Tests`, UI in
   `web` (Vitest + MSW, Playwright).

## Tests

| Level | Command | Needs Docker |
| --- | --- | --- |
| Unit (backend) | `dotnet test --project tests/ElectroMarketing.Domain.Tests` / `...Application.Tests` | no |
| Unit (frontend) | `cd web && bun run test` (`test:coverage` for v8 coverage) | no |
| Contract | `tests/ElectroMarketing.Api.Tests` `SchemaTests` + CI codegen diff | no |
| Integration | `dotnet test --project tests/ElectroMarketing.<Infrastructure\|Auth\|DbUtils\|Api>.Tests` | yes |
| End to end | `dotnet test --project tests/ElectroMarketing.IntegrationTests` (boots the AppHost, seeds, runs Playwright) | yes |

The end-to-end suite uses the same fixed ports as a running AppHost; stop your dev AppHost first.
Coverage gate: `python3 build/coverage-gate.py 0.8 ElectroMarketing.Domain,ElectroMarketing.Application <cobertura files>`.

## CI

`.github/workflows/ci.yml`: build with warnings as errors → unit tests + coverage gate → contract check
(schema export + codegen diff, `graphql-inspector diff` on PRs, `has-pending-model-changes` per context) →
gitleaks → Testcontainers integration tests → Aspire + Playwright end to end → multi-arch images
(`src/ElectroMarketing.Api/Dockerfile`, `src/ElectroMarketing.DbUtils/Dockerfile`, `web/Dockerfile`).
