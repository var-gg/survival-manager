using System;

namespace SM.Core.Contracts;

public enum EconomyCurrencyKind
{
    Gold = 0,
    Echo = 1,
}

public enum RecruitTier
{
    Common = 0,
    Rare = 1,
    Epic = 2,
}

public enum RecruitOfferSlotType
{
    StandardA = 0,
    StandardB = 1,
    OnPlan = 2,
    Protected = 3,
}

public enum RecruitOfferSource
{
    RecruitPhase = 0,
    CombatReward = 1,
    EventReward = 2,
    DirectGrant = 3,
}

public enum DuplicateResolutionKind
{
    ConvertToEcho = 0,
}

public enum ScoutDirectiveKind
{
    None = 0,
    Frontline = 1,
    Backline = 2,
    Physical = 3,
    Magical = 4,
    Support = 5,
    SynergyTag = 6,
}

public enum CandidatePlanFit
{
    OffPlan = 0,
    Bridge = 1,
    OnPlan = 2,
}

public enum FlexRollBiasMode
{
    NativeBiased = 0,
    NativePlusPlanBiased = 1,
}

public enum RetrainOperationKind
{
    RerollFlexActive = 0,
    RerollFlexPassive = 1,
    FullRetrain = 2,
}

public static class RecruitTierExtensions
{
    public static bool IsRarePlus(this RecruitTier tier)
    {
        return tier >= RecruitTier.Rare;
    }
}
