using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.Tests;

public sealed class ContactSearchGraphQlTests(ApiFactory api)
{
    [Fact]
    public async Task Client_order_wins_over_the_default_name_order()
    {
        var tag = Guid.NewGuid().ToString("N");
        await using (var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(api.AppConnectionString).Options))
        {
            foreach (var (name, city) in new[] { ("Anna", "Zeta"), ("Bert", "Alpha"), ("Carl", "Mid") })
            {
                var contact = new Contact { Name = $"{name} {tag}", City = city };
                contact.RefreshSearchText();
                ctx.Contacts.Add(contact);
            }

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = api.ClientFor($"search-{tag}@local.test", "user");
        var nodes = (await client.Gql($$"""
            { contacts(filter: { search: "{{tag}}" }, order: [{ city: ASC }]) { totalCount nodes { city } } }
            """)).Data()["contacts"]!["nodes"]!.AsArray().Select(n => n!["city"]!.GetValue<string>());

        nodes.ShouldBe(["Alpha", "Mid", "Zeta"]);

        var byName = (await client.Gql($$"""
            { contacts(filter: { search: "{{tag}}" }) { nodes { city } } }
            """)).Data()["contacts"]!["nodes"]!.AsArray().Select(n => n!["city"]!.GetValue<string>());
        byName.ShouldBe(["Zeta", "Alpha", "Mid"]);
    }
}
