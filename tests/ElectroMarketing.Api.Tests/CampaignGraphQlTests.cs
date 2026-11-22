using System.Net;
using System.Text.Json.Nodes;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.Tests;

/// <summary>Draft → validation → start → dispatcher (logging SMS sender, no Twilio in tests) → Sent.</summary>
public sealed class CampaignGraphQlTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sms_campaign_is_sent_by_the_dispatcher()
    {
        var listId = await SeedListAsync();
        using var client = api.ClientFor("campaigner@local.test", "user");
        const string fields = "name: \"Spring\", sendEmail: false, sendSms: true, sendPost: false";

        var created = (await client.Gql($$"""
            mutation { createCampaign(input: { campaign: { {{fields}}, contactListId: "{{listId}}" } }) { campaign { id status rowVersion } errors { __typename } } }
            """)).Data()["createCampaign"]!["campaign"]!;
        var id = created["id"]!.GetValue<string>();

        var invalid = (await client.Gql($$"""mutation { startCampaign(input: { id: "{{id}}" }) { errors { __typename ... on ValidationError { fields { field } } } } }""")).Data()["startCampaign"]!;
        invalid["errors"]![0]!["fields"]![0]!["field"]!.GetValue<string>().ShouldBe("smsText");

        await client.Gql(
            "mutation($id: UUID!, $c: CampaignDtoInput!, $v: UnsignedInt!) { updateCampaign(input: { id: $id, campaign: $c, rowVersion: $v }) { campaign { id } } }",
            new { id, v = created["rowVersion"]!.GetValue<uint>(), c = new { name = "Spring", sendEmail = false, sendSms = true, sendPost = false, contactListId = listId, smsText = "Hi {{Name}}" } });
        var started = (await client.Gql($$"""mutation { startCampaign(input: { id: "{{id}}" }) { campaign { status } errors { __typename } } }""")).Data()["startCampaign"]!;
        started["campaign"]!["status"]!.GetValue<string>().ShouldBe("SENDING");

        JsonNode? campaign = null;
        for (var i = 0; i < 40 && campaign?["status"]?.GetValue<string>() != "SENT"; i++)
        {
            await Task.Delay(500, Ct);
            campaign = (await client.Gql($$"""{ campaign(id: "{{id}}") { status } campaignMessages(campaignId: "{{id}}") { nodes { status token } } }""")).Data();
            campaign = campaign["campaign"];
        }

        campaign!["status"]!.GetValue<string>().ShouldBe("SENT");
        (await client.GetAsync($"/campaigns/{id}/letters", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Insights_for_an_unknown_campaign_are_null()
    {
        using var client = api.ClientFor("analyst@local.test", "user");

        var data = (await client.Gql($$"""{ campaignInsights(campaignId: "{{Guid.NewGuid()}}") { campaignId } }""")).Data();

        data["campaignInsights"].ShouldBeNull();
    }

    private async Task<Guid> SeedListAsync()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(api.AppConnectionString).Options);
        var contact = new Contact { Name = "Ann", PhoneE164 = $"+49170{Random.Shared.Next(1_000_000, 9_999_999)}" };
        contact.RefreshSearchText();
        var list = new ContactList { Name = "Campaign test" };
        db.AddRange(contact, list, new ContactListMember { ContactListId = list.Id, ContactId = contact.Id });
        await db.SaveChangesAsync(Ct);
        return list.Id;
    }
}
