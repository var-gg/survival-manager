using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>오너가 별도 파일에서 제공하는 BT10 two-key 승인.</summary>
public sealed record OwnerApprovalArtifact(
    bool Approved,
    string Statement,
    string ApprovedOn,
    IReadOnlyList<string> BoundTraceManifestHashes,
    string SourcePath);

public sealed record Bt10PayoffEvidence(bool Qualifies, IReadOnlyList<string> MentionedIds);

public sealed record Bt10NextConceptEvidence(
    bool Qualifies,
    IReadOnlyList<string> MentionedIds,
    IReadOnlyList<string> NovelIds);

public sealed record Bt10RunReportExcerpts(
    string DesireRetrospective,
    string PayoffOrNearMiss,
    string NextConcept,
    string RetryIntent);

public sealed record Bt10RunEvidence(
    string RunId,
    bool Valid,
    string ValidityFailure,
    bool DesireFormed,
    bool EvidenceGroundedCommit,
    IReadOnlyList<Bt5CommitWitness> Commits,
    Bt10PayoffEvidence PayoffOrNearMiss,
    Bt10NextConceptEvidence NextConceptNamed,
    IReadOnlyList<string> ComplaintsNormalized,
    IReadOnlyList<string> ComplaintsRaw,
    int SentenceUnitCount,
    int ResolvedSentenceCount,
    IReadOnlyList<string> UnresolvedTelemetryIds,
    Bt10RunReportExcerpts RunReportExcerpts);

/// <summary>BT10 여덟 gate metric과 owner-readable run report witness.</summary>
public sealed record Bt10Aggregate(
    int SuppliedRunCount,
    int ValidRunCount,
    int DesireFormedRunCount,
    int EvidenceGroundedCommitRunCount,
    int PayoffOrLegibleNearMissRunCount,
    int NextConceptNamedRunCount,
    int ComplaintRepeatedTwiceCount,
    int DistinctComplaintCount,
    double EvaluationSentenceTelemetryLinkRate,
    int EvaluationSentenceUnitCount,
    int OwnerApproval,
    int OwnerApprovalSampleCount,
    bool CohortConsistent,
    string CohortFailureReason,
    IReadOnlyList<Bt10RunEvidence> PerRun,
    IReadOnlyList<string> RepeatedComplaints)
{
    public IReadOnlyList<H100GateEvaluator.ExternalObservation> ToBt10Observations()
        => new[]
        {
            new H100GateEvaluator.ExternalObservation(
                "cold_start_run_count",
                ValidRunCount,
                SuppliedRunCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt10-witness.json valid_runs={0}/{1} cohort_consistent={2} failure={3}",
                    ValidRunCount,
                    SuppliedRunCount,
                    CohortConsistent,
                    CohortFailureReason)),
            CountObservation("desire_formed_run_count", DesireFormedRunCount, ValidRunCount),
            CountObservation(
                "evidence_grounded_commit_run_count",
                EvidenceGroundedCommitRunCount,
                ValidRunCount),
            CountObservation(
                "payoff_or_legible_near_miss_run_count",
                PayoffOrLegibleNearMissRunCount,
                ValidRunCount),
            CountObservation("next_concept_named_run_count", NextConceptNamedRunCount, ValidRunCount),
            new H100GateEvaluator.ExternalObservation(
                "complaint_repeated_twice_count",
                ComplaintRepeatedTwiceCount,
                DistinctComplaintCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt10-witness.json repeated={0} distinct={1} values={2}",
                    ComplaintRepeatedTwiceCount,
                    DistinctComplaintCount,
                    string.Join("|", RepeatedComplaints ?? Array.Empty<string>()))),
            new H100GateEvaluator.ExternalObservation(
                "evaluation_sentence_telemetry_link_rate",
                EvaluationSentenceTelemetryLinkRate,
                EvaluationSentenceUnitCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt10-witness.json resolved_sentences={0}/{1}",
                    CohortConsistent
                        ? (PerRun ?? Array.Empty<Bt10RunEvidence>()).Sum(run => run.ResolvedSentenceCount)
                        : 0,
                    EvaluationSentenceUnitCount)),
            new H100GateEvaluator.ExternalObservation(
                "owner_approval",
                OwnerApproval,
                OwnerApprovalSampleCount,
                OwnerApproval == 1
                    ? "owner-approval.json approved and exactly bound to six current trace manifests"
                    : "pending owner two-key; machine-underivable by design"),
        };

    private static H100GateEvaluator.ExternalObservation CountObservation(
        string metricId,
        int value,
        int sampleCount)
        => new(
            metricId,
            value,
            sampleCount,
            string.Format(
                CultureInfo.InvariantCulture,
                "bt10-witness.json {0}={1}/{2}",
                metricId,
                value,
                sampleCount));
}

