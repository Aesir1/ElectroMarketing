using System.Globalization;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Imports;

/// <summary>
/// The upsert rule: a non-empty incoming value replaces a different stored value; an empty one never erases.
/// The phone counts as changed only when its E.164 form differs. The obtained date keeps the newest.
/// </summary>
public static class ContactMerger
{
    public static List<FieldChange> Apply(Contact contact, ContactRow row)
    {
        var changes = new List<FieldChange>();
        Set(changes, nameof(Contact.Name), contact.Name, row.Name, v => contact.Name = v);
        Set(changes, nameof(Contact.Email), contact.Email, row.Email, v => contact.Email = v);
        if (row.PhoneE164 is not null && row.PhoneE164 != contact.PhoneE164)
        {
            changes.Add(new FieldChange { Field = nameof(Contact.Phone), Old = contact.Phone, New = row.Phone });
            contact.Phone = row.Phone;
            contact.PhoneE164 = row.PhoneE164;
        }

        Set(changes, nameof(Contact.Street), contact.Street, row.Street, v => contact.Street = v);
        Set(changes, nameof(Contact.PostalCode), contact.PostalCode, row.PostalCode, v => contact.PostalCode = v);
        Set(changes, nameof(Contact.City), contact.City, row.City, v => contact.City = v);
        Set(changes, nameof(Contact.Country), contact.Country, row.Country, v => contact.Country = v);
        if (row.ObtainedOn is { } obtained && (contact.ObtainedOn is null || obtained > contact.ObtainedOn))
        {
            changes.Add(new FieldChange { Field = nameof(Contact.ObtainedOn), Old = Iso(contact.ObtainedOn), New = Iso(obtained) });
            contact.ObtainedOn = obtained;
        }

        contact.RefreshSearchText();
        return changes;
    }

    private static void Set(List<FieldChange> changes, string field, string? current, string? incoming, Action<string> set)
    {
        if (incoming is null || string.Equals(current, incoming, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new FieldChange { Field = field, Old = current, New = incoming });
        set(incoming);
    }

    private static string? Iso(DateOnly? date) => date?.ToString("O", CultureInfo.InvariantCulture);
}
