using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>BT1-E09의 paired placement 귀속, Pro 4조건, anchor 지배, formation trap 후보 보고서.</summary>
public sealed record PlacementAttributionReport
{
    public const string CurrentSchemaVersion = "placement-attribution-report-bt1-e09-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public bool GoldenNeutral { get; init; } = true;
    public string Status { get; init; } = "technical_failure";
    public string Verdict { get; init; } = "insufficient_evidence";
    public MethodologySummary Methodology { get; init; } = new();
    public SampleSummary Sample { get; init; } = new();
    public ComponentSummary Components { get; init; } = new();
    public SemanticSwapSummary SemanticSwap { get; init; } = new();
    public IReadOnlyList<ProConditionResult> ProConditions { get; init; } = Array.Empty<ProConditionResult>();
    public IReadOnlyList<AnchorDominanceRow> AnchorDominance { get; init; } = Array.Empty<AnchorDominanceRow>();
    public IReadOnlyList<FormationOptionResult> FormationOptions { get; init; } = Array.Empty<FormationOptionResult>();
    public IReadOnlyList<PairAttributionRecord> PairAttributions { get; init; } = Array.Empty<PairAttributionRecord>();
    public IReadOnlyList<string> TechnicalFailures { get; init; } = Array.Empty<string>();

    public sealed record MethodologySummary
    {
        public string PairingControl { get; init; } = string.Empty;
        public string SemanticSwapRule { get; init; } = string.Empty;
        public string TacticalRule { get; init; } = string.Empty;
        public string TargetingRule { get; init; } = string.Empty;
        public string DistanceRule { get; init; } = string.Empty;
        public string PathingRule { get; init; } = string.Empty;
        public string PolicyNoiseRule { get; init; } = string.Empty;
        public string UnexplainedRule { get; init; } = string.Empty;
        public string BroadEvidenceRule { get; init; } = string.Empty;
        public string TrapCandidateRule { get; init; } = string.Empty;
        public string RightSizeNote { get; init; } = string.Empty;
    }

    public sealed record SampleSummary
    {
        public int BattleCount { get; init; }
        public int ValidBattleCount { get; init; }
        public int FailedBattleCount { get; init; }
        public int PairCount { get; init; }
        public int CompositionCount { get; init; }
        public int EncounterFamilyCount { get; init; }
        public int SeedCount { get; init; }
        public int SemanticSwapPairCount { get; init; }
        public int ProfileTransitionPairCount { get; init; }
        public int AnchorSweepPairCount { get; init; }
    }

    public sealed record ComponentSummary
    {
        public int MaterialPairCount { get; init; }
        public int NoMaterialDeltaPairCount { get; init; }
        public int TacticalPairCount { get; init; }
        public int RawDistancePairCount { get; init; }
        public int TargetingPairCount { get; init; }
        public int PathingPairCount { get; init; }
        public int PolicyNoisePairCount { get; init; }
        public int UnexplainedRawPairCount { get; init; }
        public double TacticalShare { get; init; }
        public double RawDistanceTargetingShare { get; init; }
        public double PathingShare { get; init; }
        public double PolicyNoiseShare { get; init; }
        public double UnexplainedRawShare { get; init; }
        public double PlayerVisibleExplainableShare { get; init; }
    }

    public sealed record SemanticSwapSummary
    {
        public int GroupCount { get; init; }
        public int RepeatedReversalGroupCount { get; init; }
        public int RepeatedReversalEncounterFamilyCount { get; init; }
        public double RepeatedReversalGroupRate { get; init; }
        public int FeatureInvariantViolationCount { get; init; }
        public double MedianAbsoluteWinRateDelta { get; init; }
    }

    public sealed record ProConditionResult
    {
        public string ConditionId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public bool Triggered { get; init; }
        public double ObservedValue { get; init; }
        public double Threshold { get; init; }
        public int SupportingGroupCount { get; init; }
        public int SupportingEncounterFamilyCount { get; init; }
        public string Evidence { get; init; } = string.Empty;
    }

    public sealed record AnchorDominanceRow
    {
        public int AnchorId { get; init; }
        public int UsedBattleCount { get; init; }
        public int UnusedBattleCount { get; init; }
        public double UsedWinRate { get; init; }
        public double UnusedWinRate { get; init; }
        public double WinRateDelta { get; init; }
        public double MedianStratumDelta { get; init; }
        public int EvaluableCompositionCount { get; init; }
        public int PositiveCompositionCount { get; init; }
        public int EvaluableEncounterFamilyCount { get; init; }
        public int PositiveEncounterFamilyCount { get; init; }
        public bool BuildIndependentDominance { get; init; }
    }

    public sealed record FormationOptionResult
    {
        public string ChannelId { get; init; } = string.Empty;
        public IReadOnlyList<string> IntendedProfileIds { get; init; } = Array.Empty<string>();
        public int StageFourEligibleCount { get; init; }
        public int StageFourFiredCount { get; init; }
        public int TacticalIntendedEligibleCount { get; init; }
        public int TacticalIntendedFiredCount { get; init; }
        public int PositiveWitnessCount { get; init; }
        public int TrackVariantCount { get; init; }
        public int TrackEvaluationCount { get; init; }
        public int TrackAvailableCount { get; init; }
        public int PolicyRealizedCount { get; init; }
        public int GenericPayoffWitnessCount { get; init; }
        public int PreviewFormationDecisionCount { get; init; }
        public int PreviewEvidenceSupportedCount { get; init; }
        public int EqualCostComparatorPairCount { get; init; }
        public double ComparatorNonWorseRate { get; init; }
        public double ComparatorStrictlyBetterRate { get; init; }
        public string NonUseReason { get; init; } = string.Empty;
        public bool TrapCandidate { get; init; }
    }

    public sealed record PairAttributionRecord
    {
        public string ComparisonId { get; init; } = string.Empty;
        public string PairingId { get; init; } = string.Empty;
        public string ComparisonKind { get; init; } = string.Empty;
        public string CompositionId { get; init; } = string.Empty;
        public string EncounterFamilyId { get; init; } = string.Empty;
        public int Seed { get; init; }
        public int BattleSeed { get; init; }
        public string BaselinePlacementVariantId { get; init; } = string.Empty;
        public string CandidatePlacementVariantId { get; init; } = string.Empty;
        public string BaselineProfileId { get; init; } = string.Empty;
        public string CandidateProfileId { get; init; } = string.Empty;
        public bool SemanticFeaturesPreserved { get; init; }
        public bool WinnerChanged { get; init; }
        public double AllyWinDelta { get; init; }
        public double NormalizedPowerDelta { get; init; }
        public bool MaterialOutcomeDelta { get; init; }
        public IReadOnlyList<ChannelDelta> ChannelDeltas { get; init; } = Array.Empty<ChannelDelta>();
        public double FirstContactTimeDelta { get; init; }
        public double FirstContactDistanceDelta { get; init; }
        public bool FirstTargetChanged { get; init; }
        public int TargetSwitchDelta { get; init; }
        public int PathingReplanDelta { get; init; }
        public double AllyTravelDistanceDelta { get; init; }
        public double ApproachStallRatioDelta { get; init; }
        public string Component { get; init; } = string.Empty;
        public bool PlayerVisibleExplainable { get; init; }
        public string Explanation { get; init; } = string.Empty;
    }

    public sealed record ChannelDelta(string ChannelId, int EventCountDelta);
}
