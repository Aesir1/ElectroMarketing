using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Application.Documents;
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Application.Insights;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Application.Tracking;
using ElectroMarketing.Domain.Contacts;
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
        services.AddScoped<DocumentService>();
        services.AddScoped<ImportService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ListService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<CampaignSender>();
        services.AddScoped<TrackingService>();
        services.AddScoped<InsightsService>();
        services.AddScoped<IMessageHandler<ImportBatchChanged>, ImportRequestedHandler>();
        return services;
    }
}
