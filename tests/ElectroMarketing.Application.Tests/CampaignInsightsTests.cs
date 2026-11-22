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
        result.MedianResponseHours.ShouldBe(24.2); // mean of 0.5 h and 48 h
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
    public void Median_of_an_even_count_averages_the_middle_two()
    {
        var messages = Enumerable.Range(1, 4).Select(_ => new MessageFact(Guid.CreateVersion7(), Channel.Sms, MessageStatus.Sent, Sent)).ToList();
        var events = messages.Select((m, i) => new EventFact(m.Id, TrackingEventType.LinkClick, Sent.AddHours(i + 1))).ToList();

        CampaignInsights.Compute(new Campaign { Name = "Even", StartedAtUtc = Sent }, messages, events, Berlin).MedianResponseHours.ShouldBe(2.5);
    }

    [Fact]
    public void Responders_are_capped_to_the_newest_and_counted_in_total()
    {
        var messages = Enumerable.Range(0, CampaignInsights.MaxResponders + 1).Select(_ => new MessageFact(Guid.CreateVersion7(), Channel.Sms, MessageStatus.Sent, Sent)).ToList();
        var events = messages.Select((m, i) => new EventFact(m.Id, TrackingEventType.LinkClick, Sent.AddMinutes(i))).ToList();

        var result = CampaignInsights.Compute(new Campaign { Name = "Big", StartedAtUtc = Sent }, messages, events, Berlin);

        result.ResponderCount.ShouldBe(CampaignInsights.MaxResponders + 1);
        result.Responders.Count.ShouldBe(CampaignInsights.MaxResponders);
        result.Responders[0].MessageId.ShouldBe(messages[^1].Id);
        result.Responders.ShouldNotContain(r => r.MessageId == messages[0].Id);
        CampaignInsights.Compute(new Campaign { Name = "Big", StartedAtUtc = Sent }, messages, events, Berlin, includeResponders: false).Responders.ShouldBeEmpty();
    }

    [Fact]
    public void Daily_buckets_on_a_25_hour_dst_day_are_one_bucket_at_local_midnight()
    {
        var started = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

        // 2026-10-25 is the Berlin fall-back day: 00:30 local is +02:00, 23:30 local is +01:00.
        var result = CampaignInsights.Compute(
            new Campaign { Name = "Dst", StartedAtUtc = started },
            [mail1],
            [
                new(mail1.Id, TrackingEventType.EmailOpen, new DateTimeOffset(2026, 10, 24, 22, 30, 0, TimeSpan.Zero)),
                new(mail1.Id, TrackingEventType.EmailOpen, new DateTimeOffset(2026, 10, 25, 22, 30, 0, TimeSpan.Zero)),
            ],
            Berlin);

        var point = result.Timeline.ShouldHaveSingleItem();
        (point.Opens, point.Start.Hour, point.Start.Offset).ShouldBe((2, 0, TimeSpan.FromHours(2)));
    }

    [Fact]
    public void No_events_gives_zero_rates_and_no_median()
    {
        var result = Compute();
        result.MedianResponseHours.ShouldBeNull();
        result.Responders.ShouldBeEmpty();
    }
}
