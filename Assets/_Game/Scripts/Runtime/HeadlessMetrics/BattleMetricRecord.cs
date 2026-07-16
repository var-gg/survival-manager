using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>
/// H100 전투 한 건의 관측 전용 원시 레코드. sim/save truth를 소유하지 않으며 기존 전투 결과와
/// telemetry를 결정적으로 투영한다. nullable 필드는 아직 해당 counterfactual 실험이 수행되지 않았음을 뜻한다.
/// </summary>
public sealed record BattleMetricRecord
{
    public const string CurrentSchemaVersion = "battle-metric-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public string CampaignId { get; init; } = string.Empty;
    public string BattleId { get; init; } = string.Empty;
    public string ReplayGroupId { get; init; } = string.Empty;
    public int ReplayIteration { get; init; }
    public string ScenarioId { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
    public string BuildFamilyId { get; init; } = string.Empty;
    public string OpponentFamilyId { get; init; } = string.Empty;
    public string AllyFormationId { get; init; } = string.Empty;
    public string EnemyFormationId { get; init; } = string.Empty;
    public IReadOnlyList<MetricCount> AllyBuildComponentCounts { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<MetricCount> EnemyBuildComponentCounts { get; init; } = Array.Empty<MetricCount>();
    public bool IntentionalHardCounter { get; init; }
    public int Seed { get; init; }
    public float FixedStepSeconds { get; init; }
    public int StepCount { get; init; }
    public float DurationSeconds { get; init; }
    public string WinnerSide { get; init; } = "none";
    public bool Timeout { get; init; }
    public bool Stomp { get; init; }
    public string FirstDeathSide { get; init; } = "none";
    public float AllySurvivingHp { get; init; }
    public float EnemySurvivingHp { get; init; }
    public float FinalHpDifference { get; init; }
    public float NormalizedFinalPowerDifference { get; init; }

    public int FlankStrikeCount { get; init; }
    public int RearStrikeCount { get; init; }
    public int ScreenBlockCount { get; init; }
    public int ScreenAbsorbCount { get; init; }
    public int ScreenDeterrenceCount { get; init; }
    public int SaveMomentCount { get; init; }
    public int BacklineDiveKillCount { get; init; }

    public IReadOnlyList<MetricCount> SynergyRuleActivationCounts { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<MetricCount> ComboRuleActivationCounts { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<MetricCount> AugmentRuleActivationCounts { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<MetricCount> DoctrineRuleActivationCounts { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<string> EligibleDepthRuleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FiredDepthRuleIds { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CausalDepthRuleIds { get; init; } = Array.Empty<string>();
    public int SalientEventCount { get; init; }
    public int CausalSalientEventCount { get; init; }

    public float? FormationWinRateLeverage { get; init; }
    public bool? FormationSensitive { get; init; }
    public bool? DefaultFormationWasOptimal { get; init; }

    public bool Crashed { get; init; }
    public bool Softlocked { get; init; }
    public bool ContainsNonFinite { get; init; }
    public bool IllegalNegativeState { get; init; }
    public bool NonTerminating { get; init; }
    public string FailureCode { get; init; } = string.Empty;

    public string ReplayHash { get; init; } = string.Empty;
    public string CanonicalStateHash { get; init; } = string.Empty;
    public string ActivityReplayHash { get; init; } = string.Empty;
}
