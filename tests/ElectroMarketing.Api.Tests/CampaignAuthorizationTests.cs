using System.Net;
using ElectroMarketing.Auth;
using ElectroMarketing.Auth.Persistence;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.Tests;

/// <summary>A principal with campaigns.read but without contacts.read must not reach contact data through campaigns.</summary>
public sealed class CampaignAuthorizationTests(ApiFactory api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // The seeded roles all carry contacts.read, so the test adds a role that only has campaigns.read.
    private async Task<HttpClient> CampaignsOnlyClientAsync()
    {
        var role = new AppRole { Id = Guid.CreateVersion7(), Name = $"campaigns-only-{Guid.NewGuid():N}" };
        role.NormalizedName = role.Name.ToUpperInvariant();
        await using var auth = new AuthDbContext(new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(api.AuthConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema)).Options);
        auth.Roles.Add(role);
        auth.RoleClaims.Add(new() { RoleId = role.Id, ClaimType = Permissions.ClaimType, ClaimValue = Permissions.Campaigns.Read });
        await auth.SaveChangesAsync(Ct);
        return api.ClientFor($"campaigns-only-{Guid.NewGuid():N}@local.test", role.Name);
    }

    [Fact]
    public async Task Message_contact_needs_contacts_read()
    {
        var contact = new Contact { Name = "Ann", Email = "ann@x.de" };
        var campaign = new Campaign { Name = "Sent", Status = CampaignStatus.Sent };
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(api.AppConnectionString).Options))
        {
            db.AddRange(contact, campaign, new CampaignMessage { CampaignId = campaign.Id, ContactId = contact.Id, Channel = Channel.Email, Token = ShortToken.New(), Status = MessageStatus.Sent });
            await db.SaveChangesAsync(Ct);
        }

        using var client = await CampaignsOnlyClientAsync();

        var ok = (await client.Gql($$"""{ campaignMessages(campaignId: "{{campaign.Id}}") { nodes { status } } }""")).Data();
        ok["campaignMessages"]!["nodes"]!.AsArray().Count.ShouldBe(1);

        var denied = await client.Gql($$"""{ campaignMessages(campaignId: "{{campaign.Id}}") { nodes { contact { name } } } }""");
        denied["errors"]![0]!["extensions"]!["code"]!.GetValue<string>().ShouldBe("AUTH_NOT_AUTHORIZED");
    }

    [Fact]
    public async Task Letters_download_needs_contacts_read()
    {
        using var client = await CampaignsOnlyClientAsync();

        (await client.GetAsync($"/campaigns/{Guid.NewGuid()}/letters", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
