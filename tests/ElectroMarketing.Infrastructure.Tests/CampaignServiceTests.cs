using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class CampaignServiceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly ContactList list = new() { Name = "L" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        await using var ctx = db.Create();
        ctx.ContactLists.Add(list);
        Contact[] contacts =
        [
            new() { Name = "Full", Email = "full@x.de", PhoneE164 = "+491701111111", Street = "A 1", City = "Köln" },
            new() { Name = "MailOnly", Email = "mail@x.de" },
            new() { Name = "OptOut", Email = "out@x.de", EmailOptOut = true, PhoneE164 = "+491702222222" },
        ];
        ctx.Contacts.AddRange(contacts);
        ctx.ContactListMembers.AddRange(contacts.Select(c => new ContactListMember { ContactListId = list.Id, ContactId = c.Id }));
        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CampaignService Service(AppDbContext ctx) =>
        new(new Repository<Campaign, Guid>(ctx), new CampaignStore(ctx), TimeProvider.System);

    private CampaignDto Dto(bool email = true, bool sms = true, bool post = true, string? smsText = "Hi {{Name}}") =>
        new("Spring", list.Id, email, sms, post, "Subject", "<p>Body</p>", smsText, "Letter", "Title", "Body");

    [Fact]
    public async Task Start_snapshots_one_message_per_contact_and_channel_with_skip_reasons()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var campaign = await service.CreateAsync(Dto(), Ct);

        var started = await service.StartAsync(campaign.Id, Ct);

        started.Status.ShouldBe(CampaignStatus.Sending);
        await using var read = db.Create();
        var messages = await read.CampaignMessages.Include(m => m.Contact).ToListAsync(Ct);
        messages.Count.ShouldBe(9);
        messages.Select(m => m.Token).Distinct().Count().ShouldBe(9);
        messages.Where(m => m.Status == MessageStatus.Queued).Select(m => $"{m.Contact!.Name}:{m.Channel}")
            .ShouldBe(["Full:Email", "Full:Sms", "Full:Post", "MailOnly:Email", "OptOut:Sms"], ignoreOrder: true);
        messages.Single(m => m.Contact!.Name == "OptOut" && m.Channel == Channel.Email).Error.ShouldBe("Unsubscribed from e-mail.");
    }

    [Fact]
    public async Task Started_campaign_cannot_be_started_edited_or_deleted()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var campaign = await service.CreateAsync(Dto(), Ct);
        await service.StartAsync(campaign.Id, Ct);

        await Should.ThrowAsync<ConflictException>(() => service.StartAsync(campaign.Id, Ct));
        await Should.ThrowAsync<ConflictException>(() => service.UpdateAsync(campaign.Id, Dto(), Ct));
        await Should.ThrowAsync<ConflictException>(() => service.DeleteAsync(campaign.Id, Ct));
    }

    [Fact]
    public async Task Start_validates_channel_content_and_reachability()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);

        var noText = await service.CreateAsync(Dto(smsText: null), Ct);
        (await Should.ThrowAsync<ValidationException>(() => service.StartAsync(noText.Id, Ct))).Errors.Keys.ShouldBe(["smsText"]);

        var nothing = await service.CreateAsync(Dto(email: false, sms: false, post: false), Ct);
        (await Should.ThrowAsync<ValidationException>(() => service.StartAsync(nothing.Id, Ct))).Errors.Keys.ShouldBe(["channels"]);
    }

    [Fact]
    public async Task Deleting_the_list_keeps_the_sent_messages()
    {
        await using (var ctx = db.Create())
        {
            var service = Service(ctx);
            await service.StartAsync((await service.CreateAsync(Dto(), Ct)).Id, Ct);
            ctx.ContactLists.Remove(await ctx.ContactLists.SingleAsync(Ct));
            await ctx.SaveChangesAsync(Ct);
        }

        await using var read = db.Create();
        (await read.Campaigns.SingleAsync(Ct)).ContactListId.ShouldBeNull();
        (await read.CampaignMessages.CountAsync(Ct)).ShouldBe(9);
    }
}
