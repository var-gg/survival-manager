namespace SM.HeadlessCensus;

/// <summary>실 콘텐츠에서 파생된 단일 build-grammar 관계.</summary>
public sealed record BuildGrammarTruthEdge(
    string EdgeId,
    string SubjectKind,
    string SubjectId,
    string Relation,
    string TargetKind,
    string TargetId,
    string TruthValue,
    bool Actionable,
    bool FeedbackRequired,
    string ExpectedFeedbackWitness);

public static class BuildGrammarRelation
{
    public const string Produces = "produces";
    public const string Amplifies = "amplifies";
    public const string Requires = "requires";
    public const string PaysOff = "pays_off";
    public const string Conflicts = "conflicts";
    public const string Substitutes = "substitutes";
    public const string AcquiredBy = "acquired_by";
}
