using ElectroMarketing.Application.Campaigns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Campaigns;

public static class CampaignSetup
{
    /// <summary>Senders and campaign options. Api only: DbUtils never sends and has no SMTP settings.</summary>
    public static IServiceCollection AddCampaignSending(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection(SmtpOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TrackingOptions>().Bind(configuration.GetSection(TrackingOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<CampaignOptions>().Bind(configuration.GetSection(CampaignOptions.Section)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TwilioOptions>().Bind(configuration.GetSection(TwilioOptions.Section));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TrackingOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<CampaignOptions>>().Value);
        services.AddSingleton<CampaignContent>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ILetterRenderer, QuestLetterRenderer>();
        if (string.IsNullOrEmpty(configuration[$"{TwilioOptions.Section}:{nameof(TwilioOptions.AccountSid)}"]))
        {
            if (isDevelopment)
            {
                services.AddSingleton<ISmsSender, LoggingSmsSender>();
            }
            else
            {
                services.AddSingleton<ISmsSender, UnconfiguredSmsSender>();
            }
        }
        else
        {
            // ponytail: the Twilio POST is not idempotent, so the default handler is swapped for one that keeps the timeouts but never retries POST.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental; the alternative is two stacked handlers
            services.AddHttpClient<ISmsSender, TwilioSmsSender>()
                .RemoveAllResilienceHandlers()
                .AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());
#pragma warning restore EXTEXP0001
        }

        return services;
    }
}
