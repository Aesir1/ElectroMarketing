using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Application.Documents;
using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Domain.Exceptions;
using DomainValidationException = ElectroMarketing.Domain.Exceptions.ValidationException;

namespace ElectroMarketing.Application.Imports;

public sealed record StartImportDto(
    Guid DocumentId,
    Guid CategoryId,
    [property: Required] ColumnMapping Mapping,
    [property: Required, StringLength(16)] string Culture);

/// <summary>Persistence for <see cref="ImportService.RunAsync"/>: one DbContext, the batch stays tracked, rows are detached per chunk.</summary>
public interface IImportStore
{
    Task<ImportBatch?> GetBatchAsync(Guid id, CancellationToken ct);

    /// <summary>Tracked contacts whose e-mail or E.164 phone is in the given sets.</summary>
    Task<IReadOnlyList<Contact>> FindContactsAsync(IReadOnlyCollection<string> emails, IReadOnlyCollection<string> phones, CancellationToken ct);

    void Add(Contact contact);

    void Add(ContactImport import);

    /// <summary>Saves all changes, then detaches everything except the batch so memory stays flat across chunks.</summary>
    Task SaveAsync(CancellationToken ct);

    /// <summary>Drops unsaved contact and history changes after a failed chunk.</summary>
    void DiscardChanges();
}

