using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>H100 캠페인 한 건의 완주·정책·무결성 관측 레코드.</summary>
public sealed record CampaignMetricRecord
{
    public const string CurrentSchemaVersion = "campaign-metric-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public string CampaignId { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
    public string DifficultyId { get; init; } = "normal";
    public int Seed { get; init; }
    public bool Completed { get; init; }
    public bool Truncated { get; init; }
    public string TerminalReason { get; init; } = string.Empty;
    public int SiteCount { get; init; }
    public int BattleCount { get; init; }
    public int WinCount { get; init; }
    public int LossCount { get; init; }
    public int TimeoutCount { get; init; }
    public int StompCount { get; init; }
    public int ForcedTimeoutCount { get; init; }
    public float TotalBattleSeconds { get; init; }

    public int DecisionCount { get; init; }
    public bool DecisionMetricsAvailable { get; init; }
    public int ImportantDecisionCount { get; init; }
    public int NearBestAlternativeDecisionCount { get; init; }
    public int HighLeverageDecisionCount { get; init; }

    public string MacroFamilyId { get; init; } = string.Empty;
    public IReadOnlyList<MetricCount> BuildFamilySelectionCounts { get; init; } = Array.Empty<MetricCount>();

    public int CrashCount { get; init; }
    public int SoftlockCount { get; init; }
    public int NonFiniteStateCount { get; init; }
    public int IllegalNegativeStateCount { get; init; }
    public int NonTerminatingBattleCount { get; init; }
    public string ReplayManifestHash { get; init; } = string.Empty;
}
