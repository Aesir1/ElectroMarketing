using ElectroMarketing.Application.Products;
using ElectroMarketing.Domain.Products;

namespace ElectroMarketing.Infrastructure.Persistence;

/// <summary>Override example: archived products are hidden from every query.</summary>
public sealed class ProductRepository(AppDbContext db) : Repository<Product, Guid>(db), IProductRepository
{
    public override IQueryable<Product> Query() => base.Query().Where(p => !p.IsArchived);
}
