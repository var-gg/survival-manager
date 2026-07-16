namespace SM.HeadlessMetrics;

/// <summary>세 영웅·적·시드를 고정하고 healer 한 명만 대체한 marginal-value 행.</summary>
public sealed record HealerMarginalValueRecord(
    string ComparisonId,
    int SeedCount,
    double WithHealerWinRate,
    double WithoutHealerWinRate,
    double WinRateDelta,
    double MeanPowerDifferenceDelta,
    double MarginalValue,
    bool PositiveMarginalValue,
    bool CompetentSelectedHealer,
    bool SelectionAligned);
