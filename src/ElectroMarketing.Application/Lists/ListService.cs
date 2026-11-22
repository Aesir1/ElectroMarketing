using System.ComponentModel.DataAnnotations;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Crud;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Domain.Exceptions;
using ElectroMarketing.Domain.Lists;
using DomainValidationException = ElectroMarketing.Domain.Exceptions.ValidationException;

namespace ElectroMarketing.Application.Lists;

public sealed record ListNameDto([property: Required, StringLength(200, MinimumLength = 1)] string Name);

public interface IListStore
{
    /// <summary>Adds the contacts matching <paramref name="filter"/> that are not members yet; returns how many were added.</summary>
    Task<int> AddMembersAsync(Guid listId, ContactFilter filter, CancellationToken ct);

    /// <summary>Adds the existing contacts among <paramref name="contactIds"/> that are not members yet.</summary>
    Task<int> AddMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct);

    Task<int> RemoveMembersAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct);
}

public sealed class ListService(IRepository<ContactList, Guid> lists, IRepository<ImportBatch, Guid> batches, IListStore store)
    : CrudService<ContactList, Guid, ListNameDto, ListNameDto>(lists)
{
    public async Task<ContactList> CreateFromBatchAsync(Guid batchId, string name, CancellationToken ct = default)
    {
        var dto = new ListNameDto(name);
        DtoValidator.Validate(dto);
        _ = await batches.GetByIdAsync(batchId, ct) ?? throw NotFoundException.For<ImportBatch>(batchId);
        var list = Map(dto);
        list.SourceBatchId = batchId;
        list = await Repository.AddAsync(list, ct);
        await store.AddMembersAsync(list.Id, new ContactFilter(BatchIds: [batchId]), ct);
        return list;
    }

    /// <summary>Pass either <paramref name="contactIds"/> (hand-picked) or <paramref name="filter"/> ("add all matching").</summary>
    public async Task<int> AddAsync(Guid listId, IReadOnlyCollection<Guid>? contactIds, ContactFilter? filter, CancellationToken ct = default)
    {
        if ((contactIds is null) == (filter is null))
        {
            throw new DomainValidationException(new Dictionary<string, string[]> { ["contactIds"] = ["Pass either contactIds or filter."] });
        }

        await RequireAsync(listId, ct);
        return filter is not null ? await store.AddMembersAsync(listId, filter, ct) : await store.AddMembersAsync(listId, contactIds!, ct);
    }

    public async Task<int> RemoveAsync(Guid listId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default)
    {
        await RequireAsync(listId, ct);
        return await store.RemoveMembersAsync(listId, contactIds, ct);
    }

    protected override ContactList Map(ListNameDto dto) => new() { Name = dto.Name.Trim() };

    protected override void Apply(ListNameDto dto, ContactList entity) => entity.Name = dto.Name.Trim();

    private async Task RequireAsync(Guid listId, CancellationToken ct)
    {
        if (!await Repository.ExistsAsync(listId, ct))
        {
            throw NotFoundException.For<ContactList>(listId);
        }
    }
}
