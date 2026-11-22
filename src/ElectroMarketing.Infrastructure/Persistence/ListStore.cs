using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Lists;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class ListStore(AppDbContext db, TimeProvider time) : IListStore
{
    public Task<int> AddMembersAsync(Guid listId, ContactFilter filter, CancellationToken ct) =>
        AddAsync(listId, ContactSearch.Apply(db.Contacts, filter), ct);

    public Task<int> AddMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct) =>
        AddAsync(listId, db.Contacts.Where(c => contactIds.Contains(c.Id)), ct);

    public Task<int> RemoveMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct) =>
        db.ContactListMembers.Where(m => m.ContactListId == listId && contactIds.Contains(m.ContactId)).ExecuteDeleteAsync(ct);

    // ponytail: loads the new member ids into memory (fine for 100k contacts); switch to INSERT ... SELECT if lists get bigger.
    private async Task<int> AddAsync(Guid listId, IQueryable<Contact> contacts, CancellationToken ct)
    {
        var ids = await contacts
            .Select(c => c.Id)
            .Where(id => !db.ContactListMembers.Any(m => m.ContactListId == listId && m.ContactId == id))
            .ToListAsync(ct);
        var now = time.GetUtcNow();
        db.ContactListMembers.AddRange(ids.Select(id => new ContactListMember { ContactListId = listId, ContactId = id, AddedAtUtc = now }));
        await db.SaveChangesAsync(ct);
        return ids.Count;
    }
}
