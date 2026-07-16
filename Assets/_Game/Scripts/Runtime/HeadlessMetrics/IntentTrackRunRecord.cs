namespace SM.HeadlessMetrics;

/// <summary>한 concept×seed campaign의 oracle·policy·payoff 결합 결과.</summary>
public sealed record IntentTrackRunRecord
{
    public string RunId { get; init; } = string.Empty;
    public string ConceptId { get; init; } = string.Empty;
    public string ConceptKind { get; init; } = string.Empty;
    public string AvailabilityTier { get; init; } = string.Empty;
    public int Seed { get; init; }
    public int AgencyWindowCount { get; init; }
    public int BattleCount { get; init; }
    public string OfferStreamHash { get; init; } = string.Empty;
    public bool TrackAvailable { get; init; }
    public int FirstProgressTime { get; init; } = -1;
    public int OracleRealizationTime { get; init; } = -1;
    public int MaxAgencyDrought { get; init; }
    public bool Starved { get; init; }
    public bool PolicyCommitted { get; init; }
    public bool PolicyRealized { get; init; }
    public int PolicyRealizationWindowIndex { get; init; } = -1;
    public bool RealizedBeforeFinalTwentyPercent { get; init; }
    public int PayoffRunway { get; init; }
    public bool PayoffWitnessed { get; init; }
    public int CounterDecisionCount { get; init; }
    public int IdentityRetainedCounterDecisionCount { get; init; }
    public bool WarningIssued { get; init; }
    public bool SilentDeadEnd { get; init; }
    public bool RelevantSurfaceGap { get; init; }
    public string GapKind { get; init; } = IntentTrackGapKind.None;
}
