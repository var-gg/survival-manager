using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>BT4 빌드 문법 유추 채점 입력. 모든 truth는 evaluator-only이며 정책에 비노출.</summary>
public sealed record BuildGrammarInferenceInput(
    IReadOnlyList<BuildConceptProposal> Proposals,
    BuildGrammarTruthGraph TruthGraph,
    IReadOnlyCollection<string> VisibleTokens,
    IReadOnlyCollection<string> KnownFactRefs,
    IReadOnlyCollection<string> ExposedConceptEdgeKeys,
    int ProgressCutoffDecisionIndex);

/// <summary>relation(문법군)별 precision/recall 스코어.</summary>
public sealed record BuildGrammarFamilyScore(
    string Relation,
    int ClaimedCount,
    int CorrectCount,
    int ConceptTrueCount,
    int ConceptCorrectCount,
    double Precision,
    double Recall,
    bool Exposed);

/// <summary>한 냉시작 run의 BT4 채점 결과(집계 게이트는 상위 layer).</summary>
public sealed record BuildGrammarInferenceResult(
    int ClaimCount,
    int AdmissibleClaimCount,
    int GuardViolationCount,
    IReadOnlyList<string> GuardViolations,
    IReadOnlyList<BuildGrammarFamilyScore> FamilyScores,
    double PrecisionMin,
    double RecallMin,
    int ValidConceptCount);

/// <summary>
/// 냉시작 LLM의 build 가설을 <see cref="BuildGrammarTruthGraph"/>에 대조해 문법군별 precision/recall과
/// 유효 컨셉 수를 결정적으로 산출한다. no-cheat 계약: player-visible token 밖 endpoint나 미해결 증거를 인용한
/// claim은 guard 위반으로 채점에서 배제(환각/치팅 거부). 매칭은 정확 (kind:id) token-pair — fuzzy 금지.
/// </summary>
public static class BuildGrammarInferenceScorer
{
    /// <summary>precision/recall 게이트 대상인 build-constructive relation(문법군). 부정/메타 관계는 diagnostic.</summary>
    public static readonly IReadOnlyList<string> GatedRelations = new[]
    {
        BuildGrammarRelation.Produces,
        BuildGrammarRelation.Amplifies,
        BuildGrammarRelation.Requires,
        BuildGrammarRelation.PaysOff,
    };

    private static readonly HashSet<string> SystemKinds = new(StringComparer.Ordinal)
    {
        "skill", "status", "stat", "tag", "team_rule", "passive_node", "combat_effect",
    };

    public static BuildGrammarInferenceResult Score(BuildGrammarInferenceInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.TruthGraph == null) throw new ArgumentException("TruthGraph is required.", nameof(input));

        var visible = new HashSet<string>(input.VisibleTokens ?? Array.Empty<string>(), StringComparer.Ordinal);
        var knownFacts = new HashSet<string>(input.KnownFactRefs ?? Array.Empty<string>(), StringComparer.Ordinal);
        var conceptEdges = new HashSet<string>(input.ExposedConceptEdgeKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        var proposals = input.Proposals ?? Array.Empty<BuildConceptProposal>();

        // 게이트 대상 relation의 실 truth edge를 canonical key로 집계(actionable만).
        var gated = new HashSet<string>(GatedRelations, StringComparer.Ordinal);
        var trueEdgeKeysByRelation = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var relation in GatedRelations)
        {
            trueEdgeKeysByRelation[relation] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var edge in input.TruthGraph.Edges)
        {
            if (!edge.Actionable || !gated.Contains(edge.Relation))
            {
                continue;
            }

            trueEdgeKeysByRelation[edge.Relation].Add(EdgeKey(edge));
        }

