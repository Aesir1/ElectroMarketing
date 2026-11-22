// src/ElectroMarketing.Domain/Contacts/Contact.cs
using ElectroMarketing.Domain.Abstractions;

namespace ElectroMarketing.Domain.Contacts;

/// <summary>One person or company. Only imports change contact data, through <c>ContactMerger</c>.</summary>
public class Contact : AuditableEntity
{
    public const int NameMax = 200;
    public const int EmailMax = 320;
    public const int PhoneMax = 64;
    public const int StreetMax = 200;
    public const int PostalCodeMax = 20;
    public const int CityMax = 100;
    public const int CountryMax = 100;

    public string? Name { get; set; }

    /// <summary>Trimmed and lower-case; unique. First match key of an import.</summary>
    public string? Email { get; set; }

    /// <summary>As written in the last file that changed it.</summary>
    public string? Phone { get; set; }

    /// <summary>E.164; unique. Second match key of an import.</summary>
    public string? PhoneE164 { get; set; }

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public DateOnly? ObtainedOn { get; set; }

    public bool EmailOptOut { get; set; }

    public bool SmsOptOut { get; set; }

    public bool PostOptOut { get; set; }

    /// <summary>Lower-case text of every searchable field, behind the trigram index. Call <see cref="RefreshSearchText"/> after changes.</summary>
    public string SearchText { get; private set; } = string.Empty;

    public ICollection<ContactImport> Imports { get; set; } = [];

    public void RefreshSearchText()
    {
        var digits = Phone is null ? null : new string(Phone.Where(char.IsAsciiDigit).ToArray());
        string?[] parts = [Name, Email, Phone, PhoneE164, digits, Street, PostalCode, City, Country];
        SearchText = string.Join(' ', parts.Where(p => !string.IsNullOrEmpty(p))).ToLowerInvariant();
    }
}

public enum ContactChange
{
    Created,
    Updated,
    Unchanged,
}

/// <summary>One CSV row applied to one contact: which file touched the contact and what it changed.</summary>
public class ContactImport : IEntity<Guid>
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ContactId { get; set; }

    public Contact? Contact { get; set; }

    public Guid ImportBatchId { get; set; }

    public ImportBatch? ImportBatch { get; set; }

    public int RowNumber { get; set; }

    public ContactChange Change { get; set; }

    public List<FieldChange> Changes { get; set; } = [];
}

public class FieldChange
{
    public required string Field { get; set; }

    public string? Old { get; set; }

    public string? New { get; set; }
}
