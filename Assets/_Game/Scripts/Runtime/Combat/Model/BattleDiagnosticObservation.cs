using System;
using System.Collections.Generic;
using SM.Core.Contracts;

namespace SM.Combat.Model;

/// <summary>
/// Out-of-band, non-authoritative diagnostics consumed by headless measurement runs. These records must never
/// participate in battle resolution, replay serialization, canonical hashing, or RNG decisions.
/// </summary>
internal enum BattleDiagnosticKind
{
    TargetSelection = 0,
    TacticEvaluation = 1,
    IntentOverride = 2,
    DiveIntentEvaluation = 3,
    DisplacementLifecycle = 4,
    HealingApplication = 5,
}

internal interface IBattleDiagnosticObserver
{
    bool ShouldObserve(BattleDiagnosticKind kind, string actorId, string skillId = "");

    void Observe(BattleDiagnosticEvent diagnosticEvent);
}

internal abstract record BattleDiagnosticEvent(
    BattleDiagnosticKind Kind,
    int StepIndex,
    float TimeSeconds,
    string ActorId);

internal enum TargetSelectionPurpose
{
    Unknown = 0,
    Mobility = 1,
    Signature = 2,
    Flex = 3,
    BasicAttack = 4,
    Legacy = 5,
}

internal enum TargetCandidateRejectionReason
{
    None = 0,
    MissingOrDead = 1,
    WrongSide = 2,
    SelfAllyExcluded = 3,
    ExcludeSummons = 4,
    ExcludeFullHealthAllies = 5,
    RequireMarked = 6,
    RequireBacklineExposed = 7,
    OutOfAcquireRange = 8,
}

internal sealed record TargetCandidateDiagnostic(
    string TargetId,
    string ArchetypeId,
    string ClassId,
    FormationLine FormationLine,
    bool Alive,
    float CurrentHealth,
    float HealthRatio,
    float EdgeDistance,
    float AcquireRange,
    string AcquireRangeSource,
    TargetCandidateRejectionReason InitialRejection,
    bool EligibleAfterRangeRelaxation,
    bool BacklineExposed,
    bool Screened,
    int ScreenedSortKey,
    float TargetSwitchPenalty,
    float FocusBias,
    float FlankBias,
    float ScreenPenalty,
    float GuardedPenalty,
    float FocusMarkBias,
    float ComboOpportunityBias,
    float TotalTacticBias,
    float DistanceScore);

internal sealed record TargetSelectionDiagnosticEvent(
    int StepIndex,
    float TimeSeconds,
    string ActorId,
    string ActorArchetypeId,
    string ActorClassId,
    TargetSelectionPurpose Purpose,
    string SkillId,
    TargetDomain Domain,
    TargetSelector PrimarySelector,
    TargetFallbackPolicy FallbackPolicy,
    TargetFilterFlags Filters,
    float AuthoredMaxAcquireRange,
    float ResolvedAcquireRange,
    string AcquireRangeSource,
    string CurrentTargetId,
    string PrimarySelectedTargetId,
    string FinalSelectedTargetId,
    bool FallbackUsed,
    bool MeleeNearestOverrideApplied,
    IReadOnlyList<TargetCandidateDiagnostic> Candidates)
    : BattleDiagnosticEvent(BattleDiagnosticKind.TargetSelection, StepIndex, TimeSeconds, ActorId);

internal enum StableTargetDisposition
{
    NotEvaluated = 0,
    NoCurrentTarget = 1,
    CurrentTargetInvalid = 2,
    HeldBySwitchLock = 3,
    HeldUntilReevaluation = 4,
    ReleasedOutsideAcquireLeash = 5,
    ReleasedForReevaluation = 6,
}

internal sealed record TacticEvaluationDiagnosticEvent(
    int StepIndex,
    float TimeSeconds,
    string ActorId,
    string ActorArchetypeId,
    string ActorClassId,
    string CurrentTargetIdAtEntry,
    float TargetSwitchLockRemaining,
    bool NeedsReevaluation,
    StableTargetDisposition StableTargetDisposition,
    string StableTargetId,
    float StableTargetEdgeDistance,
    float StableTargetAcquireLeash,
    BattleActionType ActionType,
    string SelectedTargetId,
    string SkillId,
    SkillDisplacementKind DisplacementKind)
    : BattleDiagnosticEvent(BattleDiagnosticKind.TacticEvaluation, StepIndex, TimeSeconds, ActorId);