public static class Bt10FunRetryScorer
{
    public static Bt10Aggregate Score(
        IReadOnlyList<SealedScoredRunInput> inputs,
        OwnerApprovalArtifact ownerApproval = null,
        string expectedPromptSchemaHash = null)
    {
        var evaluated = SealedDesireCommitEvaluation.Evaluate(inputs, expectedPromptSchemaHash);
        var perRun = evaluated.Runs.Select(EvaluateRun).ToArray();
        var cohortConsistent = evaluated.CohortConsistent;
        var validCount = cohortConsistent ? evaluated.Runs.Count(run => run.Valid) : 0;
        var desireCount = cohortConsistent ? perRun.Count(run => run.DesireFormed) : 0;
        var commitCount = cohortConsistent ? perRun.Count(run => run.EvidenceGroundedCommit) : 0;
        var payoffCount = cohortConsistent ? perRun.Count(run => run.PayoffOrNearMiss.Qualifies) : 0;
        var nextCount = cohortConsistent ? perRun.Count(run => run.NextConceptNamed.Qualifies) : 0;

        var complaintRuns = perRun
            .SelectMany(run => run.ComplaintsNormalized
                .Distinct(StringComparer.Ordinal)
                .Select(complaint => (run.RunId, Complaint: complaint)))
            .ToArray();
        var distinctComplaintCount = cohortConsistent
            ? complaintRuns.Select(value => value.Complaint).Distinct(StringComparer.Ordinal).Count()
            : 0;
        var repeated = cohortConsistent
            ? complaintRuns.GroupBy(value => value.Complaint, StringComparer.Ordinal)
                .Where(group => group.Select(value => value.RunId).Distinct(StringComparer.Ordinal).Count() >= 2)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

        var sentenceUnits = cohortConsistent ? perRun.Sum(run => run.SentenceUnitCount) : 0;
        var resolvedSentences = cohortConsistent ? perRun.Sum(run => run.ResolvedSentenceCount) : 0;
        var linkRate = sentenceUnits == 0 ? 0d : (double)resolvedSentences / sentenceUnits;
        var ownerValue = cohortConsistent && OwnerApprovalMatches(evaluated, ownerApproval) ? 1 : 0;

        return new Bt10Aggregate(
            evaluated.SuppliedRunCount,
            validCount,
            desireCount,
            commitCount,
            payoffCount,
            nextCount,
            repeated.Length,
            distinctComplaintCount,
            linkRate,
            sentenceUnits,
            ownerValue,
            ownerApproval == null ? 0 : 1,
            cohortConsistent,
            evaluated.CohortFailureReason,
            perRun,
            repeated);
    }

