using ElectroMarketing.Api.GraphQl.Errors;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Application.Lists;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Lists;
using ElectroMarketing.Infrastructure.Persistence;
using GreenDonut;
using HotChocolate.Authorization;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.GraphQl.Lists;

[ExtendObjectType(typeof(RootQuery))]
public class ListQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseSorting]
    public IQueryable<ContactList> GetContactLists(IResolverContext context, [Service] IRepository<ContactList, Guid> lists)
    {
        var query = lists.Query();

        // Default order only when the client sent none: [UseSorting] would append its order after ours.
        return context.ArgumentLiteral<IValueNode>("order") is ListValueNode { Items.Count: > 0 }
            ? query
            : query.OrderByDescending(l => l.CreatedAtUtc).ThenBy(l => l.Id);
    }

    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    public IQueryable<ContactListMember> GetContactListMembers(Guid listId, [Service] AppDbContext db) =>
        db.ContactListMembers.AsNoTracking().Where(m => m.ContactListId == listId).OrderBy(m => m.Contact!.Name).ThenBy(m => m.ContactId);
}

[ExtendObjectType(typeof(RootMutation))]
public class ListMutations : RootMutation
{
    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<ValidationError>]
    public Task<ContactList> CreateContactList(string name, [Service] ListService lists, CancellationToken ct) =>
        lists.CreateAsync(new ListNameDto(name), ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<ContactList> CreateContactListFromBatch(Guid batchId, string name, [Service] ListService lists, CancellationToken ct) =>
        lists.CreateFromBatchAsync(batchId, name, ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<ContactList> RenameContactList(Guid id, string name, [Service] ListService lists, CancellationToken ct) =>
        lists.UpdateAsync(id, new ListNameDto(name), ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [Error<NotFoundError>]
    public async Task<Guid> DeleteContactList(Guid id, [Service] ListService lists, CancellationToken ct)
    {
        await lists.DeleteAsync(id, ct);
        return id;
    }

    /// <summary>Pass <c>contactIds</c> (selection) or <c>filter</c> (all matching); returns how many were added.</summary>
    [Authorize(Policy = Permissions.Lists.Write)]
    [UseMutationConvention(PayloadFieldName = "count")]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    public Task<int> AddToContactList(Guid listId, IReadOnlyList<Guid>? contactIds, ContactFilter? filter, [Service] ListService lists, CancellationToken ct) =>
        lists.AddAsync(listId, contactIds, filter, ct);

    [Authorize(Policy = Permissions.Lists.Write)]
    [UseMutationConvention(PayloadFieldName = "count")]
    [Error<NotFoundError>]
    public Task<int> RemoveFromContactList(Guid listId, IReadOnlyList<Guid> contactIds, [Service] ListService lists, CancellationToken ct) =>
        lists.RemoveAsync(listId, contactIds, ct);
}

public sealed class ContactListType : ObjectType<ContactList>
{
    protected override void Configure(IObjectTypeDescriptor<ContactList> descriptor)
    {
        descriptor.Ignore(l => l.Members);
        descriptor.Field(l => l.RowVersion).Type<NonNullType<UnsignedIntType>>();
        descriptor.Field(l => l.Id).IsProjected(true);
        descriptor.Field("memberCount")
            .Type<NonNullType<IntType>>()
            .Resolve(ctx => ctx.DataLoader<IMemberCountByListIdDataLoader>().LoadAsync(ctx.Parent<ContactList>().Id, ctx.RequestAborted));
    }
}

public static class ListDataLoaders
{
    [DataLoader]
    public static async Task<IReadOnlyDictionary<Guid, int>> GetMemberCountByListIdAsync(
        IReadOnlyList<Guid> ids,
        IServiceScopeFactory scopes,
        CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var counts = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ContactListMembers.AsNoTracking()
            .Where(m => ids.Contains(m.ContactListId))
            .GroupBy(m => m.ContactListId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return ids.ToDictionary(id => id, id => counts.GetValueOrDefault(id));
    }
}
