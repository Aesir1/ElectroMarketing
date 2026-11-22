using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace ElectroMarketing.Application.Campaigns;

/// <summary>
/// Sends the Queued messages of every Sending campaign. Only Queued rows are picked, so a restart resumes where it
/// stopped. Failures are stored per message and never stop the campaign.
/// </summary>
public sealed class CampaignSender(
    IRepository<Campaign, Guid> campaigns,
    ICampaignStore store,
    CampaignContent content,
    IEmailSender email,
    ISmsSender sms,
    ILetterRenderer letters,
    IObjectStorage storage,
    CampaignOptions options,
    TimeProvider time,
    ILogger<CampaignSender> logger)
{
    private const int ChunkSize = 100;

    public async Task SendPendingAsync(CancellationToken ct)
    {
        foreach (var campaign in await campaigns.ListAsync(c => c.Status == CampaignStatus.Sending, ct))
        {
            await RunAsync(campaign, ct);
        }
    }

    public async Task RunAsync(Campaign campaign, CancellationToken ct)
    {
        await SendAsync(campaign.Id, Channel.Email, options.EmailsPerSecond, (m, c) => email.SendAsync(content.Email(campaign, m.Contact!, m.Token), c), ct);
        await SendAsync(campaign.Id, Channel.Sms, options.SmsPerSecond, (m, c) => sms.SendAsync(m.Contact!.PhoneE164!, content.Sms(campaign, m.Contact, m.Token), c), ct);
        await RenderLettersAsync(campaign, ct);

        campaign.Status = CampaignStatus.Sent;
        campaign.FinishedAtUtc = time.GetUtcNow();
        await campaigns.UpdateAsync(campaign, ct);
    }

    // ponytail: a crash between sending and saving a chunk re-sends up to ChunkSize messages; save per message if that matters.
    private async Task SendAsync(Guid campaignId, Channel channel, double perSecond, Func<CampaignMessage, CancellationToken, Task> send, CancellationToken ct)
    {
        var pause = TimeSpan.FromSeconds(1 / perSecond);
        while (await store.NextQueuedAsync(campaignId, channel, ChunkSize, ct) is { Count: > 0 } chunk)
        {
            foreach (var message in chunk)
            {
                try
                {
                    await send(message, ct);
                    message.Status = MessageStatus.Sent;
                    message.SentAtUtc = time.GetUtcNow();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.Status = MessageStatus.Failed;
                    message.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    logger.LogWarning(ex, "{Channel} message {MessageId} failed", channel, message.Id);
                }

                await Task.Delay(pause, time, ct);
            }

            await store.SaveAsync(ct);
        }
    }

    /// <summary>One merged PDF for all queued letters; "sent" means printed-ready.</summary>
    private async Task RenderLettersAsync(Campaign campaign, CancellationToken ct)
    {
        var queued = await store.NextQueuedAsync(campaign.Id, Channel.Post, int.MaxValue, ct);
        if (queued.Count == 0)
        {
            return;
        }

        var pdf = letters.Render(queued.Select(m => content.Letter(campaign, m.Contact!, m.Token)).ToList());
        using (var stream = new MemoryStream(pdf))
        {
            await storage.PutAsync(Document.Bucket, Campaign.LettersKey(campaign.Id), stream, "application/pdf", ct);
        }

        var now = time.GetUtcNow();
        foreach (var message in queued)
        {
            message.Status = MessageStatus.Sent;
            message.SentAtUtc = now;
        }

        campaign.LettersReady = true;
        await store.SaveAsync(ct);
    }
}
