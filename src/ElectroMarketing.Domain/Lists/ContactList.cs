using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Domain.Lists;

/// <summary>A named working set of contacts, built from an import lot and/or picked by hand.</summary>
public class ContactList : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>The import lot the list was created from, if any.</summary>
    public Guid? SourceBatchId { get; set; }

    public ICollection<ContactListMember> Members { get; set; } = [];
}

public class ContactListMember
{
    public Guid ContactListId { get; set; }

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public DateTimeOffset AddedAtUtc { get; set; }
}
