using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>한 placement 재실행에서 관측한 진형 eligibility/firing/legibility와 전투 outcome.</summary>
public sealed record FormationBattleRecord
{
    public const string CurrentSchemaVersion = "formation-battle-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public string BattleId { get; init; } = string.Empty;
    public string PairingId { get; init; } = string.Empty;
    public string PlacementSetId { get; init; } = string.Empty;
    public string PlacementVariantId { get; init; } = string.Empty;
    public string ScenarioId { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
    public int Seed { get; init; }
    public bool IsDefaultPlacement { get; init; }
    public bool IsPolicyChoice { get; init; }
    public string CoverageProbeChannelId { get; init; } = string.Empty;
    public bool IsHealerComparison { get; init; }
    public string HealerComparisonId { get; init; } = string.Empty;
    public bool ContainsHealer { get; init; }
    public bool CompetentSelectedHealer { get; init; }
    public string AllyFormationId { get; init; } = string.Empty;
    public string WinnerSide { get; init; } = "none";
    public float NormalizedFinalPowerDifference { get; init; }
    public bool Timeout { get; init; }
    public bool Stomp { get; init; }
    public string FailureCode { get; init; } = string.Empty;
    public IReadOnlyList<ChannelEvidence> Channels { get; init; } = Array.Empty<ChannelEvidence>();

    public sealed record ChannelEvidence(
        string ChannelId,
        bool Eligible,
        bool Fired,
        bool Legible,
        int EventCount,
        string Explanation);
}
