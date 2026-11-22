using ElectroMarketing.Application.Campaigns;

namespace ElectroMarketing.Api;

/// <summary>Polls for Sending campaigns every 5 s. Not a RabbitMQ consumer: sending can outlast the broker's 30 min ack timeout.</summary>
public sealed class CampaignDispatcher(IServiceScopeFactory scopes, TimeProvider time, ILogger<CampaignDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ponytail: assumes one Api instance; lock campaign rows (FOR UPDATE SKIP LOCKED) before scaling out.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), time);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CampaignSender>().SendPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Campaign dispatch failed; retrying on the next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
