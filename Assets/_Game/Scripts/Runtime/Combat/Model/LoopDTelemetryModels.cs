using System;
using System.Collections.Generic;

namespace SM.Combat.Model;

public enum TelemetryDomain
{
    Combat = 0,
    Economy = 1,
    Recruit = 2,
    Retrain = 3,
    Duplicate = 4,
    Readability = 5,
    BalanceHarness = 6,
}

public enum TelemetryEventKind
{
    BattleStarted = 0,
    BattleEnded = 1,
    UnitSpawned = 2,
    UnitDied = 3,
    TargetAcquired = 4,
    TargetSwitched = 5,
    BasicAttackStarted = 6,
    BasicAttackResolved = 7,
    SkillCastStarted = 8,
    SkillCastResolved = 9,
    MobilityTriggered = 10,
    StatusApplied = 11,
    StatusRemoved = 12,
    DamageApplied = 13,
    HealingApplied = 14,
    BarrierApplied = 15,
    GuardBroken = 16,
    InterruptApplied = 17,
    SummonSpawned = 18,
    SummonDespawned = 19,
    KillCredited = 20,
    RecruitPackGenerated = 21,
    RecruitPurchased = 22,
    RecruitRefreshed = 23,
    ScoutUsed = 24,
    RetrainPerformed = 25,
    DuplicateConverted = 26,
    PruneFlagRaised = 27,
    ReadabilityViolationRaised = 28,
}

public enum ExplainedSourceKind
{
    BasicAttack = 0,
    SignatureActive = 1,
    FlexActive = 2,
    SignaturePassive = 3,
    FlexPassive = 4,
    MobilityReaction = 5,
    Affix = 6,
    Synergy = 7,
    Augment = 8,
    Status = 9,
    SummonAttack = 10,
    SummonSkill = 11,
    DeployablePulse = 12,
    SystemRule = 13,
}

public enum SalienceClass
{
    None = 0,
    Ambient = 1,
    Minor = 2,
    Major = 3,
    Critical = 4,
}

public enum DecisionReasonCode
{
    DefaultCadence = 0,
    MaintainRangeBand = 1,
    SecureKill = 2,
    BreakGuard = 3,
    CounterThreatLane = 4,
    PeelAlly = 5,
    SpendEnergyWindow = 6,
    TriggeredReaction = 7,
    EscapeThreat = 8,
    PunishExposedBackline = 9,
    BurstDuringCrowdControl = 10,
    FollowOwnerOrder = 11,
    SummonLifecycle = 12,
    RunEconomyChoice = 13,
}

[Serializable]
public sealed class TelemetryContext
{
    public string BuildCommit = string.Empty;
    public string BalanceSnapshotId = string.Empty;
    public string ScenarioId = string.Empty;
    public string SuiteId = string.Empty;
    public int Seed = 0;
    public string RunId = string.Empty;
    public string BattleId = string.Empty;
    public bool DeterministicHarness = false;
}

[Serializable]
public sealed class ExplainStamp
{
    public ExplainedSourceKind SourceKind = ExplainedSourceKind.SystemRule;
    public string SourceContentId = string.Empty;
    public string SourceDisplayName = string.Empty;
    public DecisionReasonCode ReasonCode = DecisionReasonCode.DefaultCadence;
    public SalienceClass Salience = SalienceClass.None;
}

[Serializable]
public sealed class TelemetryEntityRef
{
    public string UnitInstanceId = string.Empty;
    public string UnitBlueprintId = string.Empty;
    public string OwnerUnitInstanceId = string.Empty;
    public bool IsSummon = false;
    public bool IsDeployable = false;
    public int SideIndex = 0;
}

[Serializable]
public sealed class TelemetryEventRecord
{
    public TelemetryDomain Domain = TelemetryDomain.Combat;
    public TelemetryEventKind EventKind = TelemetryEventKind.BattleStarted;
    public float TimeSeconds = 0f;

    public TelemetryEntityRef? Actor;
    public TelemetryEntityRef? Target;

    public ExplainStamp? Explain;

    public string StatusId = string.Empty;
    public string SkillId = string.Empty;
    public string PassiveId = string.Empty;
    public string AffixId = string.Empty;
    public string SynergyId = string.Empty;
    public string AugmentId = string.Empty;

    public float ValueA = 0f;
    public float ValueB = 0f;
    public int IntValueA = 0;
    public bool BoolValueA = false;

    public string StringValueA = string.Empty;
    public string StringValueB = string.Empty;
}

[Serializable]
public sealed class BattleSummaryReport
{
    public string ScenarioId = string.Empty;
    public int Seed = 0;
    public float BattleDurationSeconds = 0f;
    public int WinnerSideIndex = 0;

    public float TimeToFirstDamageSeconds = 0f;
    public float TimeToFirstMajorActionSeconds = 0f;
    public bool TimeoutOccurred = false;

