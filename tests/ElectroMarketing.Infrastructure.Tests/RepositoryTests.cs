using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Domain.Abstractions;
using ElectroMarketing.Domain.Documents;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Infrastructure.Tests;

/// <summary>CR-8: Repository&lt;,&gt; CRUD, soft delete, audit and concurrency against real PostgreSQL.</summary>
public sealed class RepositoryTests(PostgresFixture db) : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly ICurrentUser alice = User("alice@local.test");

    public async ValueTask InitializeAsync() => await db.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ICurrentUser User(string email)
    {
        var user = Substitute.For<ICurrentUser>();
        user.Email.Returns(email);
        user.Id.Returns(Guid.CreateVersion7());
        return user;
    }

    private static Document NewDocument(string name = "a.pdf") => new() { FileName = name, ContentType = "application/pdf", OwnerId = Guid.CreateVersion7() };

    private async Task<Document> Seed(Document product)
    {
        await using var ctx = db.Create(alice);
        return await new Repository<Document, Guid>(ctx).AddAsync(product, Ct);
    }

    [Fact]
    public async Task Add_sets_audit_fields_and_row_version()
    {
        var product = await Seed(NewDocument());

        product.CreatedBy.ShouldBe("alice@local.test");
        product.CreatedAtUtc.ShouldBe(db.Time.GetUtcNow());
        product.RowVersion.ShouldNotBe(0u);
        product.UpdatedAtUtc.ShouldBeNull();
    }

    [Fact]
    public async Task Get_by_id_returns_null_for_missing_row()
    {
        await using var ctx = db.Create();
        (await new Repository<Document, Guid>(ctx).GetByIdAsync(Guid.CreateVersion7(), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task List_and_exists()
    {
        var a = await Seed(NewDocument("A.pdf"));
        await Seed(NewDocument("B.pdf"));

        await using var ctx = db.Create();
        var repo = new Repository<Document, Guid>(ctx);
        (await repo.ListAsync(ct: Ct)).Count.ShouldBe(2);
        (await repo.ListAsync(p => p.FileName == "A.pdf", Ct)).Single().Id.ShouldBe(a.Id);
        (await repo.ExistsAsync(a.Id, Ct)).ShouldBeTrue();
        (await repo.ExistsAsync(Guid.CreateVersion7(), Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Update_sets_updated_audit_and_keeps_created()
    {
        var product = await Seed(NewDocument());
        db.Time.Advance(TimeSpan.FromMinutes(5));

        await using var ctx = db.Create(User("bob@local.test"));
        var repo = new Repository<Document, Guid>(ctx);
        var loaded = (await repo.GetByIdAsync(product.Id, Ct))!;
        loaded.FileName = "Renamed";
        await repo.UpdateAsync(loaded, Ct);

        await using var check = db.Create();
        var saved = await check.Documents.SingleAsync(Ct);
        saved.FileName.ShouldBe("Renamed");
        saved.CreatedBy.ShouldBe("alice@local.test");
        saved.UpdatedBy.ShouldBe("bob@local.test");
        saved.UpdatedAtUtc.ShouldBe(db.Time.GetUtcNow());
        saved.RowVersion.ShouldNotBe(product.RowVersion);
    }

    [Fact]
    public async Task Update_with_stale_row_version_throws_concurrency()
    {
        var product = await Seed(NewDocument());

        await using (var first = db.Create())
        {
            var repo = new Repository<Document, Guid>(first);
            var loaded = (await repo.GetByIdAsync(product.Id, Ct))!;
            loaded.FileName = "First";
            await repo.UpdateAsync(loaded, Ct);
        }

        await using var second = db.Create();
        var secondRepo = new Repository<Document, Guid>(second);
        var stale = (await secondRepo.GetByIdAsync(product.Id, Ct))!;
        stale.FileName = "Second";
        stale.RowVersion = product.RowVersion; // the version this client read before "First" was saved

        await Should.ThrowAsync<ConcurrencyException>(() => secondRepo.UpdateAsync(stale, Ct));
    }

    [Fact]
    public async Task Update_of_detached_missing_row_throws_not_found()
    {
        await using var ctx = db.Create();
        await Should.ThrowAsync<NotFoundException>(() => new Repository<Document, Guid>(ctx).UpdateAsync(NewDocument(), Ct));
    }

    [Fact]
    public async Task Delete_of_missing_row_throws_not_found()
    {
        await using var ctx = db.Create();
        await Should.ThrowAsync<NotFoundException>(() => new Repository<Document, Guid>(ctx).DeleteAsync(Guid.CreateVersion7(), Ct));
    }

    [Fact]
    public async Task Delete_soft_deletes_and_global_filter_hides_row()
    {
        var product = await Seed(NewDocument());

        await using (var ctx = db.Create())
        {
            await new Repository<Document, Guid>(ctx).DeleteAsync(product.Id, Ct);
        }

        await using var check = db.Create();
        (await check.Documents.CountAsync(Ct)).ShouldBe(0);
        var raw = await check.Documents.IgnoreQueryFilters().SingleAsync(Ct);
        raw.IsDeleted.ShouldBeTrue();
        raw.DeletedAtUtc.ShouldNotBeNull();
        await using var fresh = db.Create();
        (await new Repository<Document, Guid>(fresh).GetByIdAsync(product.Id, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task Entities_without_events_write_no_outbox_rows()
    {
        await using var ctx = db.Create();
        await new Repository<Document, Guid>(ctx).AddAsync(new Document { FileName = "a.pdf", ContentType = "application/pdf", OwnerId = Guid.CreateVersion7() }, Ct);

        (await ctx.OutboxMessages.CountAsync(Ct)).ShouldBe(0);
    }

    [Theory]
    [InlineData(EntityState.Added, false, ChangeType.Created)]
    [InlineData(EntityState.Modified, false, ChangeType.Updated)]
    [InlineData(EntityState.Modified, true, ChangeType.Deleted)]
    [InlineData(EntityState.Deleted, false, ChangeType.Deleted)]
    public void Outbox_change_type(EntityState state, bool softDeleteTurnedOn, ChangeType expected) =>
        Persistence.Interceptors.OutboxInterceptor.ChangeOf(state, NewDocument(), softDeleteTurnedOn).ShouldBe(expected);

    [Fact]
    public void Edits_to_an_already_deleted_row_publish_nothing() =>
        Persistence.Interceptors.OutboxInterceptor.ChangeOf(EntityState.Modified, new Document { FileName = "x", ContentType = "x", IsDeleted = true }, false).ShouldBeNull();
}
