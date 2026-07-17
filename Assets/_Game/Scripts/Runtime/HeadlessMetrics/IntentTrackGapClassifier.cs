namespace SM.HeadlessMetrics;

public static class IntentTrackGapKind
{
    public const string None = "none";
    public const string Surface = "surface_gap";
    public const string Agency = "agency_gap";
    public const string LeverPending = "lever_pending";
    public const string Policy = "policy_gap";
    public const string Combat = "combat_gap";
}

/// <summary>track, policy realization, E02 surface join, payoff witness를 상호배타적 실패 원인으로 내린다.</summary>
public static class IntentTrackGapClassifier
{
    public static string Classify(
        bool trackAvailable,
        bool leverPending,
        bool policyRealized,
        bool relevantSurfaceGap,
        bool payoffWitnessed)
    {
        if (policyRealized)
        {
            return payoffWitnessed ? IntentTrackGapKind.None : IntentTrackGapKind.Combat;
        }

        if (!trackAvailable)
        {
            return leverPending ? IntentTrackGapKind.LeverPending : IntentTrackGapKind.Agency;
        }

        return relevantSurfaceGap ? IntentTrackGapKind.Surface : IntentTrackGapKind.Policy;
    }
}