    public float UnexplainedDamageRatio = 0f;
    public float UnexplainedHealingRatio = 0f;
    public float OffscreenMajorEventRatio = 0f;
    public float TargetSwitchesPer10sP95 = 0f;
    public float IdleGapP95Seconds = 0f;
    public float MajorEventCollisionRate = 0f;
    public float SalienceWeightPer1sP95 = 0f;
    public float OverkillRatio = 0f;
    public float TopDamageShare = 0f;
    public float DeadBeforeFirstMajorActionRate = 0f;

    public string[] TopDamageSources = Array.Empty<string>();
    public string[] TopDecisionReasons = Array.Empty<string>();
    public string[] DecisiveMoments = Array.Empty<string>();
}

[Serializable]
public sealed class RunLiteSummaryReport
{
    public int Seed = 0;
    public int CombatsCompleted = 0;
    public int RecruitsPurchased = 0;
    public int ScoutUses = 0;
    public int RetrainUses = 0;
    public int DuplicatesConverted = 0;

    public float NoAffordableOptionRate = 0f;
    public float MeaningfulChoicePhaseRate = 0f;
    public float EchoSpendRatio = 0f;
    public float OnPlanPurchaseShare = 0f;
    public float ProtectedPurchaseShare = 0f;
    public float RetrainUseRate = 0f;
    public float ScoutUseRate = 0f;
}

public enum ReadabilityViolationKind
{
    UnexplainedDamage = 0,
    UnexplainedHealing = 1,
    SalienceOverload = 2,
    MajorEventCollision = 3,
    IdleGapTooLong = 4,
    TargetThrash = 5,
    StatusChipOverflow = 6,
    FloatingTextBurstOverflow = 7,
    OffscreenMajorEvent = 8,
    ProcChainOpacity = 9,
}

public enum ReadabilityGateSeverity
{
    Warning = 0,
    Error = 1,
    Fatal = 2,
}

public enum ReadabilityAggregationKind
{
    None = 0,
    MergeMinorTicksBySourceTarget = 1,
    MergeDotTicksByStatus = 2,
    CollapseRepeatedBarrierTicks = 3,
    SuppressAmbientWhileMajorActive = 4,
}

[Serializable]
public sealed class ReadabilityGateConfig
{
    public float UnexplainedDamageRatioMax = 0.05f;
    public float UnexplainedHealingRatioMax = 0.05f;
    public float OffscreenMajorEventRatioMax = 0.10f;
    public float TargetSwitchesPer10sP95Max = 6.0f;
    public float IdleGapP95MaxSeconds = 3.25f;
    public float TimeToFirstMajorActionP50Max = 6.0f;
    public float TimeToFirstMajorActionP50Min = 1.25f;
    public float MajorEventCollisionRateMax = 0.20f;
    public float SalienceWeightPer1sP95Max6Combat = 9.0f;
    public int MaxStatusChipsPerUnit = 3;
    public int MaxFloatingTextBurstsPerTargetPerSec = 4;
    public int MinorAggregationWindowMs = 330;

    public float ResolveScaledSalienceBudget(int combatantCount)
    {
        return SalienceWeightPer1sP95Max6Combat + (0.5f * Math.Max(0, combatantCount - 6));
    }
}

[Serializable]
public sealed class ReadabilityWindowSample
{
    public float WindowStart = 0f;
    public float WindowDuration = 0f;
    public int CombatantCount = 0;
    public float SalienceWeight = 0f;
    public int MajorOrCriticalCount = 0;
    public int OffscreenMajorCount = 0;
}

[Serializable]
public sealed class ReadabilityReport
{
    public float UnexplainedDamageRatio = 0f;
    public float UnexplainedHealingRatio = 0f;
    public float OffscreenMajorEventRatio = 0f;
    public float TargetSwitchesPer10sP95 = 0f;
    public float IdleGapP95Seconds = 0f;
    public float TimeToFirstMajorActionP50 = 0f;
    public float MajorEventCollisionRate = 0f;
    public float SalienceWeightPer1sP95 = 0f;
    public float StatusChipOverflowRate = 0f;
    public float FloatingTextBurstOverflowRate = 0f;
    public ReadabilityViolationKind[] Violations = Array.Empty<ReadabilityViolationKind>();
}

public enum ContentKind
{
    UnitBlueprint = 0,
    SignatureActive = 1,
    SignaturePassive = 2,
    FlexActive = 3,
    FlexPassive = 4,
    Affix = 5,
    SynergyFamily = 6,
    TemporaryAugment = 7,
    PermanentAugment = 8,
    PassiveBoard = 9,
}

public enum ContentHealthGrade
{
    InsufficientData = 0,
    Healthy = 1,
    Watch = 2,
    AtRisk = 3,
    Broken = 4,
}

public enum PruneDisposition
{
    Keep = 0,
    RetuneNumbers = 1,
    RetuneCadence = 2,
    SimplifyReadability = 3,
    MergeWithSibling = 4,
    MoveOutOfV1 = 5,
    Remove = 6,
}

