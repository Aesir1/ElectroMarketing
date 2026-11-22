using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Exceptions;

namespace ElectroMarketing.Application.Insights;

public sealed record MessageFact(Guid Id, Channel Channel, MessageStatus Status, DateTimeOffset? SentAtUtc);

public sealed record EventFact(Guid MessageId, TrackingEventType Type, DateTimeOffset OccurredAtUtc);

/// <summary>Counts are unique messages. Rates are per sent message. E-mail opens are approximate (Apple Mail Privacy Protection pre-loads pixels).</summary>
public sealed record ChannelStats(Channel Channel, int Sent, int Failed, int Skipped, int Opened, int Clicked, int LandingViews, int Unsubscribed, double OpenRate, double ClickRate);

public sealed record LatencyBucket(string Label, int Count);

/// <summary><see cref="Start"/> is in the configured time zone: hourly buckets for the first 3 days, daily after.</summary>
public sealed record TimelinePoint(DateTimeOffset Start, int Opens, int Clicks);

/// <summary><see cref="Weekday"/> 0 = Sunday (<see cref="DayOfWeek"/>), local time.</summary>
public sealed record HeatCell(int Weekday, int Hour, int Count);

/// <summary>
/// One message that produced any event, with the first time of each kind. Carries no contact data: the message id
/// is the key, and names or e-mails stay behind <c>campaignMessages.contact</c> (contacts.read).
/// </summary>
public sealed record Responder(Guid MessageId, Channel Channel, DateTimeOffset? OpenedAtUtc, DateTimeOffset? ClickedAtUtc, DateTimeOffset? LandingViewAtUtc, DateTimeOffset? UnsubscribedAtUtc);

public sealed record CampaignInsightsResult(
    Guid CampaignId,
    string CampaignName,
    DateTimeOffset? StartedAtUtc,
    IReadOnlyList<ChannelStats> Channels,
    IReadOnlyList<LatencyBucket> ResponseLatency,
    double? MedianResponseHours,
    IReadOnlyList<TimelinePoint> Timeline,
    IReadOnlyList<HeatCell> ByWeekdayHour,
    IReadOnlyList<Responder> Responders);

public static class CampaignInsights
{
    private static readonly (string Label, TimeSpan UpTo)[] Buckets =
    [
        ("< 1 h", TimeSpan.FromHours(1)),
        ("1–6 h", TimeSpan.FromHours(6)),
        ("6–24 h", TimeSpan.FromHours(24)),
        ("1–3 d", TimeSpan.FromDays(3)),
        ("> 3 d", TimeSpan.MaxValue),
    ];

    /// <summary>A response is a click on the short link (or QR scan) or a landing page view.</summary>
    private static bool IsResponse(TrackingEventType type) => type is TrackingEventType.LinkClick or TrackingEventType.LandingView;

