using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Combat.Model;

public enum BuffPropagationKind
{
    BurstHeal = 0,
    HealOverTime = 1,
    ShieldBarrier = 2,
    AttackBuff = 3,
    CritBuff = 4,
    DamageReductionAura = 5,
    Cleanse = 6,
}

public sealed record EffectPositionUnit(
    string UnitId,
    TeamSide Side,
    CombatVector2 Position,
    float HealthRatio,
    FormationLine FormationLine,
    bool IsMarked,
    bool IsVulnerable);

public sealed record EffectPositionSnapshot(
    int StepIndex,
    float TimeSeconds,
    IReadOnlyList<EffectPositionUnit> Units);

public sealed record AuraLingerEntry(
    string UnitId,
    float EligibleUntilSeconds);

public sealed record BuffPropagationRequest(
    string SourceId,
    TeamSide SourceSide,
    CombatVector2 Center,
    float Radius,
    BuffPropagationKind Kind,
    string Category,
    float BaseValue,
    int ActivationIndex = 0,
    IReadOnlyList<AuraLingerEntry>? LingerEntries = null,
    float AuraMembershipLingerSeconds = 0.35f);

public sealed record BuffPropagationTarget(
    string UnitId,
    float Distance,
    bool IsLingering);

public sealed record BuffPropagationResult(
    string WinningSourceId,
    string Category,
    BuffPropagationKind Kind,
    float BaseValue,
    float EffectiveValue,
    float EfficacyBonus,
    int EffectiveCount,
    int OvercapEvents,
    int MissedByDistanceCount,
    IReadOnlyList<BuffPropagationTarget> EligibleTargets);

public sealed record AoeClusterCandidate(
    string CandidateId,
    CombatVector2 Center,
    float PrimaryTargetValue,
    float ClusterMass,
    int AffectedCount,
    float SkillBias,
    float FriendlyRisk,
    float Score);

public sealed record AoeDistributionHit(
    string TargetUnitId,
    float Distance,
    float Falloff,
    float DamageMultiplier,
    int ChainIndex = 0);

public sealed record AoeClusterSelection(
    BattleAreaEffectFamily Family,
    float Radius,
    AoeClusterCandidate Candidate,
    IReadOnlyList<AoeDistributionHit> Hits);

public sealed record GroupDispersalLockState(
    string UnitId,
    CombatVector2 Center,
    float DispersedUntilSeconds,
    float Severity);

public sealed record ClusterTradeoffTelemetryFrame(
    float ClusterCohesionIndex,
    IReadOnlyDictionary<string, float> BuffCoverageHistogramByType,
    IReadOnlyDictionary<string, float> BuffEfficacyBonusByType,
    int ClusterBuffOvercapEvents,
    int BuffMissedByDistanceCount,
    float AoeCandidateClusterScore,
    IReadOnlyDictionary<string, float> AoeCatchCountHistogram,
    int CleaveCatchCount,
    int ChainJumpCount,
    int KnockbackDispersalEvents,
    float ReclusterLatencyMs,
    float FocusDamageContribution,
    float BuffValueContribution,
    float AoeCostTaken,
    float ClusterTradeoffNetValue);