    private static Bt10RunEvidence EvaluateRun(SealedDesireCommitRunEvaluation evaluation)
    {
        var run = evaluation.Input?.Run;
        var report = run?.RunReport;
        var desireFormed = evaluation.Valid
                           && evaluation.SpontaneousIntent
                           && SealedWireSubstanceRules.Substantive(report?.DesireRetrospective);
        var evidenceGroundedCommit = evaluation.Valid && evaluation.Commits.Count > 0;
        var payoffMentions = report == null
            ? Array.Empty<string>()
            : SealedWireSubstanceRules.Mentions(
                report.PayoffOrNearMiss,
                evaluation.PursuedIds);
        var payoffQualifies = evidenceGroundedCommit
                              && SealedWireSubstanceRules.Substantive(report?.PayoffOrNearMiss)
                              && payoffMentions.Count > 0;

        var nextMentions = report == null || !evaluation.VisibleUniverseAvailable
            ? Array.Empty<string>()
            : SealedWireSubstanceRules.Mentions(report.NextConcept, evaluation.VisibleIds);
        var tracked = new HashSet<string>(evaluation.AllDeclaredTrackIds, StringComparer.Ordinal);
        var novelIds = nextMentions
            .Where(id => !tracked.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var nextQualifies = evaluation.Valid
                            && SealedWireSubstanceRules.Substantive(report?.NextConcept)
                            && SealedWireSubstanceRules.Substantive(report?.RetryIntent)
                            && nextMentions.Count > 0
                            && novelIds.Length > 0;

        var rawComplaints = report?.Complaints?.ToArray() ?? Array.Empty<string>();
        var normalizedComplaints = rawComplaints
            .Select(SealedWireSubstanceRules.Normalize)
            .Where(SealedWireSubstanceRules.Substantive)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var sentenceResult = EvaluateSentences(evaluation);

        return new Bt10RunEvidence(
            run?.Shape?.RunId ?? string.Empty,
            evaluation.Valid,
            run?.Shape?.FailureReason ?? "decoded_run_missing",
            desireFormed,
            evidenceGroundedCommit,
            evaluation.Commits,
            new Bt10PayoffEvidence(payoffQualifies, payoffMentions),
            new Bt10NextConceptEvidence(nextQualifies, nextMentions, novelIds),
            normalizedComplaints,
            rawComplaints,
            sentenceResult.UnitCount,
            sentenceResult.ResolvedCount,
            sentenceResult.UnresolvedIds,
            new Bt10RunReportExcerpts(
                report?.DesireRetrospective ?? string.Empty,
                report?.PayoffOrNearMiss ?? string.Empty,
                report?.NextConcept ?? string.Empty,
                report?.RetryIntent ?? string.Empty));
    }

    private static SentenceResult EvaluateSentences(SealedDesireCommitRunEvaluation evaluation)
    {
        if (!evaluation.Valid)
        {
            return new SentenceResult(1, 0, new[] { "invalid_run_phantom" });
        }

        var sentences = evaluation.Input.Run.RunReport?.EvaluationSentences
                        ?? Array.Empty<LlmEvaluationSentenceV1>();
        if (sentences.Count == 0)
        {
            return new SentenceResult(1, 0, new[] { "missing_sentence_phantom" });
        }

        var ledgerUsable = TryTelemetryUniverse(
            evaluation.Input,
            evaluation.Input.Run.Shape.RunId,
            out var telemetryUniverse);
        var resolved = 0;
        var unresolved = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sentences.Count; index++)
        {
            var sentence = sentences[index];
            var ids = sentence?.TelemetryEventIds ?? Array.Empty<string>();
            var sentenceResolved = ledgerUsable
                                   && sentence != null
                                   && SealedWireSubstanceRules.Substantive(sentence.Sentence)
                                   && ids.Count > 0
                                   && ids.All(telemetryUniverse.Contains);
            if (sentenceResolved)
            {
                resolved++;
                continue;
            }

            if (!ledgerUsable)
            {
                unresolved.Add("ledger_unavailable");
            }

            if (sentence == null || !SealedWireSubstanceRules.Substantive(sentence.Sentence))
            {
                unresolved.Add($"sentence_{index}_not_substantive");
            }

            if (ids.Count == 0)
            {
                unresolved.Add($"sentence_{index}_missing_telemetry_id");
            }

            foreach (var id in ids.Where(id => !telemetryUniverse.Contains(id)))
            {
                unresolved.Add(id ?? string.Empty);
            }
        }

        return new SentenceResult(
            sentences.Count,
            resolved,
            unresolved.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static bool TryTelemetryUniverse(
        SealedScoredRunInput input,
        string runId,
        out HashSet<string> universe)
    {
        universe = new HashSet<string>(StringComparer.Ordinal);
        var facts = input?.Facts ?? Array.Empty<PlayerVisibleFactRecord>();
        var decisions = input?.Decisions ?? Array.Empty<PlayerVisibleDecisionRecord>();
        var rows = facts.Select(fact => (fact?.CampaignId, fact?.RunId, Present: fact != null))
            .Concat(decisions.Select(decision =>
                (decision?.CampaignId, decision?.RunId, Present: decision != null)))
            .ToArray();
        if (rows.Length == 0
            || rows.Any(row => !row.Present)
            || rows.Select(row => row.CampaignId ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .Count() != 1
            || rows.Any(row => !string.Equals(row.RunId, runId, StringComparison.Ordinal)))
        {
            return false;
        }

        foreach (var fact in facts)
        {
            if (!string.IsNullOrEmpty(fact.FactId))
            {
                universe.Add(fact.FactId);
            }
        }

        foreach (var decision in decisions)
        {
            if (!string.IsNullOrEmpty(decision.DecisionId))
            {
                universe.Add(decision.DecisionId);
            }
        }

        return true;
    }

    private static bool OwnerApprovalMatches(
        SealedDesireCommitCohortEvaluation evaluated,
        OwnerApprovalArtifact ownerApproval)
    {
        if (ownerApproval?.Approved != true
            || evaluated.Runs.Count != 6
            || evaluated.Runs.Any(run => !run.Valid))
        {
            return false;
        }

        var current = evaluated.Runs
            .Select(run => run.Input.Run.Shape.TraceManifestHash)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToArray();
        var bound = ownerApproval.BoundTraceManifestHashes?.ToArray() ?? Array.Empty<string>();
        if (current.Length != 6
            || current.Distinct(StringComparer.Ordinal).Count() != 6
            || bound.Length != 6
            || bound.Distinct(StringComparer.Ordinal).Count() != 6)
        {
            return false;
        }

        return new HashSet<string>(current, StringComparer.Ordinal)
            .SetEquals(bound);
    }

    private sealed record SentenceResult(
        int UnitCount,
        int ResolvedCount,
        IReadOnlyList<string> UnresolvedIds);
}
