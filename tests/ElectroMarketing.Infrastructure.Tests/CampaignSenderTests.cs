using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class CampaignSenderTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly IEmailSender email = Substitute.For<IEmailSender>();
    private readonly ISmsSender sms = Substitute.For<ISmsSender>();
    private readonly ILetterRenderer letters = Substitute.For<ILetterRenderer>();
    private readonly IObjectStorage storage = Substitute.For<IObjectStorage>();
    private Guid campaignId;
    private Guid listId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        letters.Render(Arg.Any<IReadOnlyList<Letter>>()).Returns([1, 2, 3]);
        await using var ctx = db.Create();
        var list = new ContactList { Name = "L" };
        Contact[] contacts =
        [
            new() { Name = "Ann", Email = "ann@x.de", PhoneE164 = "+491701111111", Street = "A 1", City = "Köln" },
            new() { Name = "Bob", Email = "bob@x.de", PhoneE164 = "+491702222222", Street = "B 2", City = "Bonn" },
        ];
        listId = list.Id;
        ctx.AddRange(list);
        ctx.Contacts.AddRange(contacts);
        ctx.ContactListMembers.AddRange(contacts.Select(c => new ContactListMember { ContactListId = list.Id, ContactId = c.Id }));
        await ctx.SaveChangesAsync(Ct);

        var service = new CampaignService(new Repository<Campaign, Guid>(ctx), new CampaignStore(ctx), TimeProvider.System);
        var campaign = await service.CreateAsync(
            new CampaignDto("C", list.Id, true, true, true, "Hi {{Name}}", "<p>{{Link}}</p>", "Hi {{Name}}", "Dear {{Name}}", null, null), Ct);
        campaignId = (await service.StartAsync(campaign.Id, Ct)).Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private CampaignSender Sender(AppDbContext ctx) => new(
        new Repository<Campaign, Guid>(ctx),
        new CampaignStore(ctx),
        new CampaignContent(new TrackingOptions { BaseUrl = "https://em.test", LandingBaseUrl = "https://app.test" }),
        email,
        sms,
        letters,
        storage,
        new CampaignOptions { SenderLine = "S", EmailsPerSecond = 1000, SmsPerSecond = 100 },
        TimeProvider.System,
        NullLogger<CampaignSender>.Instance);

    [Fact]
    public async Task Sends_every_channel_marks_messages_and_finishes()
    {
        sms.SendAsync("+491702222222", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new HttpRequestException("Twilio 400: bad")));

        await using (var ctx = db.Create())
        {
            await Sender(ctx).SendPendingAsync(Ct);
        }

        await email.Received(2).SendAsync(Arg.Is<EmailMessage>(m => m.HtmlBody.Contains("https://em.test/o/")), Arg.Any<CancellationToken>());
        await sms.Received(1).SendAsync("+491701111111", Arg.Is<string>(t => t.StartsWith("Hi Ann https://em.test/")), Arg.Any<CancellationToken>());
        letters.Received(1).Render(Arg.Is<IReadOnlyList<Letter>>(l => l.Count == 2));
        await storage.Received(1).PutAsync("documents", Campaign.LettersKey(campaignId), Arg.Any<Stream>(), "application/pdf", Arg.Any<CancellationToken>());

        await using var read = db.Create();
        var campaign = await read.Campaigns.SingleAsync(Ct);
        (campaign.Status, campaign.LettersReady).ShouldBe((CampaignStatus.Sent, true));
        var failed = await read.CampaignMessages.SingleAsync(m => m.Status == MessageStatus.Failed, Ct);
        failed.Error.ShouldBe("Twilio 400: bad");
        (await read.CampaignMessages.CountAsync(m => m.Status == MessageStatus.Sent && m.SentAtUtc != null, Ct)).ShouldBe(5);
    }

    [Fact]
    public async Task Restart_only_sends_what_is_still_queued()
    {
        await using (var ctx = db.Create())
        {
            foreach (var channel in new[] { Channel.Email, Channel.Sms })
            {
                var first = await ctx.CampaignMessages.Where(m => m.Channel == channel).OrderBy(m => m.Id).FirstAsync(Ct);
                first.Status = MessageStatus.Sent;
            }

            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Sender(ctx).SendPendingAsync(Ct);
            await Sender(ctx).SendPendingAsync(Ct);
        }

        await email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await sms.Received(1).SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Letters_failure_fails_the_post_messages_and_does_not_block_other_campaigns()
    {
        letters.Render(Arg.Any<IReadOnlyList<Letter>>()).Returns(_ => throw new InvalidOperationException("render boom"));
        Guid second;
        await using (var ctx = db.Create())
        {
            var service = new CampaignService(new Repository<Campaign, Guid>(ctx), new CampaignStore(ctx), TimeProvider.System);
            var created = await service.CreateAsync(new CampaignDto("D", listId, true, false, false, "Hi", "<p>x</p>", null, null, null, null), Ct);
            second = (await service.StartAsync(created.Id, Ct)).Id;
        }

        await using (var ctx = db.Create())
        {
            await Sender(ctx).SendPendingAsync(Ct);
        }

        await using var read = db.Create();
        var posts = await read.CampaignMessages.Where(m => m.CampaignId == campaignId && m.Channel == Channel.Post).ToListAsync(Ct);
        posts.ShouldAllBe(m => m.Status == MessageStatus.Failed && m.Error == "render boom");
        (await read.Campaigns.SingleAsync(c => c.Id == campaignId, Ct)).LettersReady.ShouldBeFalse();
        (await read.Campaigns.SingleAsync(c => c.Id == second, Ct)).Status.ShouldBe(CampaignStatus.Sent);
        (await read.CampaignMessages.CountAsync(m => m.CampaignId == second && m.Status == MessageStatus.Sent, Ct)).ShouldBe(2);
    }
}
