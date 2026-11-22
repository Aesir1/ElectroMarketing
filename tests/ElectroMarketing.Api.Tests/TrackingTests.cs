// tests/ElectroMarketing.Api.Tests/TrackingTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.Tests;

public sealed class TrackingTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(api.AppConnectionString).Options);

    private HttpClient Anonymous() => api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private async Task<(string Token, Guid MessageId, Guid ContactId)> SeedAsync(Channel channel)
    {
        await using var db = Db();
        var contact = new Contact { Email = $"{Guid.NewGuid():N}@x.test" };
        contact.RefreshSearchText();
        var campaign = new Campaign { Name = "T", LandingTitle = "Solar panels", LandingBody = "Call us.", Status = CampaignStatus.Sent };
        var message = new CampaignMessage { CampaignId = campaign.Id, ContactId = contact.Id, Channel = channel, Token = ShortToken.New(), Status = MessageStatus.Sent };
        db.AddRange(contact, campaign, message);
        await db.SaveChangesAsync(Ct);
        return (message.Token, message.Id, contact.Id);
    }

    private async Task<string[]> EventsAsync(Guid messageId)
    {
        await using var db = Db();
        return await db.TrackingEvents.Where(e => e.MessageId == messageId).OrderBy(e => e.Id).Select(e => e.Type.ToString()).ToArrayAsync(Ct);
    }

    [Fact]
    public async Task Short_link_records_click_and_redirects_to_landing_page()
    {
        var (token, messageId, _) = await SeedAsync(Channel.Sms);
        using var client = Anonymous();

        using var response = await client.GetAsync($"/{token}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe($"https://localhost:3000/l/{token}");
        (await EventsAsync(messageId)).ShouldBe(["LinkClick"]);
    }

    [Fact]
    public async Task Unknown_or_malformed_tokens_are_404_and_graphql_route_still_wins()
    {
        using var client = Anonymous();

        (await client.GetAsync("/Zz9Zz9Z", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/landing/bad!", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync("/graphql", new { query = "{ __typename }" }, Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Open_pixel_is_a_gif_even_for_unknown_tokens()
    {
        var (token, messageId, _) = await SeedAsync(Channel.Email);
        using var client = Anonymous();

        using var known = await client.GetAsync($"/o/{token}", Ct);
        using var unknown = await client.GetAsync("/o/Zz9Zz9Z", Ct);

        known.Content.Headers.ContentType!.MediaType.ShouldBe("image/gif");
        unknown.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await EventsAsync(messageId)).ShouldBe(["EmailOpen"]);
    }

    [Fact]
    public async Task Get_on_unsubscribe_only_asks_and_post_opts_out()
    {
        var (token, messageId, contactId) = await SeedAsync(Channel.Email);
        using var client = Anonymous();

        (await client.GetStringAsync($"/u/{token}", Ct)).ShouldContain("<form method=\"post\"");
        await using (var db = Db())
        {
            (await db.Contacts.SingleAsync(c => c.Id == contactId, Ct)).EmailOptOut.ShouldBeFalse();
        }

        using var post = await client.PostAsync($"/u/{token}", new FormUrlEncodedContent(new Dictionary<string, string> { ["List-Unsubscribe"] = "One-Click" }), Ct);

        post.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var read = Db();
        (await read.Contacts.SingleAsync(c => c.Id == contactId, Ct)).EmailOptOut.ShouldBeTrue();
        (await EventsAsync(messageId)).ShouldBe(["Unsubscribe"]);
    }

    [Fact]
    public async Task Landing_returns_campaign_text_and_records_a_view()
    {
        var (token, messageId, _) = await SeedAsync(Channel.Post);
        using var client = Anonymous();

        var page = await client.GetFromJsonAsync<JsonNode>($"/landing/{token}", Ct);

        (page!["title"]!.GetValue<string>(), page["body"]!.GetValue<string>()).ShouldBe(("Solar panels", "Call us."));
        (await EventsAsync(messageId)).ShouldBe(["LandingView"]);
    }
}