public enum PruneReason
{
    OutlierPower = 0,
    UnderpickedRedundant = 1,
    OverpickedDominant = 2,
    IdentityOverlap = 3,
    ReadabilityDebt = 4,
    CounterTopologyGap = 5,
    CounterTopologyDominance = 6,
    EconomyDistortion = 7,
    VarianceTooHigh = 8,
    UnsupportedBySliceCoverage = 9,
    BudgetRarityMismatch = 10,
    ForbiddenLeak = 11,
}

[Serializable]
public sealed class ContentDebtVector
{
    public int PowerDebt = 0;
    public int ReadabilityDebt = 0;
    public int RedundancyDebt = 0;
    public int VarianceDebt = 0;
    public int TopologyDebt = 0;
    public int EconomyDebt = 0;

    public int Total => PowerDebt + ReadabilityDebt + RedundancyDebt + VarianceDebt + TopologyDebt + EconomyDebt;
}

[Serializable]
public sealed class ContentIdentityFingerprint
{
    public string PrimaryTagA = string.Empty;
    public string PrimaryTagB = string.Empty;
    public string RoleProfileId = string.Empty;
    public string RarityId = string.Empty;
    public string PowerBandId = string.Empty;
    public string ActivationModelId = string.Empty;
    public string RangeDisciplineId = string.Empty;
    public bool UsesSummon = false;
    public bool UsesDeployable = false;
    public bool UsesBarrier = false;
    public bool UsesGuardBreak = false;
    public string[] ThreatPatterns = Array.Empty<string>();
    public string[] CounterTools = Array.Empty<string>();
}

[Serializable]
public sealed class ContentHealthCard
{
    public string ContentId = string.Empty;
    public ContentKind ContentKind = ContentKind.UnitBlueprint;
    public ContentHealthGrade Grade = ContentHealthGrade.InsufficientData;
    public PruneDisposition SuggestedDisposition = PruneDisposition.Keep;
    public PruneReason[] Reasons = Array.Empty<PruneReason>();

    public int ExposureCount = 0;
    public int PickCount = 0;
    public float PickRate = 0f;
    public float PresenceWinRate = 0f;
    public float PresenceWinDelta = 0f;
    public float TopDeckConcentration = 0f;
    public float UnexplainedEffectShare = 0f;
    public float ContributionToSalienceOverload = 0f;
    public float HighestIdentitySimilarity = 0f;
    public bool ProvidesUniqueCoverage = false;

    public ContentDebtVector Debt = new();
    public ContentIdentityFingerprint Fingerprint = new();
}

[Serializable]
public sealed class PruneLedgerEntry
{
    public string ContentId = string.Empty;
    public ContentKind ContentKind = ContentKind.UnitBlueprint;
    public ContentHealthGrade Grade = ContentHealthGrade.InsufficientData;
    public PruneDisposition Disposition = PruneDisposition.Keep;
    public PruneReason[] Reasons = Array.Empty<PruneReason>();
    public bool ProvidesUniqueCoverage = false;
    public string SiblingContentId = string.Empty;
    public string Note = string.Empty;
}

public enum BalanceSuiteId
{
    PureKit = 0,
    SystemicSlice = 1,
    RunLite = 2,
}

public enum BalanceScenarioId
{
    Duel_MeleeMirror_1v1 = 0,
    Standard_BalancedMirror_3v3 = 1,
    MeleeCollapse_vs_RangerHold = 2,
    Dive_vs_BacklinePeel = 3,
    SustainBall_vs_BurstSpike = 4,
    SwarmFlood_vs_Cleave = 5,
    ControlChain_vs_Tenacity = 6,
    ArmorFrontline_vs_ArmorShred = 7,
    ResistanceShell_vs_Exposure = 8,
    GuardBulwark_vs_MultiHitBreak = 9,
    EvasiveSkirmish_vs_TrackingArea = 10,
    MixedDraft_4v4 = 11,
}

public enum SliceCoverageQuotaKind
{
    FrontlineAnchor = 0,
    MeleePressure = 1,
    BacklineCarry = 2,
    MagicSource = 3,
    SupportSource = 4,
    DiveSource = 5,
    SummonSource = 6,
    AntiSwarmSource = 7,
    AntiSustainSource = 8,
    AntiControlSource = 9,
}

[Serializable]
public sealed class SliceCoverageQuota
{
    public SliceCoverageQuotaKind Kind = SliceCoverageQuotaKind.FrontlineAnchor;
    public int MinimumCount = 0;
}

[Serializable]
public sealed class BalanceTargetBand
{
    public BalanceSuiteId SuiteId = BalanceSuiteId.PureKit;
    public string MetricId = string.Empty;
    public float MinValue = 0f;
    public float MaxValue = 0f;
}

[Serializable]
public sealed class HarnessExecutionPlan
{
    public BalanceSuiteId SuiteId = BalanceSuiteId.PureKit;
    public BalanceScenarioId[] ScenarioIds = Array.Empty<BalanceScenarioId>();
    public int SeedsPerScenario = 0;
    public bool MirrorSides = true;
    public bool RandomizeStartLanes = true;
}
