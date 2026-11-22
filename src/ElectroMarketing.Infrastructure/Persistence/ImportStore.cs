using ElectroMarketing.Application.Imports;
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Persistence;

public sealed class ImportStore(AppDbContext db) : IImportStore
{
    public Task<ImportBatch?> GetBatchAsync(Guid id, CancellationToken ct) => db.ImportBatches.SingleOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Contact>> FindContactsAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> phones, CancellationToken ct) =>
        await db.Contacts
            .Where(c => (c.Email != null && emails.Contains(c.Email)) || (c.PhoneE164 != null && phones.Contains(c.PhoneE164)))
            .ToListAsync(ct);

    public void Add(Contact contact) => db.Contacts.Add(contact);

    public void Add(ContactImport import) => db.ContactImports.Add(import);

    public async Task SaveAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        DetachRows();
    }

    public void DiscardChanges() => DetachRows();

    private void DetachRows()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.Entity is not (ImportBatch or ImportRowError or ColumnMapping)).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
