namespace SM.HeadlessCensus;

/// <summary>truth graph edge에서 파생된 옵션의 단일 player-visible promise.</summary>
public sealed record OptionWitnessPromise(
    string PromiseId,
    string Relation,
    string TargetKind,
    string TargetId,
    string TruthValue,
    string ExpectedFeedbackWitness,
    string ExpectedDeltaDirection,
    double DeclaredMagnitude);

public static class OptionDeltaDirection
{
    public const string Positive = "positive";
    public const string Negative = "negative";
    public const string Zero = "zero";
    public const string Unknown = "unknown";
}
