using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>배치만 바꾼 한 전투의 outcome, formation feature, 전술/거리/타게팅/pathing trace.</summary>
public sealed record PlacementAttributionBattleRecord
{
    public const string CurrentSchemaVersion = "placement-attribution-battle-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public string BattleId { get; init; } = string.Empty;
    public string PairingId { get; init; } = string.Empty;
    public string ComparisonKind { get; init; } = string.Empty;
    public string CompositionId { get; init; } = string.Empty;
    public string ConceptVariantId { get; init; } = string.Empty;
    public string EncounterFamilyId { get; init; } = string.Empty;
    public string ScenarioId { get; init; } = string.Empty;
    public int Seed { get; init; }
    public int BattleSeed { get; init; }
    public string PlacementVariantId { get; init; } = string.Empty;
    public bool IsBaseline { get; init; }
    public bool SemanticPreservationExpected { get; init; }
    public string FormationProfileId { get; init; } = string.Empty;
    public FormationFeatureSnapshot FormationFeatures { get; init; } = FormationFeatureSnapshot.Empty;
    public IReadOnlyList<int> AnchorIdsByMemberIndex { get; init; } = Array.Empty<int>();
    public string WinnerSide { get; init; } = "none";
    public float NormalizedFinalPowerDifference { get; init; }
    public float FixedStepSeconds { get; init; }
    public IReadOnlyList<ChannelTrace> Channels { get; init; } = Array.Empty<ChannelTrace>();
    public PlacementTraceSummary Trace { get; init; } = PlacementTraceSummary.Empty;
    public string FailureCode { get; init; } = string.Empty;

    public sealed record FormationFeatureSnapshot(
        int FrontlineCount,
        int ProtectedSlotCount,
        int SideExposureCount,
        int RearExposureCount,
        double FlankRearExposureScore,
        double SupportDistance,
        double BacklineAccessibility)
    {
        public static FormationFeatureSnapshot Empty { get; } = new(0, 0, 0, 0, 0d, 0d, 0d);
    }

    public sealed record ChannelTrace(
        string ChannelId,
        bool Eligible,
        int EventCount);
}
