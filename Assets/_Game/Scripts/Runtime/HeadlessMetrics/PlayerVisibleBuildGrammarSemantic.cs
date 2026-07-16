namespace SM.HeadlessMetrics;

/// <summary>E01 fact가 실제로 노출한 구조 의미 한 건.</summary>
public sealed record PlayerVisibleBuildGrammarSemantic(
    string SourceFactId,
    string UiSource,
    string SubjectKind,
    string SubjectId,
    string Relation,
    string TargetKind,
    string TargetId,
    string VisibleValue,
    bool AvailableBeforeChoice);
