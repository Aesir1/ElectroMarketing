using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Contacts;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Contacts;

[ExtendObjectType(typeof(RootQuery))]
public class ContactQueries : RootQuery
{
    [Authorize(Policy = Permissions.Contacts.Read)]
    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Contact> GetContact(Guid id, [Service] IRepository<Contact, Guid> contacts) => contacts.Query().Where(c => c.Id == id);

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
