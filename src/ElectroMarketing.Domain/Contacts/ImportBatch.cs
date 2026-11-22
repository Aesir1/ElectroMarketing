// src/ElectroMarketing.Domain/Contacts/ImportBatch.cs
using ElectroMarketing.Domain.Abstractions;

namespace ElectroMarketing.Domain.Contacts;

public class Category : AuditableEntity
{
    public required string Name { get; set; }

    /// <summary>Upper-case trimmed <see cref="Name"/>; unique, so "Fair 2026" and "fair 2026" are one category.</summary>
    public required string NormalizedName { get; set; }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();
}

public enum ImportStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>One imported CSV file (a "lot"). Adding it publishes <c>import.created</c>, which starts the import consumer.</summary>
public class ImportBatch : AuditableEntity, IPublishesChanges
{
    public const int MaxStoredErrors = 1000;

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    /// <summary>The uploaded CSV; its bytes stay in object storage.</summary>
    public Guid DocumentId { get; set; }

    public required string FileName { get; set; }

    /// <summary>Culture name such as <c>de-DE</c>: date formats and default phone region.</summary>
    public required string Culture { get; set; }

    public required ColumnMapping Mapping { get; set; }

    public ImportStatus Status { get; set; } = ImportStatus.Pending;

    public int RowCount { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int UnchangedCount { get; set; }

    public int SkippedCount { get; set; }

    /// <summary>All warnings and errors; only the first <see cref="MaxStoredErrors"/> are kept in <see cref="Errors"/>.</summary>
    public int ErrorCount { get; set; }

    public List<ImportRowError> Errors { get; set; } = [];

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public string EventName => "import";

    public object ToChangedEvent(ChangeType change) => new ImportBatchChanged(Id, change, Status);
}

public sealed record ImportBatchChanged(Guid BatchId, ChangeType Change, ImportStatus Status);

/// <summary>CSV header name for each contact field; null when the file has no such column.</summary>
public class ColumnMapping
{
    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Street { get; set; }

    public string? PostalCode { get; set; }

    public string? City { get; set; }

    public string? Country { get; set; }

    public string? ObtainedOn { get; set; }
}

/// <summary><see cref="Row"/> is the physical CSV line (header = 1); 0 means the whole file.</summary>
public class ImportRowError
{
    public int Row { get; set; }

    public required string Message { get; set; }
}
