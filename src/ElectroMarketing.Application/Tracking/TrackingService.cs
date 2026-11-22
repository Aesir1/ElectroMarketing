// src/ElectroMarketing.Application/Tracking/TrackingService.cs
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Tracking;

public sealed record LandingPage(string Title, string Body);

/// <summary>Anonymous tracking by message token. Only the user agent is stored, never the IP address.</summary>
public sealed class TrackingService(
    IRepository<CampaignMessage, Guid> messages,
    IRepository<TrackingEvent, Guid> events,
    IRepository<Contact, Guid> contacts,
    IRepository<Campaign, Guid> campaigns,
    TimeProvider time)
{
    public async Task<bool> RecordAsync(string token, TrackingEventType type, string? userAgent, CancellationToken ct = default)
    {
        var message = await FindAsync(token, ct);
        if (message is null)
        {
            return false;
        }

        await AddEventAsync(message, type, userAgent, ct);
        return true;
    }

    public async Task<LandingPage?> LandingAsync(string token, string? userAgent, CancellationToken ct = default)
    {
        var message = await FindAsync(token, ct);
        if (message is null || await campaigns.GetByIdAsync(message.CampaignId, ct) is not { } campaign)
        {
            return null;
        }

        await AddEventAsync(message, TrackingEventType.LandingView, userAgent, ct);
        return new LandingPage(campaign.LandingTitle ?? campaign.Name, campaign.LandingBody ?? string.Empty);
    }

    /// <summary>Opts the contact out of the message's channel (SMS → SMS, everything else → e-mail).</summary>
    public async Task<bool> UnsubscribeAsync(string token, string? userAgent, CancellationToken ct = default)
    {
        var message = await FindAsync(token, ct);
        if (message is null || await contacts.GetByIdAsync(message.ContactId, ct) is not { } contact)
        {
            return false;
        }

        if (message.Channel == Channel.Sms)
        {
            contact.SmsOptOut = true;
        }
        else
        {
            contact.EmailOptOut = true;
        }

        await contacts.UpdateAsync(contact, ct);
        await AddEventAsync(message, TrackingEventType.Unsubscribe, userAgent, ct);
        return true;
    }

    /// <summary>Messages that were never delivered (Queued, Skipped) behave like unknown tokens.</summary>
    private async Task<CampaignMessage?> FindAsync(string token, CancellationToken ct) =>
        ShortToken.IsValid(token)
            ? (await messages.ListAsync(m => m.Token == token && m.Status != MessageStatus.Skipped && m.Status != MessageStatus.Queued, ct)).SingleOrDefault()
            : null;

    private Task<TrackingEvent> AddEventAsync(CampaignMessage message, TrackingEventType type, string? userAgent, CancellationToken ct) =>
        events.AddAsync(
            new TrackingEvent { MessageId = message.Id, Type = type, OccurredAtUtc = time.GetUtcNow(), UserAgent = userAgent?.Length > 512 ? userAgent[..512] : userAgent },
            ct);
}
