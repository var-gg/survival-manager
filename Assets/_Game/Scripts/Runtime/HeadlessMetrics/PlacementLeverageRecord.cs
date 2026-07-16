namespace SM.HeadlessMetrics;

/// <summary>같은 편성·적에서 medoid variant 승률의 최적−기본 차이를 집계한 행.</summary>
public sealed record PlacementLeverageRecord(
    string PlacementSetId,
    string PolicyId,
    int SeedCount,
    int VariantCount,
    double DefaultWinRate,
    double BestWinRate,
    double WinRateLeverage,
    bool DefaultWasOptimal,
    bool FormationSensitive,
    string BestVariantId);
