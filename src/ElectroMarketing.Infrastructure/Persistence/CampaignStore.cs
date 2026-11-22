using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class CampaignStore(AppDbContext db, Func<string>? newToken = null) : ICampaignStore
{
    private const int TokenBatch = 10_000;
    private readonly Func<string> newToken = newToken ?? ShortToken.New;

    public Task<bool> ListExistsAsync(Guid listId, CancellationToken ct) => db.ContactLists.AnyAsync(l => l.Id == listId, ct);

    // ponytail: in-request snapshot (all messages in memory); switch to INSERT...SELECT when lists exceed ~100k contacts.
    public async Task<int> AddRecipientsAsync(Campaign campaign, CancellationToken ct)
    {
        // Only the columns SkipReason reads; the transient Contact is never tracked.
        var rows = await db.ContactListMembers.AsNoTracking()
            .Where(m => m.ContactListId == campaign.ContactListId)
            .Select(m => new { m.ContactId, m.Contact!.Email, m.Contact.EmailOptOut, m.Contact.PhoneE164, m.Contact.SmsOptOut, m.Contact.PostOptOut, m.Contact.Street, m.Contact.City })
            .ToListAsync(ct);
        var queued = 0;
        var messages = new List<CampaignMessage>();
        foreach (var r in rows)
        {
            var contact = new Contact { Email = r.Email, EmailOptOut = r.EmailOptOut, PhoneE164 = r.PhoneE164, SmsOptOut = r.SmsOptOut, PostOptOut = r.PostOptOut, Street = r.Street, City = r.City };
            foreach (var channel in campaign.Channels())
            {
                var reason = RecipientRules.SkipReason(channel, contact);
                queued += reason is null ? 1 : 0;
                messages.Add(new CampaignMessage
                {
                    CampaignId = campaign.Id,
                    ContactId = r.ContactId,
                    Channel = channel,
                    Token = newToken(),
                    Status = reason is null ? MessageStatus.Queued : MessageStatus.Skipped,
                    Error = reason,
                });
            }
        }

        await MakeTokensUniqueAsync(messages, ct);

        var detect = db.ChangeTracker.AutoDetectChangesEnabled;
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            db.CampaignMessages.AddRange(messages);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = detect;
        }

        return queued;
    }

    /// <summary>The token index is global, so check the new tokens against the stored ones and each other; regenerate only the clashes.</summary>
    private async Task MakeTokensUniqueAsync(List<CampaignMessage> messages, CancellationToken ct)
    {
        while (true)
        {
            var clashing = new HashSet<CampaignMessage>();
            var seen = new HashSet<string>();
            foreach (var m in messages.Where(m => !seen.Add(m.Token)))
            {
                clashing.Add(m);
            }

            foreach (var batch in messages.Select(m => m.Token).Distinct().Chunk(TokenBatch))
            {
                var taken = (await db.CampaignMessages.AsNoTracking().Where(m => batch.Contains(m.Token)).Select(m => m.Token).ToListAsync(ct)).ToHashSet();
                clashing.UnionWith(messages.Where(m => taken.Contains(m.Token)));
            }

            if (clashing.Count == 0)
            {
                return;
            }

            foreach (var m in clashing)
            {
                m.Token = newToken();
            }
        }
    }

    public async Task<IReadOnlyList<CampaignMessage>> NextQueuedAsync(Guid campaignId, Channel channel, int take, CancellationToken ct) =>
        await db.CampaignMessages
            .Include(m => m.Contact)
            .Where(m => m.CampaignId == campaignId && m.Channel == channel && m.Status == MessageStatus.Queued)
            .OrderBy(m => m.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is CampaignMessage or Contact).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
