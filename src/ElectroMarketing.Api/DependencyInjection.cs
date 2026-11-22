using ElectroMarketing.Api.GraphQl;
using ElectroMarketing.Api.Messaging;
using ElectroMarketing.Infrastructure.Storage;

namespace ElectroMarketing.Api;

public static class DependencyInjection
{
    /// <summary>GraphQL schema, message consumers and startup work of the Api host.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        _ = configuration;
        services.AddHostedService<BucketInitializer>();
        services.AddHostedService<ProductAuditConsumer>();
        services.AddElectroMarketingGraphQl(isDevelopment);
        return services;
    }
}
