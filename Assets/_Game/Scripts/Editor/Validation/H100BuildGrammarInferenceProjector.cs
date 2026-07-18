using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

/// <summary>
/// 실 truth graph + E02 player-visible surface를 BT4 채점기(<see cref="BuildGrammarInferenceScorer"/>) 입력으로
/// 조립하는 합성층 projector. no-cheat 경계: 스코어러에 넘기는 VisibleTokens/KnownFactRefs는 오직 player-visible
/// surface에서만 파생한다(truth graph 자체는 evaluator-only이며 recall 분모 계산에만 쓰이고 정책/LLM엔 비노출).
/// </summary>
internal static class H100BuildGrammarInferenceProjector
{
    public static BuildGrammarInferenceInput Project(
        BuildGrammarTruthGraph truthGraph,
        InformationSurfaceAuditInput surface,
        IReadOnlyList<BuildConceptProposal> proposals,
        int progressCutoffDecisionIndex,
        BuildGrammarInferenceCaptureSource captureSource)
    {
        if (truthGraph == null) throw new ArgumentNullException(nameof(truthGraph));
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var visibleTokens = VisibleTokenSet(surface);
        return new BuildGrammarInferenceInput(
            proposals ?? Array.Empty<BuildConceptProposal>(),
            truthGraph,
            visibleTokens,
            KnownFactRefs(surface),
            DeriveExposedConceptEdges(truthGraph, visibleTokens),
            progressCutoffDecisionIndex,
            captureSource);
    }

    /// <summary>
    /// 결정적 no-cheat 하한 baseline: "게임이 선택 전 가시적으로 말해주는 것만 그대로 주장하면 BT4가 얼마인가".
    /// player-visible semantic edge(gated relation·양 endpoint 가시)만 claim으로 승격 — 실 LLM 러너가 반드시
    /// 능가해야 할 바닥. truth graph 미참조(가시 표면만).
    /// </summary>
    public static BuildConceptProposal BuildVisibleSemanticBaseline(InformationSurfaceAuditInput surface)
    {
        if (surface == null) throw new ArgumentNullException(nameof(surface));

        var visibleTokens = new HashSet<string>(VisibleTokenSet(surface), StringComparer.Ordinal);
        var gated = new HashSet<string>(BuildGrammarInferenceScorer.GatedRelations, StringComparer.Ordinal);
        var claims = surface.VisibleSemantics
            .Where(semantic => semantic != null
                               && semantic.AvailableBeforeChoice
                               && gated.Contains(semantic.Relation))
            .Select(semantic => new BuildHypothesisClaim(
                semantic.SubjectKind,
                semantic.SubjectId,
                semantic.Relation,
                semantic.TargetKind,
                semantic.TargetId,
                string.IsNullOrWhiteSpace(semantic.SourceFactId)
                    ? Array.Empty<string>()
                    : new[] { semantic.SourceFactId },
                0.7d))
            .Where(claim => visibleTokens.Contains(claim.SubjectToken)
                            && visibleTokens.Contains(claim.TargetToken))
            .GroupBy(claim => claim.EdgeKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(claim => claim.EdgeKey, StringComparer.Ordinal)
            .ToArray();
        return new BuildConceptProposal("baseline-visible-semantic", claims, DeclaredAtDecisionIndex: 0, PayoffObservedAtDecisionIndex: -1);
    }

    // no-cheat 가시 어휘 = player가 참조할 수 있는 모든 entity(kind:id). 정의된 토큰(PlayerVisibleTokenUse)과
    // player-visible build-grammar edge의 endpoint(PlayerVisibleBuildGrammarSemantic)는 별개 vocabulary이므로
    // 둘의 합집합을 쓴다(실측: token-only는 edge endpoint를 배제해 claim 0). 어느 쪽에도 없는 entity 참조는
    // 여전히 guard가 거부(할루시네이션 방어 불변).
    private static string[] VisibleTokenSet(InformationSurfaceAuditInput surface)
    {
        var tokens = surface.VisibleTokens
            .Where(token => token != null
                            && !string.IsNullOrWhiteSpace(token.TokenKind)
                            && !string.IsNullOrWhiteSpace(token.TokenId))
            .Select(token => $"{token.TokenKind}:{token.TokenId}");
        var semanticEndpoints = surface.VisibleSemantics
            .Where(semantic => semantic != null)
            .SelectMany(semantic => new[]
            {
                (Kind: semantic.SubjectKind, Id: semantic.SubjectId),
                (Kind: semantic.TargetKind, Id: semantic.TargetId),
            })
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Kind)
                               && !string.IsNullOrWhiteSpace(endpoint.Id))
            .Select(endpoint => $"{endpoint.Kind}:{endpoint.Id}");
        return tokens
            .Concat(semanticEndpoints)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] KnownFactRefs(InformationSurfaceAuditInput surface)
        => surface.VisibleTokens.Select(token => token?.SourceFactId)
            .Concat(surface.VisibleSemantics.Select(semantic => semantic?.SourceFactId))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// BT4 recall 분모의 first-cut proxy: "핵심 학습가능 문법" = gated relation·actionable·feedback-required이며
    /// 양 endpoint가 player-visible한 truth edge. 2264 전 edge에 recall을 걸면 비현실이므로 core로 좁힌다.
    /// 후속에서 E03 컨셉 카탈로그 scope로 정밀화 예정(현 proxy는 truth graph 자체 유도라 fuzzy 매핑 없음).
    /// </summary>
    private static string[] DeriveExposedConceptEdges(
        BuildGrammarTruthGraph truthGraph,
        IReadOnlyCollection<string> visibleTokens)
    {
        var visible = new HashSet<string>(visibleTokens, StringComparer.Ordinal);
        var gated = new HashSet<string>(BuildGrammarInferenceScorer.GatedRelations, StringComparer.Ordinal);
        return truthGraph.Edges
            .Where(edge => edge.Actionable && edge.FeedbackRequired && gated.Contains(edge.Relation))
            .Where(edge => visible.Contains($"{edge.SubjectKind}:{edge.SubjectId}")
                           && visible.Contains($"{edge.TargetKind}:{edge.TargetId}"))
            .Select(edge => $"{edge.SubjectKind}:{edge.SubjectId}|{edge.Relation}|{edge.TargetKind}:{edge.TargetId}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