        var guardViolations = new List<string>();
        var admissibleByRelation = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var relation in GatedRelations)
        {
            admissibleByRelation[relation] = new HashSet<string>(StringComparer.Ordinal);
        }

        var claimCount = 0;
        var admissibleClaimCount = 0;
        var validConceptCount = 0;

        foreach (var proposal in proposals.OrderBy(value => value.ProposalId, StringComparer.Ordinal))
        {
            var proposalAdmissible = new List<BuildHypothesisClaim>();
            foreach (var claim in proposal.Claims ?? Array.Empty<BuildHypothesisClaim>())
            {
                claimCount++;
                var violation = GuardViolation(claim, visible, knownFacts);
                if (violation != null)
                {
                    guardViolations.Add($"{proposal.ProposalId}:{claim.EdgeKey}:{violation}");
                    continue;
                }

                admissibleClaimCount++;
                proposalAdmissible.Add(claim);
                if (admissibleByRelation.TryGetValue(claim.Relation, out var set))
                {
                    set.Add(claim.EdgeKey);
                }
            }

            if (IsValidConcept(proposal, proposalAdmissible, conceptEdges, trueEdgeKeysByRelation, input.ProgressCutoffDecisionIndex))
            {
                validConceptCount++;
            }
        }

        var familyScores = new List<BuildGrammarFamilyScore>();
        var precisionValues = new List<double>();
        var recallValues = new List<double>();
        foreach (var relation in GatedRelations)
        {
            var claimed = admissibleByRelation[relation];
            var trueAll = trueEdgeKeysByRelation[relation];
            var conceptTrue = new HashSet<string>(trueAll.Where(conceptEdges.Contains), StringComparer.Ordinal);
            var correct = claimed.Count(trueAll.Contains);
            var conceptCorrect = claimed.Count(conceptTrue.Contains);
            var exposed = conceptTrue.Count > 0;

            var precision = claimed.Count > 0 ? (double)correct / claimed.Count : double.NaN;
            var recall = conceptTrue.Count > 0 ? (double)conceptCorrect / conceptTrue.Count : double.NaN;

            familyScores.Add(new BuildGrammarFamilyScore(
                relation,
                claimed.Count,
                correct,
                conceptTrue.Count,
                conceptCorrect,
                double.IsNaN(precision) ? 0d : precision,
                double.IsNaN(recall) ? 0d : recall,
                exposed));

            if (claimed.Count > 0)
            {
                precisionValues.Add(precision);
            }

            if (exposed)
            {
                recallValues.Add(double.IsNaN(recall) ? 0d : recall);
            }
        }

        var precisionMin = precisionValues.Count > 0 ? precisionValues.Min() : 0d;
        var recallMin = recallValues.Count > 0 ? recallValues.Min() : 0d;

        return new BuildGrammarInferenceResult(
            claimCount,
            admissibleClaimCount,
            guardViolations.Count,
            guardViolations,
            familyScores,
            precisionMin,
            recallMin,
            validConceptCount);
    }

    private static string EdgeKey(BuildGrammarTruthEdge edge)
        => $"{edge.SubjectKind}:{edge.SubjectId}|{edge.Relation}|{edge.TargetKind}:{edge.TargetId}";

    private static string? GuardViolation(
        BuildHypothesisClaim claim,
        HashSet<string> visible,
        HashSet<string> knownFacts)
    {
        if (claim == null)
        {
            return "null_claim";
        }

        if (!visible.Contains(claim.SubjectToken))
        {
            return $"off_surface_subject({claim.SubjectToken})";
        }

        if (!visible.Contains(claim.TargetToken))
        {
            return $"off_surface_target({claim.TargetToken})";
        }

        foreach (var evidence in claim.EvidenceRefs ?? Array.Empty<string>())
        {
            if (!knownFacts.Contains(evidence))
            {
                return $"unresolved_evidence({evidence})";
            }
        }

        return null;
    }

    private static bool IsValidConcept(
        BuildConceptProposal proposal,
        IReadOnlyList<BuildHypothesisClaim> admissible,
        HashSet<string> conceptEdges,
        IReadOnlyDictionary<string, HashSet<string>> trueEdgeKeysByRelation,
        int progressCutoffDecisionIndex)
    {
        // (c) payoff 전 선언 + (d) 진행도 30% 이내.
        if (!proposal.DeclaredBeforePayoff)
        {
            return false;
        }

        if (proposal.DeclaredAtDecisionIndex > progressCutoffDecisionIndex)
        {
            return false;
        }

        if (admissible.Count == 0)
        {
            return false;
        }

        // (b) 실 fact로 resolve된 증거 ≥2(guard 통과 claim의 증거만 집계).
        var evidenceCount = admissible
            .SelectMany(claim => claim.EvidenceRefs ?? Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (evidenceCount < 2)
        {
            return false;
        }

        // (a) ≥2 distinct 시스템 연결(claim endpoint kind 합집합).
        var systems = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in admissible)
        {
            if (SystemKinds.Contains(claim.SubjectKind))
            {
                systems.Add(claim.SubjectKind);
            }

            if (SystemKinds.Contains(claim.TargetKind))
            {
                systems.Add(claim.TargetKind);
            }
        }

        if (systems.Count < 2)
        {
            return false;
        }

        // (e) 순수 환각이 아님 — 최소 1개 admissible claim이 실 truth edge(노출 컨셉이면 가산점 아님, 진위만).
        var anyTrue = admissible.Any(claim =>
            trueEdgeKeysByRelation.TryGetValue(claim.Relation, out var trueSet)
            && trueSet.Contains(claim.EdgeKey));
        return anyTrue;
    }
}
