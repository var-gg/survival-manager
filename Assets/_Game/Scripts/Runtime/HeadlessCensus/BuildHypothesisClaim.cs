using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>
/// 냉시작 LLM 베타테스터가 payoff 관측 전에 선언한 단일 build-grammar 가설 edge의 census-native 투영.
/// 정책 assembly의 <c>SM.HeadlessPolicies.BuildHypothesis</c>는 census를 볼 수 없으므로, Editor 합성층이
/// 자연어 edge를 canonical (kind:id)로 정규화해 이 형태로 실어 evaluator에 넘긴다(no-cheat 경계 유지).
/// </summary>
public sealed record BuildHypothesisClaim(
    string SubjectKind,
    string SubjectId,
    string Relation,
    string TargetKind,
    string TargetId,
    IReadOnlyList<string> EvidenceRefs,
    double Confidence)
{
    /// <summary>truth graph edge와 대조하는 canonical key. endpoint는 player-visible token 어휘(kind:id)로 표현.</summary>
    public string EdgeKey => $"{SubjectKind}:{SubjectId}|{Relation}|{TargetKind}:{TargetId}";

    public string SubjectToken => $"{SubjectKind}:{SubjectId}";

    public string TargetToken => $"{TargetKind}:{TargetId}";
}

/// <summary>
/// 하나의 build 컨셉으로 묶인 가설 edge 집합. 유효 컨셉 판정(≥2 시스템 연결·증거≥2·payoff 전 선언·진행도 30% 이내)의 단위.
/// </summary>
public sealed record BuildConceptProposal(
    string ProposalId,
    IReadOnlyList<BuildHypothesisClaim> Claims,
    int DeclaredAtDecisionIndex,
    int PayoffObservedAtDecisionIndex)
{
    /// <summary>payoff 관측 전에 선언됐는가(사후 합리화 차단). payoff 미관측(-1)이면 항상 선언 선행으로 본다.</summary>
    public bool DeclaredBeforePayoff
        => PayoffObservedAtDecisionIndex < 0 || DeclaredAtDecisionIndex < PayoffObservedAtDecisionIndex;
}
