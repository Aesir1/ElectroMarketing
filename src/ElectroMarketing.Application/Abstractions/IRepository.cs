using System.Linq.Expressions;
using ElectroMarketing.Domain.Abstractions;

namespace ElectroMarketing.Application.Abstractions;

public interface IRepository<TEntity, in TKey>
    where TEntity : class, IEntity<TKey>
{
    /// <summary>No-tracking query for GraphQL paging, filtering and projection.</summary>
    IQueryable<TEntity> Query();

    /// <summary>Tracked entity, or null when missing.</summary>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default);

    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default);

    /// <summary>Runs a shaped no-tracking query (filter, order, take, select) in SQL, so callers load columns instead of entities.</summary>
    Task<IReadOnlyList<TResult>> SelectAsync<TResult>(Func<IQueryable<TEntity>, IQueryable<TResult>> shape, CancellationToken ct = default);

    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Throws NotFoundException when missing and ConcurrencyException when the row version changed.</summary>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken ct = default);

    /// <summary>Soft delete when <typeparamref name="TEntity"/> is <see cref="ISoftDelete"/>; NotFoundException when missing.</summary>
    Task DeleteAsync(TKey id, CancellationToken ct = default);

    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);
}
