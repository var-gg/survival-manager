namespace SM.HeadlessMetrics;

/// <summary>placement attribution이 소비하는 전투 trace의 결정적 축약.</summary>
public sealed record PlacementTraceSummary(
    int FirstContactTick,
    double FirstContactDistance,
    string FirstTargetSignature,
    int TargetSwitchCount,
    int PathingReplanCount,
    double AllyTravelDistance,
    double ApproachStallRatio)
{
    public static PlacementTraceSummary Empty { get; } = new(-1, -1d, string.Empty, 0, 0, 0d, 0d);
}
