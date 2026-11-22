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
            var first = await ctx.CampaignMessages.Where(m => m.Channel == Channel.Email).OrderBy(m => m.Id).FirstAsync(Ct);
            first.Status = MessageStatus.Sent;
            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Sender(ctx).SendPendingAsync(Ct);
            await Sender(ctx).SendPendingAsync(Ct);
        }

        await email.Received(1).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }
}
