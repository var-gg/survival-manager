using System;
using System.Collections.Generic;

namespace SM.Editor.Validation;

internal sealed record CampaignRecoveryMeasurementReport(
    string SchemaVersion,
    int CellCount,
    int AttemptCap,
    string PolicyId,
    IReadOnlyList<CampaignRecoveryNodeObservation> Nodes,
    CampaignClearedSiteReentryObservation ClearedSiteReentry,
    CampaignRecoveryReachabilityObservation Reachability,
    string CanonicalHash);

internal sealed record CampaignRecoveryNodeObservation(
    string NodeId,
    string SiteId,
    string Band,
    double CanonicalFirstAttemptWinRate,
    IReadOnlyList<CampaignRecoveryPairObservation> Pairs);

internal sealed record CampaignRecoveryPairObservation(
    string CellId,
    CampaignRecoveryArmObservation ArmA,
    CampaignRecoveryArmObservation ArmB);

internal sealed record CampaignRecoveryArmObservation(
    string ArmId,
    bool ClearedWithinCap,
    int AttemptsToClearCensored,
    IReadOnlyList<CampaignRecoveryAttemptObservation> Attempts,
    CampaignRecoveryMutationTotals Mutations);

internal sealed record CampaignRecoveryAttemptObservation(
    int Attempt,
    bool TargetReached,
    bool TargetWon,
    string TerminalNodeId,
    int TerminalBattleSeed,
    CampaignRecoveryPowerObservation RunEntryPower,
    CampaignRecoveryPowerObservation? TargetPower,
    IReadOnlyList<CampaignRecoverySettlementObservation> Settlements);

internal sealed record CampaignRecoveryPowerObservation(
    double EffectiveHp,
    double EffectiveOffense,
    int HeroLevelSum,
    int EquippedItemCount);

internal sealed record CampaignRecoverySettlementObservation(
    string NodeId,
    bool Victory,
    string ChoiceKind,
    int GoldDelta,
    int EchoDelta,
    int AugmentChoiceDelta,
    bool RunTerminated,
    bool TownDecisionsDriven,
    int RecruitDecisionApplied,
    int PassiveDecisionApplied,
    int RefitDecisionApplied,
    int PrepEquipmentAssignments);

internal sealed record CampaignRecoveryMutationTotals(
    int DefeatSettlements,
    int GoldDelta,
    int EchoDelta,
    int AugmentChoices,
    int RecruitDecisionsApplied,
    int PassiveDecisionsApplied,
    int RefitDecisionsApplied,
    int PrepEquipmentAssignments);

internal sealed record CampaignClearedSiteReentryObservation(
    string SiteId,
    bool CanReenter,
    bool RewardsAgain,
    int RevisitLifetimeExperienceDelta,
    int RevisitGoldDelta,
    int RevisitEchoDelta,
    int RevisitInventoryDelta,
    int RevisitRewardLedgerDelta,
    int RevisitPermanentAugmentDelta,
    bool UnboundedFarmClosed,
    IReadOnlyList<CampaignRevisitRewardObservation> Revisits);

internal sealed record CampaignRevisitRewardObservation(
    int RevisitIndex,
    int LifetimeExperienceDelta,
    int GoldDelta,
    int EchoDelta,
    int InventoryDelta,
    int RewardLedgerDelta,
    int PermanentAugmentDelta);

internal sealed record CampaignRecoveryReachabilityObservation(
    bool DefeatRewardsDriven,
    bool RunTerminationDriven,
    bool TownDecisionsDriven,
    IReadOnlyList<string> UnreachableParts);

internal sealed record CampaignRecoveryTarget(
    string NodeId,
    string SiteId,
    string Band,
    double CanonicalFirstAttemptWinRate);

internal sealed record CampaignRecoveryArrival(
    string ProfileSnapshot,
    string CellId,
    CampaignRecoveryTarget Target);

internal sealed class CampaignRecoveryMutationCounter
{
    public int DefeatSettlements { get; set; }
    public int GoldDelta { get; set; }
    public int EchoDelta { get; set; }
    public int AugmentChoices { get; set; }
    public int RecruitDecisionsApplied { get; set; }
    public int PassiveDecisionsApplied { get; set; }
    public int RefitDecisionsApplied { get; set; }
    public int PrepEquipmentAssignments { get; set; }

    public CampaignRecoveryMutationTotals ToObservation()
        => new(
            DefeatSettlements,
            GoldDelta,
            EchoDelta,
            AugmentChoices,
            RecruitDecisionsApplied,
            PassiveDecisionsApplied,
            RefitDecisionsApplied,
            PrepEquipmentAssignments);
}
