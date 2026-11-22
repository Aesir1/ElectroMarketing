using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class ContactSearchTests(PostgresFixture db) : IAsyncLifetime
{
    private static readonly Category Trade = new() { Name = "Trade", NormalizedName = "TRADE" };
    private static readonly Category Web = new() { Name = "Web", NormalizedName = "WEB" };
    private ImportBatch first = null!;
    private ImportBatch second = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        var document = new Document { OwnerId = Guid.CreateVersion7(), FileName = "f.csv", ContentType = "text/csv" };
        first = Batch(Trade.Id, document.Id);
        second = Batch(Web.Id, document.Id);
        await using var ctx = db.Create();
        ctx.AddRange(Trade, Web, document, first, second);
        Add(ctx, first, "Jörg Müller", "j@x.de", "0170 1234567", "+491701234567", "Köln", new DateOnly(2026, 3, 1));
        Add(ctx, second, "Eva Schmidt", "eva@y.com", null, null, "Bonn", new DateOnly(2026, 5, 1));
        Add(ctx, second, "Max 50% Mustermann", "max@x.de", null, null, "Köln", null);
        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ImportBatch Batch(Guid categoryId, Guid documentId) =>
        new() { CategoryId = categoryId, DocumentId = documentId, FileName = "f.csv", Culture = "de-DE", Mapping = new ColumnMapping() };

    private static void Add(AppDbContext ctx, ImportBatch batch, string name, string email, string? phone, string? e164, string city, DateOnly? on)
    {
        var contact = new Contact { Name = name, Email = email, Phone = phone, PhoneE164 = e164, City = city, ObtainedOn = on };
        contact.RefreshSearchText();
        ctx.Contacts.Add(contact);
        ctx.ContactImports.Add(new ContactImport { ContactId = contact.Id, ImportBatchId = batch.Id, RowNumber = 2, Change = ContactChange.Created });
    }

    private async Task<List<string?>> Names(ContactFilter filter)
    {
        await using var ctx = db.Create();
        return await ContactSearch.Apply(ctx.Contacts, filter).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync(Ct);
    }

    [Theory]
    [InlineData("köln", "Jörg Müller|Max 50% Mustermann")]
    [InlineData("KÖLN müller", "Jörg Müller")]
    [InlineData("1234567", "Jörg Müller")]
    [InlineData("0170-1234", "Jörg Müller")]
    [InlineData("+49170", "Jörg Müller")]
    [InlineData("x.de", "Jörg Müller|Max 50% Mustermann")]
    [InlineData("50%", "Max 50% Mustermann")]
    [InlineData("5_%", "")]
    [InlineData("nobody", "")]
    public async Task Every_term_must_match_some_field(string search, string expected) =>
        (await Names(new ContactFilter(Search: search))).ShouldBe(expected.Length == 0 ? [] : expected.Split('|'));

    [Fact]
    public async Task Filters_by_category_batch_and_date()
    {
        (await Names(new ContactFilter(CategoryIds: [Web.Id]))).ShouldBe(["Eva Schmidt", "Max 50% Mustermann"]);
        (await Names(new ContactFilter(BatchIds: [first.Id]))).ShouldBe(["Jörg Müller"]);
        (await Names(new ContactFilter(ObtainedFrom: new DateOnly(2026, 4, 1)))).ShouldBe(["Eva Schmidt"]);
        (await Names(new ContactFilter(Search: "köln", CategoryIds: [Web.Id]))).ShouldBe(["Max 50% Mustermann"]);
    }

    [Fact]
    public void Terms_are_lower_case_and_phone_like_terms_become_digits() =>
        ContactSearch.Terms("  Köln  0170-1234 (030) KÖLN ").ShouldBe(["köln", "01701234", "030"]);
}
