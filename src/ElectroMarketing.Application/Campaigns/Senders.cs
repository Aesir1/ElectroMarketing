namespace ElectroMarketing.Application.Campaigns;

public sealed record EmailMessage(string To, string? ToName, string Subject, string HtmlBody, string UnsubscribeUrl);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public interface ISmsSender
{
    Task SendAsync(string toE164, string text, CancellationToken ct);
}

public sealed record Letter(string? Name, string? Street, string? PostalCode, string? City, string? Country, string Body, string Url);

public interface ILetterRenderer
{
    /// <summary>One PDF, one letter per page (more if the body is long).</summary>
    byte[] Render(IReadOnlyList<Letter> letters);
}
