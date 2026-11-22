using System.Net.Http.Headers;
using System.Text;
using ElectroMarketing.Application.Campaigns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Campaigns;

public sealed class TwilioOptions
{
    public const string Section = "Twilio";

    public string AccountSid { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Sender number (E.164) or alphanumeric sender id.</summary>
    public string From { get; set; } = string.Empty;
}

/// <summary>Twilio Messages REST API; any non-2xx answer throws with Twilio's message.</summary>
public sealed class TwilioSmsSender(HttpClient http, IOptions<TwilioOptions> options) : ISmsSender
{
    public async Task SendAsync(string toE164, string text, CancellationToken ct)
    {
        var o = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{o.AccountSid}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["To"] = toE164, ["From"] = o.From, ["Body"] = text }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.AccountSid}:{o.AuthToken}")));
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Twilio {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
        }
    }
}

/// <summary>Outside development without Twilio: fail loudly instead of logging phone numbers and tracking links.</summary>
public sealed class UnconfiguredSmsSender : ISmsSender
{
    public Task SendAsync(string toE164, string text, CancellationToken ct) =>
        throw new InvalidOperationException("SMS is not configured (Twilio).");
}

/// <summary>Used when no Twilio account is configured (development): the SMS only goes to the log.</summary>
public sealed class LoggingSmsSender(ILogger<LoggingSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string toE164, string text, CancellationToken ct)
    {
        logger.LogInformation("SMS (not sent, Twilio not configured) to {To}: {Text}", toE164, text);
        return Task.CompletedTask;
    }
}
