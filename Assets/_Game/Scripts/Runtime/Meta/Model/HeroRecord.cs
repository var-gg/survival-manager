using SM.Core.Contracts;

namespace SM.Meta.Model;

public sealed record HeroRecord(
    string Id,
    string Name,
    string ArchetypeId,
    string RaceId,
    string ClassId,
    string PositiveTraitId,
    string NegativeTraitId,
    string FlexActiveId = "",
    string FlexPassiveId = "",
    RecruitTier RecruitTier = RecruitTier.Common,
    RecruitOfferSource RecruitSource = RecruitOfferSource.RecruitPhase,
    UnitRetrainState? RetrainState = null,
    UnitEconomyFootprint? EconomyFootprint = null)
{
    public UnitRetrainState EffectiveRetrainState => RetrainState ?? new UnitRetrainState();

    public UnitEconomyFootprint EffectiveEconomyFootprint => EconomyFootprint ?? new UnitEconomyFootprint();
}
