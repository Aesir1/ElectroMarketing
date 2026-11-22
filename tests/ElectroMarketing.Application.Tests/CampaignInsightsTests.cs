using ElectroMarketing.Application.Insights;
using ElectroMarketing.Domain.Campaigns;

namespace ElectroMarketing.Application.Tests;

public class CampaignInsightsTests
{
    private static readonly DateTimeOffset Sent = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero); // Monday 09:00 Berlin
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

    private readonly MessageFact mail1 = new(Guid.CreateVersion7(), Channel.Email, MessageStatus.Sent, Sent);
    private readonly MessageFact mail2 = new(Guid.CreateVersion7(), Channel.Email, MessageStatus.Sent, Sent);
    private readonly MessageFact mail3 = new(Guid.CreateVersion7(), Channel.Email, MessageStatus.Failed, null);
    private readonly MessageFact sms1 = new(Guid.CreateVersion7(), Channel.Sms, MessageStatus.Sent, Sent);
    private readonly MessageFact post1 = new(Guid.CreateVersion7(), Channel.Post, MessageStatus.Skipped, null);

    private CampaignInsightsResult Compute(params EventFact[] events) =>
        CampaignInsights.Compute(
            new Campaign { Name = "Spring", StartedAtUtc = Sent },
            [mail1, mail2, mail3, sms1, post1],
            events,
            Berlin);

    [Fact]
    public void Channel_stats_count_unique_messages_and_rates_against_sent()
    {
        // mail1 has two opens and two unsubscribes: every event type still counts the message once.
        var result = Compute(
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(5)),
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(9)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddMinutes(10)),
            new(mail1.Id, TrackingEventType.Unsubscribe, Sent.AddMinutes(11)),
            new(mail1.Id, TrackingEventType.Unsubscribe, Sent.AddMinutes(12)),
            new(mail2.Id, TrackingEventType.EmailOpen, Sent.AddHours(2)),
            new(sms1.Id, TrackingEventType.LinkClick, Sent.AddDays(2)),
            new(sms1.Id, TrackingEventType.Unsubscribe, Sent.AddDays(2)));

        var email = result.Channels.Single(c => c.Channel == Channel.Email);
        (email.Sent, email.Failed, email.Opened, email.Clicked, email.Unsubscribed, email.OpenRate, email.ClickRate).ShouldBe((2, 1, 2, 1, 1, 1.0, 0.5));
        var sms = result.Channels.Single(c => c.Channel == Channel.Sms);
        (sms.Clicked, sms.Unsubscribed, sms.ClickRate).ShouldBe((1, 1, 1.0));
        result.Channels.Single(c => c.Channel == Channel.Post).Skipped.ShouldBe(1);
    }

    [Fact]
    public void Response_latency_uses_first_click_or_landing_view()
    {
        var result = Compute(
            new(mail1.Id, TrackingEventType.LandingView, Sent.AddMinutes(30)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddHours(5)),
            new(mail2.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(1)),
            new(sms1.Id, TrackingEventType.LinkClick, Sent.AddDays(2)));

        result.ResponseLatency.Select(b => (b.Label, b.Count)).ShouldBe([("< 1 h", 1), ("1–6 h", 0), ("6–24 h", 0), ("1–3 d", 1), ("> 3 d", 0)]);
        result.MedianResponseHours.ShouldBe(48);
    }

    [Fact]
    public void Timeline_is_hourly_in_local_time_and_heatmap_counts_responses()
    {
        var result = Compute(
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(5)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddMinutes(20)),
            new(mail2.Id, TrackingEventType.LinkClick, Sent.AddHours(1).AddMinutes(1)));

        result.Timeline.Select(p => (p.Start.Hour, p.Opens, p.Clicks)).ShouldBe([(9, 1, 1), (10, 0, 1)]);
        result.Timeline[0].Start.Offset.ShouldBe(TimeSpan.FromHours(1));
        result.ByWeekdayHour.ShouldBe([new HeatCell(1, 9, 1), new HeatCell(1, 10, 1)]);
    }

    [Fact]
    public void Responders_list_each_message_once_with_first_event_times_and_no_contact_data()
    {
        var result = Compute(
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(9)),
            new(mail1.Id, TrackingEventType.EmailOpen, Sent.AddMinutes(5)),
            new(mail1.Id, TrackingEventType.LinkClick, Sent.AddMinutes(10)),
            new(sms1.Id, TrackingEventType.Unsubscribe, Sent.AddDays(2)));

        result.Responders.Select(r => r.MessageId).ShouldBe([mail1.Id, sms1.Id], ignoreOrder: true);
        var first = result.Responders.Single(r => r.MessageId == mail1.Id);
        (first.Channel, first.OpenedAtUtc, first.ClickedAtUtc, first.UnsubscribedAtUtc)
            .ShouldBe((Channel.Email, Sent.AddMinutes(5), Sent.AddMinutes(10), null));
    }

    [Fact]
    public void No_events_gives_zero_rates_and_no_median()
    {
        var result = Compute();
        result.MedianResponseHours.ShouldBeNull();
        result.Responders.ShouldBeEmpty();
    }
}