public sealed class ImportService(
    IImportStore store,
    DocumentService documents,
    IRepository<Document, Guid> documentRows,
    IRepository<Category, Guid> categories,
    IRepository<ImportBatch, Guid> batches,
    IObjectStorage storage,
    TimeProvider time)
{
    public const int ChunkSize = 1000;

    public async Task<CsvPreview> PreviewAsync(Guid documentId, CancellationToken ct = default) =>
        (await OpenAsync(documentId, ct)).File.Preview();

    /// <summary>Validates and stores a Pending batch; the outbox event starts <see cref="RunAsync"/> in the consumer.</summary>
    public async Task<ImportBatch> StartAsync(StartImportDto dto, CancellationToken ct = default)
    {
        DtoValidator.Validate(dto);
        var culture = ParseCulture(dto.Culture);
        var (document, file) = await OpenAsync(dto.DocumentId, ct);
        ValidateMapping(dto.Mapping, file.Headers);
        _ = await categories.GetByIdAsync(dto.CategoryId, ct) ?? throw NotFoundException.For<Category>(dto.CategoryId);
        return await batches.AddAsync(
            new ImportBatch { CategoryId = dto.CategoryId, DocumentId = document.Id, FileName = document.FileName, Culture = culture.Name, Mapping = dto.Mapping },
            ct);
    }

    /// <summary>Imports a Pending batch. Redelivered messages are no-ops; an interrupted (Running) batch is failed, not re-applied.</summary>
    public async Task RunAsync(Guid batchId, CancellationToken ct = default)
    {
        var batch = await store.GetBatchAsync(batchId, ct) ?? throw NotFoundException.For<ImportBatch>(batchId);
        if (batch.Status != ImportStatus.Pending)
        {
            if (batch.Status == ImportStatus.Running)
            {
                batch.Status = ImportStatus.Failed;
                batch.ErrorCount++;
                batch.Errors.Add(new ImportRowError { Row = 0, Message = "The import was interrupted. Import the file again; unchanged rows are detected." });
                batch.FinishedAtUtc = time.GetUtcNow();
                await store.SaveAsync(ct);
            }

            return;
        }

        batch.Status = ImportStatus.Running;
        await store.SaveAsync(ct);

        var log = new ImportLog();
        try
        {
            var document = await documentRows.GetByIdAsync(batch.DocumentId, ct) ?? throw NotFoundException.For<Document>(batch.DocumentId);
            await using var content = await storage.GetAsync(Document.Bucket, document.ObjectKey, ct)
                ?? throw NotFoundException.For<Document>(batch.DocumentId);
            var file = await CsvContactFile.ReadAsync(content.Content, ct);
            var culture = CultureInfo.GetCultureInfo(batch.Culture);
            foreach (var chunk in file.Rows(batch.Mapping).Chunk(ChunkSize))
            {
                await ImportChunkAsync(batch.Id, chunk, culture, log, ct);
            }

            batch.Status = ImportStatus.Done;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            store.DiscardChanges();
            batch.Status = ImportStatus.Failed;
            log.Error(0, ex is DomainException ? ex.Message : $"Import failed: {ex.Message}");
        }

        log.CopyTo(batch);
        batch.FinishedAtUtc = time.GetUtcNow();
        await store.SaveAsync(ct);
    }

    private async Task ImportChunkAsync(Guid batchId, RawRow[] chunk, CultureInfo culture, ImportLog log, CancellationToken ct)
    {
        var rows = new List<ContactRow>(chunk.Length);
        foreach (var raw in chunk)
        {
            log.Rows++;
            var parsed = ContactNormalizer.Parse(raw, culture);
            foreach (var warning in parsed.Warnings)
            {
                log.Error(raw.RowNumber, warning);
            }

            if (parsed.Row is null)
            {
                log.Skipped++;
                log.Error(raw.RowNumber, parsed.SkipReason!);
                continue;
            }

            rows.Add(parsed.Row);
        }

        var existing = await store.FindContactsAsync(
            rows.Select(r => r.Email).OfType<string>().ToHashSet(),
            rows.Select(r => r.PhoneE164).OfType<string>().ToHashSet(),
            ct);
        var byEmail = existing.Where(c => c.Email is not null).ToDictionary(c => c.Email!);
        var byPhone = existing.Where(c => c.PhoneE164 is not null).ToDictionary(c => c.PhoneE164!);

        foreach (var row in rows)
        {
            // Match key 1: e-mail; match key 2: phone.
            var contact = (row.Email is null ? null : byEmail.GetValueOrDefault(row.Email))
                ?? (row.PhoneE164 is null ? null : byPhone.GetValueOrDefault(row.PhoneE164));
            var created = contact is null;
            if (contact is null)
            {
                contact = new Contact();
                store.Add(contact);
            }

            var effective = row;
            if (row.PhoneE164 is not null && byPhone.TryGetValue(row.PhoneE164, out var owner) && owner != contact)
            {
                log.Error(row.RowNumber, $"Phone {row.PhoneE164} belongs to another contact; phone not changed.");
                effective = row with { Phone = null, PhoneE164 = null };
            }

            var (oldEmail, oldPhone) = (contact.Email, contact.PhoneE164);
            var changes = ContactMerger.Apply(contact, effective);
            Reindex(byEmail, oldEmail, contact.Email, contact);
            Reindex(byPhone, oldPhone, contact.PhoneE164, contact);

            var change = ContactChange.Unchanged;
            if (created)
            {
                change = ContactChange.Created;
            }
            else if (changes.Count > 0)
            {
                change = ContactChange.Updated;
            }

            log.Count(change);
            store.Add(new ContactImport { ContactId = contact.Id, ImportBatchId = batchId, RowNumber = row.RowNumber, Change = change, Changes = changes });
        }

        await store.SaveAsync(ct);
    }

    private static void Reindex(Dictionary<string, Contact> index, string? oldKey, string? newKey, Contact contact)
    {
        if (oldKey is not null && oldKey != newKey)
        {
            index.Remove(oldKey);
        }

        if (newKey is not null)
        {
            index[newKey] = contact;
        }
    }

    private async Task<(Document Document, CsvContactFile File)> OpenAsync(Guid documentId, CancellationToken ct)
    {
        var (document, content) = await documents.OpenContentAsync(documentId, ct);
        await using (content)
        {
            return (document, await CsvContactFile.ReadAsync(content.Content, ct));
        }
    }

    private static CultureInfo ParseCulture(string name)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(name);
            if (!culture.IsNeutralCulture && culture.Name.Length > 0)
            {
                return culture;
            }
        }
        catch (CultureNotFoundException)
        {
            // falls through to the validation error
        }

        throw Invalid(nameof(StartImportDto.Culture), "Use a specific culture such as de-DE or en-US.");
    }

    private static void ValidateMapping(ColumnMapping mapping, IReadOnlyList<string> headers)
    {
        if (mapping.Email is null && mapping.Phone is null)
        {
            throw Invalid(nameof(StartImportDto.Mapping), "Map at least the e-mail or the phone column.");
        }

        string?[] mapped = [mapping.Name, mapping.Email, mapping.Phone, mapping.Street, mapping.PostalCode, mapping.City, mapping.Country, mapping.ObtainedOn];
        var missing = mapped.OfType<string>().Where(m => !headers.Contains(m, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            throw Invalid(nameof(StartImportDto.Mapping), $"Columns not in the file: {string.Join(", ", missing)}.");
        }
    }

    private static DomainValidationException Invalid(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private sealed class ImportLog
    {
        private readonly List<ImportRowError> errors = [];
        private int created;
        private int updated;
        private int unchanged;
        private int errorCount;

        public int Rows { get; set; }

        public int Skipped { get; set; }

        public void Error(int row, string message)
        {
            errorCount++;
            if (errors.Count < ImportBatch.MaxStoredErrors)
            {
                errors.Add(new ImportRowError { Row = row, Message = message });
            }
        }

        public void Count(ContactChange change)
        {
            _ = change switch
            {
                ContactChange.Created => created++,
                ContactChange.Updated => updated++,
                _ => unchanged++,
            };
        }

        public void CopyTo(ImportBatch batch)
        {
            (batch.RowCount, batch.CreatedCount, batch.UpdatedCount, batch.UnchangedCount, batch.SkippedCount) = (Rows, created, updated, unchanged, Skipped);
            batch.ErrorCount += errorCount;
            batch.Errors.AddRange(errors.Take(ImportBatch.MaxStoredErrors - batch.Errors.Count));
        }
    }
}

/// <summary>Runs the import for every new batch (routing key <c>import.created</c>).</summary>
public sealed class ImportRequestedHandler(ImportService imports) : IMessageHandler<ImportBatchChanged>
{
    public Task HandleAsync(ImportBatchChanged message, MessageContext context, CancellationToken ct) =>
        message.Change == ChangeType.Created ? imports.RunAsync(message.BatchId, ct) : Task.CompletedTask;
}
