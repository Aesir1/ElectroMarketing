using ElectroMarketing.Application.Campaigns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Campaigns;

public static class CampaignSetup
{
    /// <summary>Senders and campaign options. Api only: DbUtils never sends and has no SMTP settings.</summary>
    public static IServiceCollection AddCampaignSending(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TrackingOptions>().Bind(configuration.GetSection(TrackingOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CampaignOptions>().Bind(configuration.GetSection(CampaignOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TwilioOptions>().Bind(configuration.GetSection(TwilioOptions.Section));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TrackingOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CampaignOptions>>().Value);
        services.AddSingleton<CampaignContent>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        if (string.IsNullOrEmpty(configuration[$"{TwilioOptions.Section}:{nameof(TwilioOptions.AccountSid)}"]))
        {
            services.AddSingleton<ISmsSender, LoggingSmsSender>();
        }
        else
        {
            services.AddHttpClient<ISmsSender, TwilioSmsSender>();
        }

        return services;
    }
}
