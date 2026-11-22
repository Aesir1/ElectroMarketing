// tests/ElectroMarketing.Infrastructure.Tests/ContactPersistenceTests.cs
using ElectroMarketing.Domain.Contacts;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class ContactPersistenceTests(PostgresFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Email_and_phone_are_unique()
    {
        await using (var ctx = db.Create())
        {
            ctx.Contacts.Add(new Contact { Email = "a@x.de", PhoneE164 = "+4917012345" });
            await ctx.SaveChangesAsync(Ct);
        }

        await using var again = db.Create();
        again.Contacts.Add(new Contact { Email = "a@x.de" });
        await Should.ThrowAsync<DbUpdateException>(() => again.SaveChangesAsync(Ct));

        again.ChangeTracker.Clear();
        again.Contacts.Add(new Contact { PhoneE164 = "+4917012345" });
        await Should.ThrowAsync<DbUpdateException>(() => again.SaveChangesAsync(Ct));
    }

    [Fact]
    public async Task Batch_mapping_and_errors_round_trip_as_json()
    {
        var category = new Category { Name = "Fair", NormalizedName = "FAIR" };
        var document = new Domain.Documents.Document { OwnerId = Guid.CreateVersion7(), FileName = "a.csv", ContentType = "text/csv" };
        var batch = new ImportBatch
        {
            CategoryId = category.Id,
            DocumentId = document.Id,
            FileName = "a.csv",
            Culture = "de-DE",
            Mapping = new ColumnMapping { Email = "E-Mail" },
            Errors = [new ImportRowError { Row = 3, Message = "bad" }],
        };
        await using (var ctx = db.Create())
        {
            ctx.AddRange(category, document, batch);
            await ctx.SaveChangesAsync(Ct);
        }

        await using var read = db.Create();
        var loaded = await read.ImportBatches.SingleAsync(Ct);
        loaded.Mapping.Email.ShouldBe("E-Mail");
        loaded.Errors.Single().Row.ShouldBe(3);
    }

    [Fact]
    public async Task Saving_an_import_batch_writes_import_created_to_the_outbox_in_the_same_save()
    {
        var category = new Category { Name = "Fair", NormalizedName = "FAIR" };
        var document = new Domain.Documents.Document { OwnerId = Guid.CreateVersion7(), FileName = "a.csv", ContentType = "text/csv" };
        var batch = new ImportBatch { CategoryId = category.Id, DocumentId = document.Id, FileName = "a.csv", Culture = "de-DE", Mapping = new ColumnMapping() };
        await using (var ctx = db.Create())
        {
            ctx.AddRange(category, document, batch);
            await ctx.SaveChangesAsync(Ct);
        }

        await using var read = db.Create();
        var message = await read.OutboxMessages.SingleAsync(Ct);
        message.RoutingKey.ShouldBe("import.created");
        message.Type.ShouldBe(nameof(ImportBatchChanged));
        message.Payload.ShouldContain(batch.Id.ToString());
    }
}
