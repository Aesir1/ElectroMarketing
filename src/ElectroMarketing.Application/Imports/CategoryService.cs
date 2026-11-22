using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Domain.Contacts;

namespace ElectroMarketing.Application.Imports;

public sealed record CreateCategoryDto([property: Required, StringLength(100, MinimumLength = 1)] string Name);

/// <summary>A duplicate name (case-insensitive) hits the unique index and surfaces as ConflictError.</summary>
public sealed class CategoryService(IRepository<Category, Guid> categories)
    : CrudService<Category, Guid, CreateCategoryDto, CreateCategoryDto>(categories)
{
    protected override Category Map(CreateCategoryDto dto) => new() { Name = dto.Name.Trim(), NormalizedName = Category.Normalize(dto.Name) };

    protected override void Apply(CreateCategoryDto dto, Category entity)
    {
        entity.Name = dto.Name.Trim();
        entity.NormalizedName = Category.Normalize(dto.Name);
    }
}
