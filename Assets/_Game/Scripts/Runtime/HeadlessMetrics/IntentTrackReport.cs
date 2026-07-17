using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>BT6/BT7가 직접 소비할 수 있는 결정적 E05 report.</summary>
public sealed record IntentTrackReport
{
    public const string CurrentSchemaVersion = "intent-track-report-bt1-v2";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string EvaluatorVersion { get; init; } = string.Empty;
    public string ConfidenceInterval { get; init; } = "wilson_one_sided_95";
    public int SeedBase { get; init; }
    public int SeedCount { get; init; }
    public int OwnerAnchorCount { get; init; }
    public int SystemMedoidCatalogCount { get; init; }
    public int SystemMedoidSampleCount { get; init; }
    public IReadOnlyList<string> EnabledLeverIds { get; init; } = Array.Empty<string>();
    public string AgencyWindowDefinition { get; init; } = string.Empty;
    public string V1LeverCaveat { get; init; } = string.Empty;
    public string RightSizeNote { get; init; } = string.Empty;
    public IntentTrackPredicateCoverage PredicateCoverage { get; init; } = IntentTrackPredicateCoverage.Empty;
    public IReadOnlyList<IntentTrackTierSummary> TierSummaries { get; init; } = Array.Empty<IntentTrackTierSummary>();
    public IReadOnlyList<IntentTrackConceptSummary> OwnerAnchorSummaries { get; init; } = Array.Empty<IntentTrackConceptSummary>();
    public IReadOnlyList<IntentTrackConceptSummary> SystemMedoidSummaries { get; init; } = Array.Empty<IntentTrackConceptSummary>();
    public IReadOnlyList<IntentTrackCount> GapDistribution { get; init; } = Array.Empty<IntentTrackCount>();
    public double FalseHopeRate { get; init; }
    public IReadOnlyList<IntentTrackMetricValue> Bt6Metrics { get; init; } = Array.Empty<IntentTrackMetricValue>();
    public IReadOnlyList<IntentTrackMetricValue> Bt7Metrics { get; init; } = Array.Empty<IntentTrackMetricValue>();
    public string Bt6Status { get; init; } = "fail";
    public string Bt7Status { get; init; } = "fail";
    public IReadOnlyList<IntentTrackRunRecord> Runs { get; init; } = Array.Empty<IntentTrackRunRecord>();
}

public sealed record IntentTrackTierSummary(
    string AvailabilityTier,
    int RunCount,
    int TrackAvailableCount,
    double TrackAvailableRate,
    double TrackAvailableLcb95,
    int FirstProgressSampleCount,
    int FirstProgressP90,
    int AgencyDroughtP90,
    int StarvationCount,
    double StarvationRate);

public sealed record IntentTrackConceptSummary(
    string ConceptId,
    string ConceptKind,
    string AvailabilityTier,
    int RunCount,
    int TrackAvailableCount,
    double TrackAvailableRate,
    double TrackAvailableLcb95,
    int PolicyRealizedCount,
    int CaptureDenominator,
    double PolicyCaptureRate,
    double PolicyCaptureLcb95,
    int FirstProgressP90,
    int AgencyDroughtP90,
    int StarvationCount,
    double StarvationRate,
    double RealizedBeforeFinalTwentyPercentRate,
    int PayoffRunwayMin,
    int PayoffWitnessCount,
    int CounterDecisionCount,
    double IdentityRetentionAfterCounter,
    IReadOnlyList<IntentTrackCount> GapDistribution,
    bool Pass,
    int VariantCount,
    int V1LeverTrackSeedCount,
    int V1TrackVariantEvaluationCount,
    int LeverPendingVariantEvaluationCount,
    int TrueUnavailableVariantEvaluationCount,
    IReadOnlyList<IntentTrackCount> LeverPendingByLever,
    IReadOnlyList<IntentTrackVariantSummary> VariantSummaries);

public sealed record IntentTrackVariantSummary(
    string VariantId,
    string AvailabilityTier,
    int EvaluationCount,
    int V1TrackCount,
    int LeverPendingCount,
    int TrueUnavailableCount,
    IReadOnlyList<IntentTrackCount> LeverPendingByLever,
    IReadOnlyList<IntentTrackPredicateSummary> IdentityPredicates);

public sealed record IntentTrackPredicateSummary(
    string Predicate,
    string PredicateKind,
    int EvaluationCount,
    int SatisfiedCount);

public sealed record IntentTrackPredicateCoverage(
    int OwnerVariantCount,
    int SystemVariantCount,
    int UniqueIdentityPredicateCount,
    IReadOnlyList<string> PredicateKinds,
    int UnevaluablePredicateCount)
{
    public static IntentTrackPredicateCoverage Empty { get; } = new(
        0,
        0,
        0,
        Array.Empty<string>(),
        0);
}

public sealed record IntentTrackCount(string Id, int Count);

public sealed record IntentTrackMetricValue(string MetricId, double Value, int SampleCount);
