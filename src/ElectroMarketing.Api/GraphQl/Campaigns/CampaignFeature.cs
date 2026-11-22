using ElectroMarketing.Api.GraphQl.Errors;
using ElectroMarketing.Application.Abstractions;
using ElectroMarketing.Application.Campaigns;
using ElectroMarketing.Auth;
using ElectroMarketing.Domain.Campaigns;
using HotChocolate.Authorization;
using HotChocolate.Data.Filters;
using HotChocolate.Language;
using HotChocolate.Resolvers;

namespace ElectroMarketing.Api.GraphQl.Campaigns;

[ExtendObjectType(typeof(RootQuery))]
public class CampaignQueries : RootQuery
{
    [Authorize(Policy = Permissions.Campaigns.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Campaign> GetCampaigns(IResolverContext context, [Service] IRepository<Campaign, Guid> campaigns)
    {
        var query = campaigns.Query();

        // Default order only when the client sent none: [UseSorting] would append its order after ours.
        return context.ArgumentLiteral<IValueNode>("order") is ListValueNode { Items.Count: > 0 }
            ? query
            : query.OrderByDescending(c => c.CreatedAtUtc).ThenBy(c => c.Id);
    }

    [Authorize(Policy = Permissions.Campaigns.Read)]
    [UseFirstOrDefault]
    [UseProjection]
    public IQueryable<Campaign> GetCampaign(Guid id, [Service] IRepository<Campaign, Guid> campaigns) => campaigns.Query().Where(c => c.Id == id);

    [Authorize(Policy = Permissions.Campaigns.Read)]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering<CampaignMessageFilterType>]
    [UseSorting]
    public IQueryable<CampaignMessage> GetCampaignMessages(IResolverContext context, Guid campaignId, [Service] IRepository<CampaignMessage, Guid> messages)
    {
        var query = messages.Query().Where(m => m.CampaignId == campaignId);
        return context.ArgumentLiteral<IValueNode>("order") is ListValueNode { Items.Count: > 0 } ? query : query.OrderBy(m => m.Id);
    }
}

[ExtendObjectType(typeof(RootMutation))]
public class CampaignMutations : RootMutation
{
    [Authorize(Policy = Permissions.Campaigns.Write)]
    [Error<ValidationError>]
    public Task<Campaign> CreateCampaign(CampaignDto campaign, [Service] CampaignService campaigns, CancellationToken ct) =>
        campaigns.CreateAsync(campaign, ct);

    [Authorize(Policy = Permissions.Campaigns.Write)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    [Error<ConflictError>]
    [Error<ConcurrencyError>]
    public Task<Campaign> UpdateCampaign(Guid id, CampaignDto campaign, [Service] CampaignService campaigns, CancellationToken ct) =>
        campaigns.UpdateAsync(id, campaign, ct);

    [Authorize(Policy = Permissions.Campaigns.Write)]
    [Error<NotFoundError>]
    [Error<ConflictError>]
    public async Task<Guid> DeleteCampaign(Guid id, [Service] CampaignService campaigns, CancellationToken ct)
    {
        await campaigns.DeleteAsync(id, ct);
        return id;
    }

    /// <summary>Snapshots the list and queues the messages; the dispatcher sends them in the background.</summary>
    [Authorize(Policy = Permissions.Campaigns.Send)]
    [Error<NotFoundError>]
    [Error<ValidationError>]
    [Error<ConflictError>]
    [Error<ConcurrencyError>]
    public Task<Campaign> StartCampaign(Guid id, [Service] CampaignService campaigns, CancellationToken ct) => campaigns.StartAsync(id, ct);
}

public sealed class CampaignType : ObjectType<Campaign>
{
    protected override void Configure(IObjectTypeDescriptor<Campaign> descriptor)
    {
        descriptor.Ignore(c => c.Channels());
        descriptor.Field(c => c.RowVersion).Type<NonNullType<UnsignedIntType>>();
    }
}

/// <summary>Without this, filtering on the nested Contact would generate a ContactFilterInput that clashes with the contact search input.</summary>
public sealed class CampaignMessageFilterType : FilterInputType<CampaignMessage>
{
    protected override void Configure(IFilterInputTypeDescriptor<CampaignMessage> descriptor) => descriptor.Ignore(m => m.Contact);
}

/// <summary>The message exposes its contact, so reading it also needs contacts.read.</summary>
public sealed class CampaignMessageType : ObjectType<CampaignMessage>
{
    protected override void Configure(IObjectTypeDescriptor<CampaignMessage> descriptor) =>
        descriptor.Field(m => m.Contact).Authorize(Permissions.Contacts.Read);
}
