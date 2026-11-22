using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Infrastructure.Campaigns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class SenderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Smtp_sender_delivers_html_with_one_click_unsubscribe_headers()
    {
        // Use the same pinned tag as the AppHost Mailpit container.
        await using var mailpit = new ContainerBuilder("axllent/mailpit:v1.33.1")
            .WithPortBinding(1025, true)
            .WithPortBinding(8025, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
            .Build();
        await mailpit.StartAsync(Ct);
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions { Host = mailpit.Hostname, Port = mailpit.GetMappedPublicPort(1025), From = "news@em.test" }));

        await sender.SendAsync(new EmailMessage("eve@x.de", "Eve", "Hello", "<p>Hi</p>", "https://em.test/u/Ab3dE5g"), Ct);

        // Subject and display name come from CSV data: line breaks must not become header injection.
        await sender.SendAsync(new EmailMessage("bob@x.de", "Bob\r\nBcc: x@evil.test", "Hi Bob\r\nBcc: x@evil.test", "<p>Hi</p>", "https://em.test/u/Zz9"), Ct);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{mailpit.Hostname}:{mailpit.GetMappedPublicPort(8025)}") };
        var list = await http.GetFromJsonAsync<JsonNode>("/api/v1/messages", Ct);
        var messages = list!["messages"]!.AsArray();
        var eve = messages.Single(m => m!["Subject"]!.GetValue<string>() == "Hello")!;
        var id = eve["ID"]!.GetValue<string>();
        var headers = await http.GetFromJsonAsync<JsonNode>($"/api/v1/message/{id}/headers", Ct);
        headers!["List-Unsubscribe"]![0]!.GetValue<string>().ShouldBe("<https://em.test/u/Ab3dE5g>");
        headers["List-Unsubscribe-Post"]![0]!.GetValue<string>().ShouldBe("List-Unsubscribe=One-Click");

        var bob = messages.Single(m => m!["To"]![0]!["Address"]!.GetValue<string>() == "bob@x.de")!;
        bob["Subject"]!.GetValue<string>().ShouldBe("Hi Bob  Bcc: x@evil.test");
        (bob["Bcc"] as JsonArray ?? []).ShouldBeEmpty();
    }

    [Fact]
    public async Task Twilio_sender_posts_form_with_basic_auth_and_throws_on_error()
    {
        HttpRequestMessage? seen = null;
        string? body = null;
        var handler = new StubHandler(async request =>
        {
            seen = request;
            body = await request.Content!.ReadAsStringAsync(Ct);
            return new HttpResponseMessage(body.Contains("%2B490", StringComparison.Ordinal) ? HttpStatusCode.BadRequest : HttpStatusCode.Created)
            {
                Content = new StringContent("{\"message\":\"invalid To\"}"),
            };
        });
        var sender = new TwilioSmsSender(new HttpClient(handler), Options.Create(new TwilioOptions { AccountSid = "AC1", AuthToken = "secret", From = "+4915100000" }));

        await sender.SendAsync("+491701234567", "Hi https://em.test/Ab3dE5g", Ct);

        seen!.RequestUri!.ToString().ShouldBe("https://api.twilio.com/2010-04-01/Accounts/AC1/Messages.json");
        seen.Headers.Authorization!.ToString().ShouldBe("Basic " + Convert.ToBase64String("AC1:secret"u8.ToArray()));
        body.ShouldBe("To=%2B491701234567&From=%2B4915100000&Body=Hi+https%3A%2F%2Fem.test%2FAb3dE5g");
        (await Should.ThrowAsync<HttpRequestException>(() => sender.SendAsync("+490", "x", Ct))).Message.ShouldContain("invalid To");
    }

    [Fact]
    public void Without_twilio_settings_sms_are_only_logged()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "localhost",
            ["Smtp:From"] = "news@em.test",
            ["Tracking:BaseUrl"] = "https://em.test",
            ["Tracking:LandingBaseUrl"] = "https://app.test",
            ["Campaigns:SenderLine"] = "EM · Street 1 · 12345 Town",
        }).Build();
        var services = new ServiceCollection().AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)).AddCampaignSending(configuration).BuildServiceProvider();

        services.GetRequiredService<ISmsSender>().ShouldBeOfType<LoggingSmsSender>();
        services.GetRequiredService<CampaignContent>().Link("Ab3dE5g").ShouldBe("https://em.test/Ab3dE5g");
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
