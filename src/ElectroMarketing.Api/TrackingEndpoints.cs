// src/ElectroMarketing.Api/TrackingEndpoints.cs
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Application.Tracking;
using ElectroMarketing.Domain.Campaigns;

namespace ElectroMarketing.Api;

/// <summary>
/// Public short-link routes. <c>/{token}</c> sits at the root so links stay short; literal routes such as
/// <c>/graphql</c> and <c>/health</c> take precedence over the parameter route.
/// </summary>
public static class TrackingEndpoints
{
    // 1×1 transparent GIF.
    private static readonly byte[] Pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    public static IEndpointRouteBuilder MapTrackingEndpoints(this IEndpointRouteBuilder app)
    {
        var tracking = app.MapGroup(string.Empty).AllowAnonymous();
        tracking.MapGet($"/{{token:length({ShortToken.Length})}}", ClickAsync);
        tracking.MapGet("/o/{token}", OpenAsync);
        tracking.MapGet("/u/{token}", AskUnsubscribe);
        tracking.MapPost("/u/{token}", UnsubscribeAsync).DisableAntiforgery();
        tracking.MapGet("/landing/{token}", LandingAsync);
        return app;
    }

    private static async Task<IResult> ClickAsync(string token, HttpRequest request, TrackingService tracking, TrackingOptions options, CancellationToken ct) =>
        await tracking.RecordAsync(token, TrackingEventType.LinkClick, UserAgent(request), ct)
            ? Results.Redirect($"{options.LandingBaseUrl.TrimEnd('/')}/l/{token}")
            : Results.NotFound();

    /// <summary>Always answers with the pixel so mail clients never show a broken image.</summary>
    private static async Task<IResult> OpenAsync(string token, HttpRequest request, HttpResponse response, TrackingService tracking, CancellationToken ct)
    {
        await tracking.RecordAsync(token, TrackingEventType.EmailOpen, UserAgent(request), ct);
        response.Headers.CacheControl = "no-store";
        return Results.File(Pixel, "image/gif");
    }

    /// <summary>GET only asks: mail security scanners fetch every link, so a GET must never unsubscribe.</summary>
    private static IResult AskUnsubscribe(string token) =>
        ShortToken.IsValid(token)
            ? Html("""<form method="post"><p>Do you want to stop receiving these messages?</p><button>Unsubscribe</button></form>""")
            : Results.NotFound();

    /// <summary>Form button and RFC 8058 one-click POST from mail clients.</summary>
    private static async Task<IResult> UnsubscribeAsync(string token, HttpRequest request, TrackingService tracking, CancellationToken ct) =>
        await tracking.UnsubscribeAsync(token, UserAgent(request), ct)
            ? Html("<p>You are unsubscribed. You will not receive these messages any more.</p>")
            : Results.NotFound();

    private static async Task<IResult> LandingAsync(string token, HttpRequest request, TrackingService tracking, CancellationToken ct) =>
        await tracking.LandingAsync(token, UserAgent(request), ct) is { } page ? Results.Ok(page) : Results.NotFound();

    private static string? UserAgent(HttpRequest request) => request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private static IResult Html(string body) => Results.Content(
        $"""<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Subscription</title><body style="font-family:system-ui;max-width:32rem;margin:3rem auto;padding:0 1rem">{body}</body></html>""",
        "text/html; charset=utf-8");
}
