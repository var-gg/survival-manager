using System.Collections.Generic;

namespace SM.Meta.Model;

public sealed record SiteEventResolutionState(
    ActiveRunState Run,
    int Echo,
    IReadOnlyDictionary<string, int> HeroExperienceById,
    int RecruitOffersGrantedAtSite,
    int RecruitOffersPerSiteMax,
    int ExtractBonusEcho,
    IReadOnlyList<SiteEventItemGrant> GrantedItems,
    IReadOnlyList<string> GrantedConsumableIds,
    IReadOnlyList<SiteEventRecruitOffer> GrantedRecruitOffers,
    string SelectedRouteNodeId,
    IReadOnlyList<string> LegalRouteNodeIds);

public sealed record SiteEventOutcomeApplication(
    bool IsSuccess,
    string Error,
    SiteEventResolutionState State,
    IReadOnlyList<string> AffectedHeroIds)
{
    public static SiteEventOutcomeApplication Success(
        SiteEventResolutionState state,
        IReadOnlyList<string> affectedHeroIds) =>
        new(true, string.Empty, state, affectedHeroIds);

    public static SiteEventOutcomeApplication Fail(
        SiteEventResolutionState original,
        string error) =>
        new(false, error, original, System.Array.Empty<string>());
}
