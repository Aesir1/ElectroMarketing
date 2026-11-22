using System.Text;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Documents;
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

/// <summary>The whole import against PostgreSQL with the CSV served by a fake object storage.</summary>
public sealed class ImportServiceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly IObjectStorage storage = Substitute.For<IObjectStorage>();
    private readonly Category category = new() { Name = "Fair", NormalizedName = "FAIR" };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        await using var ctx = db.Create();
        ctx.Categories.Add(category);
        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private ImportService Service(AppDbContext ctx) => new(
        new ImportStore(ctx),
        new DocumentService(new Repository<Document, Guid>(ctx), storage, Substitute.For<ICurrentUser>()),
        new Repository<Document, Guid>(ctx),
        new Repository<Category, Guid>(ctx),
        new Repository<ImportBatch, Guid>(ctx),
        storage,
        db.Time);

    /// <summary>Stores a document row and a pending batch for <paramref name="csv"/>, then runs the import.</summary>
    private async Task<ImportBatch> ImportAsync(string csv)
    {
        var document = new Document { OwnerId = Guid.CreateVersion7(), FileName = "f.csv", ContentType = "text/csv", Status = DocumentStatus.Available };
        storage.GetAsync(Document.Bucket, document.ObjectKey, Arg.Any<CancellationToken>())
            .Returns(_ => new StoredObject(new MemoryStream(Encoding.UTF8.GetBytes(csv)), "text/csv", csv.Length));
        var batch = new ImportBatch
        {
            CategoryId = category.Id,
            DocumentId = document.Id,
            FileName = "f.csv",
            Culture = "de-DE",
            Mapping = CsvContactFile.SuggestMapping(csv.Split('\n')[0].Split(';')),
        };
        await using (var ctx = db.Create())
        {
            ctx.AddRange(document, batch);
            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Service(ctx).RunAsync(batch.Id, Ct);
        }

        await using var read = db.Create();
        return await read.ImportBatches.SingleAsync(b => b.Id == batch.Id, Ct);
    }

    [Fact]
    public async Task Creates_then_updates_with_history_per_file()
    {
        var first = await ImportAsync("Name;E-Mail;Telefon;Ort\nMax;max@x.de;0170 1234567;Köln\nEva;eva@x.de;;Bonn\n;;;Nowhere\n");
        (first.Status, first.RowCount, first.CreatedCount, first.SkippedCount).ShouldBe((ImportStatus.Done, 3, 2, 1));
        first.Errors.Single().Row.ShouldBe(4);

        // Max matched by phone (new e-mail), Eva matched by e-mail and moves; unchanged row still recorded.
        var second = await ImportAsync("Name;E-Mail;Telefon;Ort\nMax;max.new@x.de;+49 170 1234567;Köln\nEva;EVA@x.de;;Berlin\nMax;max.new@x.de;;\n");
        (second.CreatedCount, second.UpdatedCount, second.UnchangedCount).ShouldBe((0, 2, 1));

        await using var ctx = db.Create();
        var max = await ctx.Contacts.Include(c => c.Imports).SingleAsync(c => c.PhoneE164 == "+491701234567", Ct);
        max.Email.ShouldBe("max.new@x.de");
        max.Imports.Select(i => i.ImportBatchId).ShouldBe([first.Id, second.Id, second.Id], ignoreOrder: true);
        max.Imports.Single(i => i.ImportBatchId == second.Id && i.Change == ContactChange.Updated)
            .Changes.Single().ShouldSatisfyAllConditions(c => c.Field.ShouldBe("Email"), c => c.Old.ShouldBe("max@x.de"));
        (await ctx.Contacts.SingleAsync(c => c.Email == "eva@x.de", Ct)).City.ShouldBe("Berlin");
    }

    [Fact]
    public async Task Email_match_wins_and_foreign_phone_is_not_taken()
    {
        await ImportAsync("E-Mail;Telefon\na@x.de;0170 1111111\nb@x.de;0170 2222222\n");

        var batch = await ImportAsync("E-Mail;Telefon;Ort\na@x.de;0170 2222222;Köln\n");

        batch.Status.ShouldBe(ImportStatus.Done);
        batch.Errors.Single().Message.ShouldBe("Phone +491702222222 belongs to another contact; phone not changed.");
        await using var ctx = db.Create();
        var a = await ctx.Contacts.SingleAsync(c => c.Email == "a@x.de", Ct);
        (a.PhoneE164, a.City).ShouldBe(("+491701111111", "Köln"));
    }

    [Fact]
    public async Task Redelivery_of_a_finished_batch_does_nothing_and_running_batch_fails()
    {
        var done = await ImportAsync("E-Mail\na@x.de\n");
        await using (var ctx = db.Create())
        {
            await Service(ctx).RunAsync(done.Id, Ct);
            (await ctx.ContactImports.CountAsync(Ct)).ShouldBe(1);
            var batch = await ctx.ImportBatches.SingleAsync(Ct);
            batch.Status = ImportStatus.Running;
            await ctx.SaveChangesAsync(Ct);
        }

        await using (var ctx = db.Create())
        {
            await Service(ctx).RunAsync(done.Id, Ct);
        }

        await using var read = db.Create();
        var failed = await read.ImportBatches.SingleAsync(Ct);
        failed.Status.ShouldBe(ImportStatus.Failed);
        (await read.ContactImports.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Key_freed_by_one_row_is_taken_by_a_new_contact_in_the_same_chunk()
    {
        await ImportAsync("E-Mail;Telefon\ne1@x.de;0170 1111111\n");

        var batch = await ImportAsync("E-Mail;Telefon\ne2@x.de;0170 1111111\ne1@x.de;0170 2222222\n");

        batch.Status.ShouldBe(ImportStatus.Done);
        (batch.CreatedCount, batch.UpdatedCount).ShouldBe((1, 1));
        await using var ctx = db.Create();
        (await ctx.Contacts.SingleAsync(c => c.PhoneE164 == "+491701111111", Ct)).Email.ShouldBe("e2@x.de");
        (await ctx.Contacts.SingleAsync(c => c.Email == "e1@x.de", Ct)).PhoneE164.ShouldBe("+491702222222");
    }

    [Fact]
    public async Task Contact_created_by_an_earlier_row_is_matched_by_a_later_row_in_the_same_chunk()
    {
        var batch = await ImportAsync("E-Mail;Name\na@x.de;A\na@x.de;B\n");

        batch.Status.ShouldBe(ImportStatus.Done);
        await using var ctx = db.Create();
        var contact = await ctx.Contacts.Include(c => c.Imports).SingleAsync(Ct);
        contact.Imports.OrderBy(i => i.RowNumber).Select(i => i.Change).ShouldBe([ContactChange.Created, ContactChange.Updated]);
    }
}
