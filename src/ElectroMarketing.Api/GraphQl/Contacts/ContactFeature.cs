using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Contacts;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Contacts;
using ElectroMarketing.Infrastructure.Persistence;
using HotChocolate.Authorization;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using Microsoft.EntityFrameworkCore;

namespace ElectroMarketing.Api.GraphQl.Contacts;

[ExtendObjectType(typeof(RootQuery))]
public class ContactQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Contact> GetContact(Guid id, [Service] IRepository<Contact, Guid> contacts) => contacts.Query().Where(c => c.Id == id);

    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseSorting]
    public IQueryable<Contact> GetContacts(ContactFilter? filter, IResolverContext context, [Service] AppDbContext db)
    {
        var query = ContactSearch.Apply(db.Contacts.AsNoTracking(), filter ?? new ContactFilter());

        // Default order only when the client sent none: [UseSorting] would append its order after ours, making it a mere tie-breaker.
        return context.ArgumentLiteral<IValueNode>("order") is ListValueNode { Items.Count: > 0 }
            ? query
            : query.OrderBy(c => c.Name).ThenBy(c => c.Id);
    }

    /// <summary>Every CSV row applied to the contact, newest first (UUID v7 ids sort by time).</summary>
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UsePaging(MaxPageSize = 100)]
    [UseProjection]
    public IQueryable<ContactImport> GetContactHistory(Guid contactId, [Service] IRepository<ContactImport, Guid> imports) =>
        imports.Query().Where(i => i.ContactId == contactId).OrderByDescending(i => i.Id);
}

public sealed class ContactType : ObjectType<Contact>
{
    protected override void Configure(IObjectTypeDescriptor<Contact> descriptor)
    {
        descriptor.Ignore(c => c.SearchText);
        descriptor.Ignore(c => c.Imports);
        descriptor.Field(c => c.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}