internal sealed record IntentOverrideDiagnosticEvent(
    int StepIndex,
    float TimeSeconds,
    string ActorId,
    string PreIntentTargetId,
    CombatIntentType IntentType,
    string IntentTargetId,
    bool OverrideApplied,
    string FinalTargetId)
    : BattleDiagnosticEvent(BattleDiagnosticKind.IntentOverride, StepIndex, TimeSeconds, ActorId);

internal enum DiveIntentGateReason
{
    Selected = 0,
    HoldBruiserTag = 1,
    AttackRangeAboveMeleeThreshold = 2,
    PostureDisallowsDive = 3,
    HealthBelowEntryThreshold = 4,
    SupportProxyMissing = 5,
    TooManyNearbyEnemies = 6,
    NoRuntimeBacklineCandidate = 7,
    PostureFilteredAllCandidates = 8,
    CandidateScoreBelowEntryThreshold = 9,
    TeamDiveSlotUnavailable = 10,
    DistinctTargetUnavailable = 11,
    ContinueScoreBelowThreshold = 12,
}

internal sealed record DiveTargetCandidateDiagnostic(
    string TargetId,
    string ArchetypeId,
    string ClassId,
    FormationLine FormationLine,
    bool Alive,
    bool PostureEligible,
    float HealthRatio,
    bool HasFocusMark,
    bool HasFrontlineProtector,
    float ForwardDepth,
    float PathDistance,
    int FormationLineScore,
    int ClassScore,
    int LowHealthScore,
    int ProtectorScore,
    int FocusMarkScore,
    int ForwardDepthScore,
    int PathDistanceScore,
    int TotalScore,
    int RequiredScore);

internal sealed record DiveIntentDiagnosticEvent(
    int StepIndex,
    float TimeSeconds,
    string ActorId,
    string ActorArchetypeId,
    string ActorClassId,
    DiveIntentGateReason Reason,
    bool ContinuingExistingDive,
    string CurrentIntentTargetId,
    string SelectedTargetId,
    TeamPostureType Posture,
    float ActorHealthRatio,
    float RequiredHealthRatio,
    float ActorAttackRange,
    float MeleeRangeThreshold,
    float MaxForwardDepth,
    float MaxPathDistance,
    bool HasSupportProxy,
    int NearbyEnemyCount,
    int NearbyEnemyLimit,
    int ActiveDiverCount,
    int EligibleDiverCount,
    IReadOnlyList<DiveTargetCandidateDiagnostic> Candidates)
    : BattleDiagnosticEvent(BattleDiagnosticKind.DiveIntentEvaluation, StepIndex, TimeSeconds, ActorId);

internal enum DisplacementLifecycleStage
{
    Selected = 0,
    CastStarted = 1,
    Resolved = 2,
    Aborted = 3,
}

internal sealed record DisplacementLifecycleDiagnosticEvent(
    int StepIndex,
    float TimeSeconds,
    string ActorId,
    string ActorArchetypeId,
    string ActorClassId,
    string TargetId,
    string TargetArchetypeId,
    string SkillId,
    SkillDisplacementKind DisplacementKind,
    float AuthoredDisplacementDistance,
    DisplacementLifecycleStage Stage,
    long ActionInstanceId,
    float EdgeDistance,
    float ActorDisplacement,
    float TargetDisplacement,
    string AbortReason)
    : BattleDiagnosticEvent(BattleDiagnosticKind.DisplacementLifecycle, StepIndex, TimeSeconds, ActorId);

internal sealed record HealingApplicationDiagnosticEvent(
    int StepIndex,
    float TimeSeconds,
    string ActorId,
    string ActorArchetypeId,
    string ActorClassId,
    string TargetId,
    string SkillId,
    string SourceId,
    float RawAmount,
    float AttemptedAfterModifiers,
    float EffectiveAmount,
    float OverhealAmount)
    : BattleDiagnosticEvent(BattleDiagnosticKind.HealingApplication, StepIndex, TimeSeconds, ActorId);

internal readonly record struct HealingApplicationResult(
    float RawAmount,
    float AttemptedAfterModifiers,
    float EffectiveAmount)
{
    internal float OverhealAmount => MathF.Max(0f, AttemptedAfterModifiers - EffectiveAmount);
}
