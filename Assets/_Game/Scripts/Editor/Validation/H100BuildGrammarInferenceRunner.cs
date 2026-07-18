using System;
using System.Linq;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>
/// BT4 채점기를 실 콘텐츠(truth graph + E02 player-visible surface)에 붙여 검증하는 smoke 진입점.
/// 합성 적대 테스트(BuildGrammarInferenceScorerFastTests)가 채점 로직을, 이 러너가 실데이터 조립·no-cheat 정합을 증명한다.
/// 실 LLM 미투입 — 결정적 no-cheat 가시-의미 baseline으로 채점 경로를 관통시킨다(baseline은 실 러너가 능가할 하한).
/// </summary>
public static class H100BuildGrammarInferenceRunner
{
    public static void RunFromCli()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100BuildGrammarInferenceRunner));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var error))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {error}");
        }

        var truthGraph = H100BuildGrammarTruthProjector.Project(snapshot);
        var surface = H100BuildGrammarVisibleSurfaceProjector.Project(snapshot, truthGraph);
        var baseline = H100BuildGrammarInferenceProjector.BuildVisibleSemanticBaseline(surface);
        var input = H100BuildGrammarInferenceProjector.Project(
            truthGraph,
            surface,
            new[] { baseline },
            progressCutoffDecisionIndex: int.MaxValue,
            captureSource: BuildGrammarInferenceCaptureSource.SyntheticStandIn);
        var result = BuildGrammarInferenceScorer.Score(input);

        // 정합성 assert: 실 truth graph가 서고, no-cheat baseline은 guard를 깨지 않아야(projector의 VisibleTokens/
        // KnownFactRefs가 baseline claim 어휘와 정합함을 실데이터로 증명). recall 분모는 truth edge의 부분집합.
        if (truthGraph.Edges.Count == 0)
        {
            throw new InvalidOperationException("BT4 real-data smoke: truth graph is empty.");
        }

        if (surface.VisibleTokens.Count == 0)
        {
            throw new InvalidOperationException("BT4 real-data smoke: player-visible token surface is empty.");
        }

        if (result.GuardViolationCount != 0)
        {
            throw new InvalidOperationException(
                "BT4 real-data smoke: no-cheat visible-semantic baseline produced "
                + $"{result.GuardViolationCount} guard violations (projector token/fact 어휘 부정합). "
                + $"first={result.GuardViolations.FirstOrDefault()}");
        }

        if (result.CaptureSource != BuildGrammarInferenceCaptureSource.SyntheticStandIn
            || result.CertificationEligible)
        {
            throw new InvalidOperationException(
                "BT4 real-data smoke: visible-semantic baseline must remain a certification-ineligible SyntheticStandIn.");
        }

        var exposedFamilies = result.FamilyScores.Count(score => score.Exposed);
        var families = string.Join(
            " ",
            result.FamilyScores.Select(score => string.Format(
                "{0}[claim={1} correct={2} conceptTrue={3} P={4:F3} R={5:F3}{6}]",
                score.Relation,
                score.ClaimedCount,
                score.CorrectCount,
                score.ConceptTrueCount,
                score.Precision,
                score.Recall,
                score.Exposed ? " exposed" : string.Empty)));
        Debug.Log(
            $"[H100BuildGrammarInference] truthEdges={truthGraph.Edges.Count} visibleTokens={surface.VisibleTokens.Count} "
            + $"baselineClaims={baseline.Claims.Count} admissible={result.AdmissibleClaimCount} guardViolations={result.GuardViolationCount} "
            + $"captureSource={result.CaptureSource} certificationEligible={result.CertificationEligible} "
            + $"exposedFamilies={exposedFamilies} precisionMin={result.PrecisionMin:F4} recallMin={result.RecallMin:F4} "
            + $"validConcepts={result.ValidConceptCount}");
        Debug.Log($"[H100BuildGrammarInference] families: {families}");
    }
}
