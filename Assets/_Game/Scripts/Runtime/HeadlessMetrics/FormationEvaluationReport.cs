using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>Coverage와 Competent를 분리해 Q5 threshold를 fail-closed로 판정한 Stage 4 보고서.</summary>
public sealed record FormationEvaluationReport
{
    public const string CurrentSchemaVersion = "formation-evaluation-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public string CoveragePolicyId { get; init; } = string.Empty;
    public string CompetentPolicyId { get; init; } = string.Empty;
    public string CausalMethod { get; init; } = string.Empty;
    public string CausalPrecisionNote { get; init; } = string.Empty;
    public bool CoveragePass { get; init; }
    public bool CompetentPrevalencePass { get; init; }
    public bool CompetentImpactPass { get; init; }
    public bool CompetentLegibilityPass { get; init; }
    public bool PlacementLeveragePass { get; init; }
    public bool HealerSelectionPass { get; init; }
    public bool CompetentQ5Pass { get; init; }
    public bool NeedsStageFiveBalance { get; init; }
    public IReadOnlyList<string> ChannelsNeedingTuning { get; init; } = Array.Empty<string>();
    public IReadOnlyList<FormationPolicySummary> PolicySummaries { get; init; } = Array.Empty<FormationPolicySummary>();
    public PlacementGateSummary Placement { get; init; } = new();
    public HealerGateSummary Healer { get; init; } = new();

    public sealed record PlacementGateSummary
    {
        public int ComparisonSetCount { get; init; }
        public double MedianLeverage { get; init; }
        public double SensitiveMedianLeverage { get; init; }
        public double LeverageP90 { get; init; }
        public double DefaultOptimalRate { get; init; }
    }

    public sealed record HealerGateSummary
    {
        public int ComparisonCount { get; init; }
        public int PositiveStateCount { get; init; }
        public int AlignedPositiveStateCount { get; init; }
        public double PositiveSelectionAlignmentRate { get; init; }
    }
}
