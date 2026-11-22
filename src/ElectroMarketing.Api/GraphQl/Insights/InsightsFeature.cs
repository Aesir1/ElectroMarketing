using ElectroMarketing.Application.Insights;
using ElectroMarketing.Auth;
using HotChocolate.Authorization;

namespace ElectroMarketing.Api.GraphQl.Insights;

[ExtendObjectType(typeof(RootQuery))]
public class InsightsQueries : RootQuery
{
    [Authorize(Policy = Permissions.Insights.Read)]
    public Task<CampaignInsightsResult?> GetCampaignInsights(Guid campaignId, [Service] InsightsService insights, CancellationToken ct) =>
        insights.ForCampaignAsync(campaignId, ct);

    [Authorize(Policy = Permissions.Insights.Read)]
    public Task<IReadOnlyList<CampaignInsightsResult>> GetRecentCampaignInsights([Service] InsightsService insights, CancellationToken ct, int take = 10) =>
        insights.RecentAsync(take, ct);
}
