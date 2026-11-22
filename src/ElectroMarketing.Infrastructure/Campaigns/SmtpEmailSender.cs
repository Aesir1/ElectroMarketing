using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Campaigns;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ElectroMarketing.Infrastructure.Campaigns;

public sealed class SmtpOptions : IValidatableObject
{
    public const string Section = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 25;

    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required]
    [EmailAddress]
    public string From { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Username) && !UseStartTls)
        {
            yield return new ValidationResult("UseStartTls must be true when Username is set (credentials must not travel in clear text).", [nameof(UseStartTls)]);
        }
    }
}

/// <summary>HTML mail with RFC 8058 one-click unsubscribe headers (required by Gmail/Yahoo for bulk senders).</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;

        // Subject and name come from contact (CSV) data: no line breaks, so no header injection.
        var mime = new MimeMessage { Subject = SingleLine(message.Subject) };
        mime.From.Add(new MailboxAddress(o.FromName, o.From));
        mime.To.Add(new MailboxAddress(message.ToName is null ? null : SingleLine(message.ToName), message.To));
        mime.Headers.Add("List-Unsubscribe", $"<{message.UnsubscribeUrl}>");
        mime.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody }.ToMessageBody();

        // ponytail: one SMTP connection per message; keep the connection open per chunk if the relay throttles connects.
        using var client = new SmtpClient();
        await client.ConnectAsync(o.Host, o.Port, o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);
        if (!string.IsNullOrEmpty(o.Username))
        {
            await client.AuthenticateAsync(o.Username, o.Password ?? string.Empty, ct);
        }

        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    private static string SingleLine(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
