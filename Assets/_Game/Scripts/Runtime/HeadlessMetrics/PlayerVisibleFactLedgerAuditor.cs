using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>BT2 네 metric과 ledger 표본 수를 보존하는 순수 감사 결과.</summary>
public sealed record PlayerVisibleFactAuditResult(
    int FactCount,
    int DecisionCount,
    int PostDecisionInformationReferenceCount,
    int NonUiSemanticInternalFieldReferenceCount,
    int OracleOrTruthLeakCount,
    int UnsupportedCertainClaimCount)
{
    public IReadOnlyList<H100GateEvaluator.ExternalObservation> ToBt2Observations()
        => new[]
        {
            new H100GateEvaluator.ExternalObservation(
                "post_decision_information_reference_count",
                PostDecisionInformationReferenceCount,
                DecisionCount,
                "player_visible_fact_ledger.jsonl observed_at join"),
            new H100GateEvaluator.ExternalObservation(
                "non_ui_semantic_internal_field_reference_count",
                NonUiSemanticInternalFieldReferenceCount,
                FactCount,
                "player-visible ui_source whitelist and internal semantic scan"),
            new H100GateEvaluator.ExternalObservation(
                "oracle_or_truth_leak_count",
                OracleOrTruthLeakCount,
                FactCount + DecisionCount,
                "fact and decision provenance forbidden reference scan"),
            new H100GateEvaluator.ExternalObservation(
                "unsupported_certain_claim_count",
                UnsupportedCertainClaimCount,
                DecisionCount,
                "certain decision claims with resolvable prior evidence"),
        };
}

/// <summary>fact 존재·시점·UI 의미·금지 참조를 검사하고 decision join을 fail closed한다.</summary>
public static class PlayerVisibleFactLedgerAuditor
{
    private static readonly string[] InternalSemanticTokens =
    {
        "internal_field",
        "resolved_enemy_stat",
        "rng_state",
        "next_roll",
        "future_node",
        "unrevealed_node",
    };

    private static readonly string[] ForbiddenReferenceTokens =
    {
        "oracle",
        "truth_graph",
        "build_grammar_truth",
        "census_result",
        "future_offer",
    };

    public static PlayerVisibleFactAuditResult Audit(
        IReadOnlyList<PlayerVisibleFactRecord> facts,
        IReadOnlyList<PlayerVisibleDecisionRecord> decisions)
    {
        facts ??= Array.Empty<PlayerVisibleFactRecord>();
        decisions ??= Array.Empty<PlayerVisibleDecisionRecord>();
        var postDecision = 0;
        var unsupported = 0;
        foreach (var decision in decisions)
        {
            var references = decision.EvidenceRefs ?? Array.Empty<PlayerVisibleEvidenceRef>();
            var unsupportedDecision = references.Count == 0;
            foreach (var reference in references)
            {
                var matching = facts.Where(fact =>
                        string.Equals(fact.CampaignId, decision.CampaignId, StringComparison.Ordinal)
                        && string.Equals(fact.FactId, reference.FactId, StringComparison.Ordinal))
                    .ToArray();
                if (matching.Length == 0)
                {
                    unsupportedDecision = true;
                    continue;
                }

                if (!matching.Any(fact => fact.ObservedAt.CompareTo(decision.DecidedAt) <= 0))
                {
                    postDecision++;
                    unsupportedDecision = true;
                }
            }

            if (string.Equals(decision.Confidence, "certain", StringComparison.Ordinal)
                && unsupportedDecision)
            {
                unsupported++;
            }
        }

        var knownUiSources = PlayerVisibleUiSource.All.ToHashSet(StringComparer.Ordinal);
        var nonUi = facts
            .Where(fact => !knownUiSources.Contains(fact.UiSource) || ContainsAny(fact, InternalSemanticTokens))
            .Select(fact => fact.FactId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var leaks = facts.Where(fact => ContainsAny(fact, ForbiddenReferenceTokens))
            .Select(fact => fact.FactId)
            .Concat(decisions.Where(decision => ContainsAny(decision, ForbiddenReferenceTokens))
                .Select(decision => decision.DecisionId))
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new PlayerVisibleFactAuditResult(
            facts.Count,
            decisions.Count,
            postDecision,
            nonUi,
            leaks,
            unsupported);
    }

    public static void ValidateDecision(
        IReadOnlyList<PlayerVisibleFactRecord> facts,
        PlayerVisibleDecisionRecord decision)
    {
        if (decision == null)
        {
            throw new ArgumentNullException(nameof(decision));
        }

        if (decision.EvidenceRefs == null || decision.EvidenceRefs.Count == 0)
        {
            throw new PlayerVisibleProvenanceException(
                $"Decision '{decision.DecisionId}' has no player-visible evidence references.");
        }

        if (decision.EvidenceRefs.Any(reference => reference == null || string.IsNullOrWhiteSpace(reference.FactId))
            || decision.EvidenceRefs.Select(reference => reference.FactId).Distinct(StringComparer.Ordinal).Count()
            != decision.EvidenceRefs.Count)
        {
            throw new PlayerVisibleProvenanceException(
                $"Decision '{decision.DecisionId}' has blank or duplicate evidence references.");
        }

        foreach (var reference in decision.EvidenceRefs)
        {
            var matching = facts.Where(fact =>
                    string.Equals(fact.CampaignId, decision.CampaignId, StringComparison.Ordinal)
                    && string.Equals(fact.FactId, reference.FactId, StringComparison.Ordinal))
                .ToArray();
            if (matching.Any(fact => fact.ObservedAt.CompareTo(decision.DecidedAt) <= 0))
            {
                continue;
            }

            if (matching.Length > 0)
            {
                throw new PlayerVisibleProvenanceException(
                    $"Decision '{decision.DecisionId}' references post-decision fact '{reference.FactId}'.");
            }

            throw new PlayerVisibleProvenanceException(
                $"Decision '{decision.DecisionId}' references unknown fact '{reference.FactId}'.");
        }
    }

    private static bool ContainsAny(PlayerVisibleFactRecord fact, IEnumerable<string> tokens)
        => ContainsAny(
            string.Join("|", fact.UiSource, fact.Subject, fact.Verb, fact.Target, fact.Condition,
                fact.StackOrThreshold, fact.AcquisitionHint, fact.SourceText),
            tokens);

    private static bool ContainsAny(PlayerVisibleDecisionRecord decision, IEnumerable<string> tokens)
        => ContainsAny(
            string.Join("|", decision.PolicyId, decision.DecisionKind, decision.Action, decision.Rationale),
            tokens);

    private static bool ContainsAny(string value, IEnumerable<string> tokens)
        => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}

/// <summary>ledger join이 결정 시점 이전 증거를 입증하지 못한 경우의 fail-closed 예외.</summary>
public sealed class PlayerVisibleProvenanceException : InvalidOperationException
{
    public PlayerVisibleProvenanceException(string message)
        : base(message)
    {
    }
}
