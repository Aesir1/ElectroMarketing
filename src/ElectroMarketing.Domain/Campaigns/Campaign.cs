using System.Security.Cryptography;
using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Domain.Campaigns;

public enum CampaignStatus
{
    Draft,
    Sending,
    Sent,
}

public enum Channel
{
    Email,
    Sms,
    Post,
}

public enum MessageStatus
{
    Queued,
    Sent,
    Failed,
    Skipped,
}

public enum TrackingEventType
{
    EmailOpen,
    LinkClick,
    LandingView,
    Unsubscribe,
}

/// <summary>Editable while Draft. Starting it snapshots the list into <see cref="CampaignMessage"/> rows.</summary>
public class Campaign : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>Null after the list was deleted; sent campaigns keep their messages.</summary>
    public Guid? ContactListId { get; set; }

    public bool SendEmail { get; set; }

    public bool SendSms { get; set; }

    public bool SendPost { get; set; }

    public string? EmailSubject { get; set; }

    /// <summary>HTML with placeholders <c>{{Name}}</c>, <c>{{Link}}</c>, <c>{{UnsubscribeLink}}</c>.</summary>
    public string? EmailBody { get; set; }

    public string? SmsText { get; set; }

    public string? LetterBody { get; set; }

    public string? LandingTitle { get; set; }

    /// <summary>Plain text shown on the public landing page.</summary>
    public string? LandingBody { get; set; }

    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? FinishedAtUtc { get; set; }

    /// <summary>The merged letters PDF exists at <see cref="LettersKey"/>.</summary>
    public bool LettersReady { get; set; }

    public static string LettersKey(Guid campaignId) => $"campaigns/{campaignId}/letters.pdf";

    public IEnumerable<Channel> Channels()
    {
        if (SendEmail)
        {
            yield return Channel.Email;
        }

        if (SendSms)
        {
            yield return Channel.Sms;
        }

        if (SendPost)
        {
            yield return Channel.Post;
        }
    }
}

/// <summary>One contact on one channel. <see cref="Token"/> is the random id in every link of this message.</summary>
public class CampaignMessage : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CampaignId { get; set; }

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public Channel Channel { get; set; }

    public required string Token { get; set; }

    public MessageStatus Status { get; set; } = MessageStatus.Queued;

    public DateTimeOffset? SentAtUtc { get; set; }

    /// <summary>Failure message or skip reason.</summary>
    public string? Error { get; set; }
}

/// <summary>Open, click, landing view or unsubscribe of one message. No IP address is stored.</summary>
public class TrackingEvent : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid MessageId { get; set; }

    public CampaignMessage? Message { get; set; }

    public TrackingEventType Type { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? UserAgent { get; set; }
}

/// <summary>7 random base62 characters: 62^7 ≈ 3.5·10^12 values, short enough for SMS and QR codes, not guessable in bulk.</summary>
public static class ShortToken
{
    public const int Length = 7;
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string New() => RandomNumberGenerator.GetString(Alphabet, Length);

    public static bool IsValid(string token) => token.Length == Length && token.All(char.IsAsciiLetterOrDigit);
}
