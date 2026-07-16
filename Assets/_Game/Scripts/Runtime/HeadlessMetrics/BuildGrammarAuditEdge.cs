namespace SM.HeadlessMetrics;

/// <summary>Census graph를 sibling 참조 없이 metrics 경계로 전달하는 순수 edge DTO.</summary>
public sealed record BuildGrammarAuditEdge(
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
