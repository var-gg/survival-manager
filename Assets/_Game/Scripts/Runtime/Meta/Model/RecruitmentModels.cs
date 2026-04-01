using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Meta.Model;

[Serializable]
public sealed class EconomyWallet
{
    public int Gold;
    public int Echo;
}

[Serializable]
public sealed class RecruitTierCostTable
{
    public int CommonGoldCost = 4;
    public int RareGoldCost = 7;
    public int EpicGoldCost = 10;

    public int GetCost(RecruitTier tier)
    {
        return tier switch
        {
            RecruitTier.Common => CommonGoldCost,
            RecruitTier.Rare => RareGoldCost,
            RecruitTier.Epic => EpicGoldCost,
            _ => CommonGoldCost,
        };
    }
}

[Serializable]
public sealed class DuplicateEchoValueTable
{
    public int CommonEchoValue = 30;
    public int RareEchoValue = 50;
    public int EpicEchoValue = 80;

    public int GetValue(RecruitTier tier)
    {
        return tier switch
        {
            RecruitTier.Common => CommonEchoValue,
            RecruitTier.Rare => RareEchoValue,
            RecruitTier.Epic => EpicEchoValue,
            _ => CommonEchoValue,
        };
    }
}

[Serializable]
public sealed class ScoutDirective
{
    public ScoutDirectiveKind Kind = ScoutDirectiveKind.None;
    public string SynergyTagId = string.Empty;

    public bool IsNone => Kind == ScoutDirectiveKind.None;

    public ScoutDirective Clone()
    {
        return new ScoutDirective
        {
            Kind = Kind,
            SynergyTagId = SynergyTagId,
        };
    }
}

[Serializable]
public sealed class RecruitPityState
{
    public int PacksSinceRarePlusSeen;
    public int PacksSinceEpicSeen;

    public RecruitPityState Clone()
    {
        return new RecruitPityState
        {
            PacksSinceRarePlusSeen = PacksSinceRarePlusSeen,
            PacksSinceEpicSeen = PacksSinceEpicSeen,
        };
    }
}

[Serializable]
public sealed class RecruitPhaseState
{
    public int FreeRefreshesRemaining = 1;
    public int PaidRefreshCountThisPhase;
    public bool ScoutUsedThisPhase;
    public ScoutDirective PendingScoutDirective = new();

    public RecruitPhaseState Clone()
    {
        return new RecruitPhaseState
        {
            FreeRefreshesRemaining = FreeRefreshesRemaining,
            PaidRefreshCountThisPhase = PaidRefreshCountThisPhase,
            ScoutUsedThisPhase = ScoutUsedThisPhase,
            PendingScoutDirective = PendingScoutDirective?.Clone() ?? new ScoutDirective(),
        };
    }
}

[Serializable]
public sealed class CandidatePlanScoreBreakdown
{
    public int BreakpointProgressScore;
    public int NativeTagMatchScore;
    public int RoleNeedScore;
    public int AugmentHookScore;
    public int ScoutDirectiveScore;
    public int OversaturationPenalty;

    public int Total =>
        BreakpointProgressScore +
        NativeTagMatchScore +
        RoleNeedScore +
        AugmentHookScore +
        ScoutDirectiveScore -
        OversaturationPenalty;
}

[Serializable]
public sealed class RecruitOfferMetadata
{
    public RecruitOfferSlotType SlotType = RecruitOfferSlotType.StandardA;
    public RecruitTier Tier = RecruitTier.Common;
    public CandidatePlanFit PlanFit = CandidatePlanFit.OffPlan;
    public CandidatePlanScoreBreakdown PlanScore = new();
    public bool ProtectedByPity;
    public bool BiasedByScout;
    public int GoldCost;
}

[Serializable]
public sealed class RecruitUnitPreview
{
    public string UnitBlueprintId = string.Empty;
    public string UnitInstanceSeed = string.Empty;
    public string FlexActiveId = string.Empty;
    public string FlexPassiveId = string.Empty;
    public RecruitOfferMetadata Metadata = new();
}

