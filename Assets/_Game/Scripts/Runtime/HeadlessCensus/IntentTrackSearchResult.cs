using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>identity 도달을 목표로 한 track oracle의 결정적 최단 경로 결과.</summary>
public sealed record IntentTrackSearchResult(
    string EvaluatorVersion,
    bool TrackAvailable,
    int FirstProgressTime,
    int RealizationTime,
    int RealizationWindowIndex,
    int AgencyWindowCount,
    int MaxAgencyDrought,
    bool Starved,
    int TargetIdentityPredicateCount,
    int FinalIdentityPredicateCount,
    IReadOnlyList<string> ChoicePath,
    IReadOnlyList<IntentTrackIdentityPredicateResult> IdentityPredicateResults)
{
    public const string CurrentEvaluatorVersion = "intent-track-oracle-bt1-v2";
}

/// <summary>선택된 실현 경로 또는 최선 near-miss 상태에서의 identity 술어별 명시 판정.</summary>
public sealed record IntentTrackIdentityPredicateResult(
    string Predicate,
    string PredicateKind,
    bool Satisfied);
