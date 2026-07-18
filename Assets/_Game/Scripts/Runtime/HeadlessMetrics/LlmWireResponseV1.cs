using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>냉시작 LLM이 단일 decision seam에 반환하는 wire response.</summary>
public sealed record LlmDecisionResponseV1(
    string SelectedAction,
    LlmDeclaredIntentV1 DeclaredIntent,
    string IntentRef,
    IReadOnlyList<LlmBuildHypothesisV1> BuildHypotheses);

/// <summary>action 선택 전에 선언되어야 하는 build intent wire snapshot.</summary>
public sealed record LlmDeclaredIntentV1(
    string IntentId,
    IReadOnlyList<string> TrackTokenIds,
    string ExpectedPayoff,
    IReadOnlyList<string> EvidenceFactIds,
    string NextAcquisitionPlan,
    IReadOnlyList<string> AllowedSubstitutions,
    IReadOnlyList<string> PivotConditions,
    double Confidence);

/// <summary>
/// <c>SM.HeadlessCensus.BuildHypothesisClaim</c>의 edge shape를 값으로만 병렬 표현하는 wire DTO다.
/// sibling asmdef type을 직접 참조하지 않는다.
/// </summary>
public sealed record LlmBuildHypothesisV1(
    string SubjectKind,
    string SubjectId,
    string Relation,
    string TargetKind,
    string TargetId,
    IReadOnlyList<string> EvidenceRefs,
    double Confidence);

/// <summary>run 종료 시 BT10 fun scoring에 공급하는 LLM wire report.</summary>
public sealed record LlmRunReportResponseV1(
    string DesireRetrospective,
    string PayoffOrNearMiss,
    string NextConcept,
    IReadOnlyList<string> Complaints,
    IReadOnlyList<LlmEvaluationSentenceV1> EvaluationSentences,
    string RetryIntent);

/// <summary>평가 문장과 그 근거가 된 telemetry event id의 명시적 연결.</summary>
public sealed record LlmEvaluationSentenceV1(
    string Sentence,
    IReadOnlyList<string> TelemetryEventIds);