[Serializable]
public sealed class UnitRetrainState
{
    public int RetrainCount;
    public string PreviousFlexActiveId = string.Empty;
    public string PreviousFlexPassiveId = string.Empty;
    public int TotalEchoSpent;
    public int ConsecutivePlanIncoherentRetrains;

    public UnitRetrainState Clone()
    {
        return new UnitRetrainState
        {
            RetrainCount = RetrainCount,
            PreviousFlexActiveId = PreviousFlexActiveId,
            PreviousFlexPassiveId = PreviousFlexPassiveId,
            TotalEchoSpent = TotalEchoSpent,
            ConsecutivePlanIncoherentRetrains = ConsecutivePlanIncoherentRetrains,
        };
    }
}

[Serializable]
public sealed class RetrainCostTable
{
    public int FlexActiveBaseEchoCost = 40;
    public int FlexPassiveBaseEchoCost = 30;
    public int FullRetrainBaseEchoCost = 60;
    public int PerUnitEscalation = 10;
    public int EscalationCap = 30;

    public int GetBaseCost(RetrainOperationKind operation)
    {
        return operation switch
        {
            RetrainOperationKind.RerollFlexActive => FlexActiveBaseEchoCost,
            RetrainOperationKind.RerollFlexPassive => FlexPassiveBaseEchoCost,
            RetrainOperationKind.FullRetrain => FullRetrainBaseEchoCost,
            _ => FlexActiveBaseEchoCost,
        };
    }

    public int GetEscalation(UnitRetrainState? state)
    {
        if (state == null)
        {
            return 0;
        }

        return Math.Min(EscalationCap, Math.Max(0, state.RetrainCount) * PerUnitEscalation);
    }

    public int GetTotalCost(RetrainOperationKind operation, UnitRetrainState? state)
    {
        return GetBaseCost(operation) + GetEscalation(state);
    }
}

[Serializable]
public sealed class DuplicateConversionResult
{
    public DuplicateResolutionKind Resolution = DuplicateResolutionKind.ConvertToEcho;
    public RecruitTier SourceTier = RecruitTier.Common;
    public int EchoGranted;
}

[Serializable]
public sealed class UnitEconomyFootprint
{
    public int RecruitGoldPaid;
    public int RetrainEchoPaid;

    public UnitEconomyFootprint Clone()
    {
        return new UnitEconomyFootprint
        {
            RecruitGoldPaid = RecruitGoldPaid,
            RetrainEchoPaid = RetrainEchoPaid,
        };
    }
}

[Serializable]
public sealed class TeamPlanProfile
{
    public List<string> TopSynergyTagIds = new();
    public Dictionary<string, int> BreakpointGapsByTag = new();
    public bool NeedsFrontline;
    public bool NeedsBackline;
    public bool NeedsSupport;
    public bool PrefersPhysical;
    public bool PrefersMagical;
    public List<string> AugmentHookTags = new();
    public TeamCounterCoverageReport CounterCoverage = new();
}

public sealed record RecruitCandidateEvaluation(
    string UnitBlueprintId,
    CandidatePlanFit PlanFit,
    CandidatePlanScoreBreakdown Score,
    RecruitTier Tier);

public sealed record RecruitPackGenerationResult(
    IReadOnlyList<RecruitUnitPreview> Offers,
    RecruitPityState UpdatedPity,
    RecruitPhaseState UpdatedPhase);

public static class RecruitmentBalanceCatalog
{
    public static readonly RecruitTierCostTable DefaultRecruitTierCosts = new();
    public static readonly DuplicateEchoValueTable DefaultDuplicateEchoValues = new();
    public static readonly RetrainCostTable DefaultRetrainCosts = new();

    public const int ScoutEchoCost = 35;
}
