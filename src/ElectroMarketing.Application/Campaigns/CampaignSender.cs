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
    private const int ChunkSize = 1;

    // ponytail: campaigns run one after another; a long one delays the rest. Run them in parallel if that matters.
    public async Task SendPendingAsync(CancellationToken ct)
    {
        foreach (var campaign in await campaigns.ListAsync(c => c.Status == CampaignStatus.Sending, ct))
        {
            try
            {
                await RunAsync(campaign, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Campaign {CampaignId} failed; continuing with the next", campaign.Id);
            }
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

    // ponytail: status is saved after every message, so a crash re-sends at most the single in-flight message.
    // No SMTP retry: an outage marks the remaining e-mails Failed.
    private async Task SendAsync(Guid campaignId, Channel channel, double perSecond, Func<CampaignMessage, CancellationToken, Task> send, CancellationToken ct)
    {
        var pause = TimeSpan.FromSeconds(1 / perSecond);
        while (await store.NextQueuedAsync(campaignId, channel, ChunkSize, ct) is { Count: > 0 } chunk)
        {
            foreach (var message in chunk)
            {
                // The contact was loaded with this chunk, so an opt-out since the start of the campaign is honoured.
                if (Skip(channel, message))
                {
                    await store.SaveAsync(CancellationToken.None);
                    continue;
                }

                try
                {
                    await send(message, ct);
                    message.Status = MessageStatus.Sent;
                    message.SentAtUtc = time.GetUtcNow();
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    message.Status = MessageStatus.Failed;
                    message.Error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    logger.LogWarning(ex, "{Channel} message {MessageId} failed", channel, message.Id);
                }

                // Save per message; the store detaches tracked messages, hence a chunk of one.
                await store.SaveAsync(CancellationToken.None);
                await Task.Delay(pause, time, ct);
            }
        }
    }

    /// <summary>One merged PDF for all queued letters; "sent" means printed-ready.</summary>
    private async Task RenderLettersAsync(Campaign campaign, CancellationToken ct)
    {
        var queued = (await store.NextQueuedAsync(campaign.Id, Channel.Post, int.MaxValue, ct)).Where(m => !Skip(Channel.Post, m)).ToList();
        if (queued.Count == 0)
        {
            await store.SaveAsync(ct);
            return;
        }

        // ponytail: every letter is rendered in memory at once; render in batches of pages if campaigns get huge.
        try
        {
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
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Letters of campaign {CampaignId} failed", campaign.Id);
            var error = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            foreach (var message in queued)
            {
                message.Status = MessageStatus.Failed;
                message.Error = error;
            }
        }

        // The campaign is loaded untracked: UpdateAsync saves it together with the tracked messages.
        await campaigns.UpdateAsync(campaign, ct);
    }

    /// <summary>Marks the message Skipped when the contact can no longer get it (opted out or data removed since the start).</summary>
    private static bool Skip(Channel channel, CampaignMessage message)
    {
        if (RecipientRules.SkipReason(channel, message.Contact!) is not { } reason)
        {
            return false;
        }

        message.Status = MessageStatus.Skipped;
        message.Error = reason;
        return true;
    }
}
