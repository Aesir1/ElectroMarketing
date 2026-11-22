using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

public sealed class ListServiceTests(PostgresFixture db) : IAsyncLifetime
{
    private readonly List<Contact> contacts = [];
    private ImportBatch batch = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await db.ResetAsync();
        var category = new Category { Name = "C", NormalizedName = "C" };
        var document = new Document { OwnerId = Guid.CreateVersion7(), FileName = "f.csv", ContentType = "text/csv" };
        batch = new ImportBatch { CategoryId = category.Id, DocumentId = document.Id, FileName = "f.csv", Culture = "de-DE", Mapping = new ColumnMapping() };
        await using var ctx = db.Create();
        ctx.AddRange(category, document, batch);
        foreach (var (name, city, inBatch) in new[] { ("Anna", "Köln", true), ("Ben", "Köln", true), ("Cem", "Bonn", false) })
        {
            var c = new Contact { Name = name, Email = $"{name.ToLowerInvariant()}@x.de", City = city };
            c.RefreshSearchText();
            contacts.Add(c);
            ctx.Contacts.Add(c);
            if (inBatch)
            {
                ctx.ContactImports.Add(new ContactImport { ContactId = c.Id, ImportBatchId = batch.Id, RowNumber = 2, Change = ContactChange.Created });
            }
        }

        await ctx.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ListService Service(AppDbContext ctx) =>
        new(new Repository<ContactList, Guid>(ctx), new Repository<ImportBatch, Guid>(ctx), new ListStore(ctx, TimeProvider.System));

    private async Task<string[]> Members(Guid listId)
    {
        await using var ctx = db.Create();
        return await ctx.ContactListMembers.Where(m => m.ContactListId == listId).Select(m => m.Contact!.Name!).OrderBy(n => n).ToArrayAsync(Ct);
    }

    [Fact]
    public async Task From_batch_then_add_by_filter_and_ids_without_duplicates()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);

        var list = await service.CreateFromBatchAsync(batch.Id, "Fair leads", Ct);
        (await Members(list.Id)).ShouldBe(["Anna", "Ben"]);

        (await service.AddAsync(list.Id, null, new ContactFilter(Search: "köln"), Ct)).ShouldBe(0);
        (await service.AddAsync(list.Id, [contacts[2].Id, contacts[0].Id], null, Ct)).ShouldBe(1);
        (await Members(list.Id)).ShouldBe(["Anna", "Ben", "Cem"]);

        (await service.RemoveAsync(list.Id, [contacts[1].Id], Ct)).ShouldBe(1);
        (await Members(list.Id)).ShouldBe(["Anna", "Cem"]);
    }

    [Fact]
    public async Task Add_needs_exactly_one_source_and_an_existing_list()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var list = await service.CreateAsync(new ListNameDto("Manual"), Ct);

        await Should.ThrowAsync<ValidationException>(() => service.AddAsync(list.Id, null, null, Ct));
        await Should.ThrowAsync<NotFoundException>(() => service.AddAsync(Guid.CreateVersion7(), [contacts[0].Id], null, Ct));
    }

    [Fact]
    public async Task Deleting_a_list_removes_members_not_contacts()
    {
        await using var ctx = db.Create();
        var service = Service(ctx);
        var list = await service.CreateFromBatchAsync(batch.Id, "Temp", Ct);

        await service.DeleteAsync(list.Id, Ct);

        await using var read = db.Create();
        (await read.ContactListMembers.CountAsync(Ct)).ShouldBe(0);
        (await read.Contacts.CountAsync(Ct)).ShouldBe(3);
    }
}
