using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Campaigns;

public sealed class TrackingOptions
{
    public const string Section = "Tracking";

    /// <summary>Public origin of the short links: the Api, or a short domain routed to it.</summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Origin of the web app that serves <c>/l/{token}</c>.</summary>
    [Required]
    [Url]
    public string LandingBaseUrl { get; set; } = string.Empty;
}

public sealed class CampaignOptions
{
    public const string Section = "Campaigns";

    /// <summary>Small sender line above the address window of letters.</summary>
    [Required]
    public string SenderLine { get; set; } = string.Empty;

    [Range(0.01, 1000)]
    public double EmailsPerSecond { get; set; } = 10;

    [Range(0.01, 100)]
    public double SmsPerSecond { get; set; } = 1;

    /// <summary>IANA zone used to bucket insights by local hour and day.</summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Berlin";
}

public static partial class MessageTemplate
{
    /// <summary>Replaces <c>{{Key}}</c>; values are HTML-encoded when <paramref name="html"/> (contact data comes from CSV files). Unknown keys stay.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> values, bool html) =>
        Placeholder().Replace(template, m => Replacement(m, values, html));

    private static string Replacement(Match m, IReadOnlyDictionary<string, string> values, bool html)
    {
        if (!values.TryGetValue(m.Groups[1].Value, out var v))
        {
            return m.Value;
        }

        return html ? WebUtility.HtmlEncode(v) : v;
    }

    [GeneratedRegex(@"\{\{(\w+)\}\}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();
}

public static class RecipientRules
{
    /// <summary>Why the contact cannot get a message on the channel, or null when it can.</summary>
    public static string? SkipReason(Channel channel, Contact contact) => channel switch
    {
        Channel.Email when contact.Email is null => "No e-mail address.",
        Channel.Email when contact.EmailOptOut => "Unsubscribed from e-mail.",
        Channel.Sms when contact.PhoneE164 is null => "No phone number.",
        Channel.Sms when contact.SmsOptOut => "Unsubscribed from SMS.",
        Channel.Post when contact.Street is null || contact.City is null => "No postal address.",
        _ => null,
    };
}

/// <summary>Builds the per-contact message for each channel. Every link carries the message token.</summary>
public sealed class CampaignContent(TrackingOptions tracking)
{
    private string Base => tracking.BaseUrl.TrimEnd('/');

    public string Link(string token) => $"{Base}/{token}";

    public string Pixel(string token) => $"{Base}/o/{token}";

    public string Unsubscribe(string token) => $"{Base}/u/{token}";

    public string Landing(string token) => $"{tracking.LandingBaseUrl.TrimEnd('/')}/l/{token}";

    public EmailMessage Email(Campaign campaign, Contact contact, string token)
    {
        var values = Values(contact, token);
        var body = MessageTemplate.Render(campaign.EmailBody ?? string.Empty, values, html: true);
        if (campaign.EmailBody?.Contains("{{Link}}", StringComparison.Ordinal) != true)
        {
            body += $"""<p><a href="{Link(token)}">{Link(token)}</a></p>""";
        }

        body += $"""<p style="font-size:12px;color:#666"><a href="{Unsubscribe(token)}">Unsubscribe</a></p><img src="{Pixel(token)}" width="1" height="1" alt="">""";
        return new EmailMessage(contact.Email!, contact.Name, MessageTemplate.Render(campaign.EmailSubject ?? string.Empty, values, html: false), body, Unsubscribe(token));
    }

    public string Sms(Campaign campaign, Contact contact, string token)
    {
        var text = MessageTemplate.Render(campaign.SmsText ?? string.Empty, Values(contact, token), html: false);
        return text.Contains(Link(token), StringComparison.Ordinal) ? text : $"{text} {Link(token)}";
    }

    public Letter Letter(Campaign campaign, Contact contact, string token) =>
        new(
            contact.Name,
            contact.Street,
            contact.PostalCode,
            contact.City,
            contact.Country,
            MessageTemplate.Render(campaign.LetterBody ?? string.Empty, Values(contact, token), html: false),
            Link(token));

    private Dictionary<string, string> Values(Contact contact, string token) => new()
    {
        ["Name"] = contact.Name ?? string.Empty,
        ["Link"] = Link(token),
        ["UnsubscribeLink"] = Unsubscribe(token),
    };
}
