using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Domain.Campaigns;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class CampaignStore(AppDbContext db) : ICampaignStore
{
    public async Task<int> AddRecipientsAsync(Campaign campaign, CancellationToken ct)
    {
        var contacts = await db.ContactListMembers.AsNoTracking()
            .Where(m => m.ContactListId == campaign.ContactListId)
            .Select(m => m.Contact!)
            .ToListAsync(ct);
        var queued = 0;
        foreach (var contact in contacts)
        {
            foreach (var channel in campaign.Channels())
            {
                var reason = RecipientRules.SkipReason(channel, contact);
                queued += reason is null ? 1 : 0;
                db.CampaignMessages.Add(new CampaignMessage
                {
                    CampaignId = campaign.Id,
                    ContactId = contact.Id,
                    Channel = channel,
                    Token = ShortToken.New(),
                    Status = reason is null ? MessageStatus.Queued : MessageStatus.Skipped,
                    Error = reason,
                });
            }
        }

        return queued;
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
