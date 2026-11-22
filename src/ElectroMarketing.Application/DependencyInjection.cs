using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Documents;
using ElectroMarketing.Application.Products;
using ElectroMarketing.Domain.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElectroMarketing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        _ = configuration;
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ProductService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<IMessageHandler<ProductChanged>, ProductAuditHandler>();
        return services;
    }
}
