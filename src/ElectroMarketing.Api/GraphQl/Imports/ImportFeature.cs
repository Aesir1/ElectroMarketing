using ElectroMarketing.Api.GraphQl.Errors;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Imports;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Contacts;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Imports;

[ExtendObjectType(typeof(RootQuery))]
public class ImportQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ImportBatch> GetImportBatches([Service] IRepository<ImportBatch, Guid> batches) =>
        batches.Query().OrderByDescending(b => b.CreatedAtUtc);

    [Authorize(Policy = Permissions.Contacts.Read)]
    [UseProjection]
    public IQueryable<Category> GetCategories([Service] IRepository<Category, Guid> categories) => categories.Query().OrderBy(c => c.Name);
}

[ExtendObjectType(typeof(RootMutation))]
public class ImportMutations : RootMutation
{
    [Authorize(Policy = Permissions.Imports.Write)]
    [Error<ValidationError>]
    [Error<ConflictError>]
    public Task<Category> CreateCategory(string name, [Service] CategoryService categories, CancellationToken ct) =>
        categories.CreateAsync(new CreateCategoryDto(name), ct);

    /// <summary>Delimiter, encoding, headers, first 10 rows and a suggested column mapping of an uploaded CSV.</summary>
    [Authorize(Policy = Permissions.Imports.Write)]
    [Error<NotFoundError>]
    public Task<CsvPreview> PreviewImport(Guid documentId, [Service] ImportService imports, CancellationToken ct) =>
        imports.PreviewAsync(documentId, ct);

    /// <summary>Queues the import; poll <c>importBatches</c> for the status.</summary>
    [Authorize(Policy = Permissions.Imports.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<ImportBatch> StartImport(Guid documentId, Guid categoryId, ColumnMapping mapping, string culture, [Service] ImportService imports, CancellationToken ct) =>
        imports.StartAsync(new StartImportDto(documentId, categoryId, mapping, culture), ct);
}

public sealed class ImportBatchType : ObjectType<ImportBatch>
{
    protected override void Configure(IObjectTypeDescriptor<ImportBatch> descriptor)
    {
        descriptor.Ignore(b => b.EventName);
        descriptor.Ignore(b => b.ToChangedEvent(default));
        descriptor.Field(b => b.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}

public sealed class CategoryType : ObjectType<Category>
{
    protected override void Configure(IObjectTypeDescriptor<Category> descriptor)
    {
        descriptor.Ignore(c => c.NormalizedName);
        descriptor.Field(c => c.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}