    // ponytail: in-memory over all messages and events of one campaign (fine to ~100k); move to SQL aggregates if campaigns grow.
    public static CampaignInsightsResult Compute(Campaign campaign, IReadOnlyList<MessageFact> messages, IReadOnlyList<EventFact> events, TimeZoneInfo zone)
    {
        var byMessage = events.ToLookup(e => e.MessageId);

        var channels = messages.GroupBy(m => m.Channel).OrderBy(g => g.Key).Select(g =>
        {
            int Count(Func<MessageFact, bool> predicate) => g.Count(predicate);
            int With(Func<TrackingEventType, bool> type) => g.Count(m => byMessage[m.Id].Any(e => type(e.Type)));
            var sent = Count(m => m.Status == MessageStatus.Sent);
            var opened = With(t => t == TrackingEventType.EmailOpen);
            var clicked = With(IsResponse);
            return new ChannelStats(
                g.Key,
                sent,
                Count(m => m.Status == MessageStatus.Failed),
                Count(m => m.Status == MessageStatus.Skipped),
                opened,
                clicked,
                With(t => t == TrackingEventType.LandingView),
                With(t => t == TrackingEventType.Unsubscribe),
                Rate(opened, sent),
                Rate(clicked, sent));
        }).ToList();

        var latencies = messages
            .Where(m => m.SentAtUtc is not null)
            .Select(m => byMessage[m.Id].Where(e => IsResponse(e.Type)).Select(e => (TimeSpan?)(e.OccurredAtUtc - m.SentAtUtc!.Value)).Min())
            .OfType<TimeSpan>()
            .Select(t => t < TimeSpan.Zero ? TimeSpan.Zero : t)
            .Order()
            .ToList();
        var buckets = Buckets.Select((b, i) => new LatencyBucket(b.Label, latencies.Count(l => l < b.UpTo && (i == 0 || l >= Buckets[i - 1].UpTo)))).ToList();
        double? median = latencies.Count == 0 ? null : Math.Round(latencies[latencies.Count / 2].TotalHours, 1);

        var relevant = events.Where(e => e.Type == TrackingEventType.EmailOpen || IsResponse(e.Type)).ToList();
        var hourly = relevant.Count == 0 || campaign.StartedAtUtc is null || relevant.Max(e => e.OccurredAtUtc) - campaign.StartedAtUtc <= TimeSpan.FromDays(3);
        var timeline = relevant
            .GroupBy(e => Truncate(TimeZoneInfo.ConvertTime(e.OccurredAtUtc, zone), hourly))
            .OrderBy(g => g.Key)
            .Select(g => new TimelinePoint(g.Key, g.Count(e => e.Type == TrackingEventType.EmailOpen), g.Count(e => e.Type == TrackingEventType.LinkClick)))
            .ToList();

        var heat = events
            .Where(e => e.Type == TrackingEventType.LinkClick)
            .Select(e => TimeZoneInfo.ConvertTime(e.OccurredAtUtc, zone))
            .GroupBy(t => (Weekday: (int)t.DayOfWeek, t.Hour))
            .OrderBy(g => g.Key.Weekday).ThenBy(g => g.Key.Hour)
            .Select(g => new HeatCell(g.Key.Weekday, g.Key.Hour, g.Count()))
            .ToList();

        DateTimeOffset? First(Guid id, TrackingEventType type) => byMessage[id].Where(e => e.Type == type).Select(e => (DateTimeOffset?)e.OccurredAtUtc).Min();
        var responders = messages
            .Where(m => byMessage[m.Id].Any())
            .Select(m => new Responder(
                m.Id,
                m.Channel,
                First(m.Id, TrackingEventType.EmailOpen),
                First(m.Id, TrackingEventType.LinkClick),
                First(m.Id, TrackingEventType.LandingView),
                First(m.Id, TrackingEventType.Unsubscribe)))
            .OrderBy(r => new[] { r.OpenedAtUtc, r.ClickedAtUtc, r.LandingViewAtUtc, r.UnsubscribedAtUtc }.Min())
            .ThenBy(r => r.MessageId)
            .ToList();

        return new CampaignInsightsResult(campaign.Id, campaign.Name, campaign.StartedAtUtc, channels, buckets, median, timeline, heat, responders);
    }

    private static double Rate(int count, int sent) => sent == 0 ? 0 : Math.Round((double)count / sent, 4);

    private static DateTimeOffset Truncate(DateTimeOffset local, bool hourly) =>
        new(local.Year, local.Month, local.Day, hourly ? local.Hour : 0, 0, 0, local.Offset);
}

public sealed class InsightsService(
    IRepository<Campaign, Guid> campaigns,
    IRepository<CampaignMessage, Guid> messages,
    IRepository<TrackingEvent, Guid> events,
    CampaignOptions options)
{
    public async Task<CampaignInsightsResult> ForCampaignAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await campaigns.GetByIdAsync(campaignId, ct) ?? throw NotFoundException.For<Campaign>(campaignId);
        return await ComputeAsync(campaign, ct);
    }

    /// <summary>The last <paramref name="take"/> (1–20) started campaigns, newest first.</summary>
    public async Task<IReadOnlyList<CampaignInsightsResult>> RecentAsync(int take, CancellationToken ct = default)
    {
        var started = (await campaigns.ListAsync(c => c.Status != CampaignStatus.Draft, ct))
            .OrderByDescending(c => c.StartedAtUtc)
            .Take(Math.Clamp(take, 1, 20));
        var result = new List<CampaignInsightsResult>();
        foreach (var campaign in started)
        {
            result.Add(await ComputeAsync(campaign, ct));
        }

        return result;
    }

    private async Task<CampaignInsightsResult> ComputeAsync(Campaign campaign, CancellationToken ct)
    {
        var m = await messages.ListAsync(x => x.CampaignId == campaign.Id, ct);
        var e = await events.ListAsync(x => x.Message!.CampaignId == campaign.Id, ct);
        return CampaignInsights.Compute(
            campaign,
            m.Select(x => new MessageFact(x.Id, x.Channel, x.Status, x.SentAtUtc)).ToList(),
            e.Select(x => new EventFact(x.MessageId, x.Type, x.OccurredAtUtc)).ToList(),
            TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone));
    }
}
