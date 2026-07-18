using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>decoded trace와 같은 run의 player-visible ledger를 결합한 순수 scorer 입력.</summary>
public sealed record SealedScoredRunInput(
    DecodedSealedRun Run,
    IReadOnlyList<PlayerVisibleFactRecord> Facts,
    IReadOnlyList<PlayerVisibleDecisionRecord> Decisions);

public sealed record Bt5CommitWitness(
    int DecisionIndex,
    string SeamType,
    string SelectedAction,
    string MatchedToken,
    string TargetToken,
    string IntentId,
    int IntentDeclaredAtIndex);

public sealed record Bt5RunDiagnostics(
    int InadmissibleTrackTokenCount,
    int UnknownKindTrackTokenCount,
    int ConfidenceOutOfUnitRangeCount,
    int EvidenceJoinFailureCount,
    int LedgerMappingFailureCount,
    int NoTargetRewardPickCount,
    int RewardMechanicsHoleCount,
    int ActionJoinFailureCount,
    int DanglingIntentRefCount,
    int SameDecisionFormationCommitCount,
    int PriorDeclarationCommitCount,
    int FamilyOnlyCommitCount);

public sealed record Bt5RunEvidence(
    string RunId,
    bool Valid,
    string ValidityFailure,
    bool TerminalFailure,
    bool SpontaneousIntent,
    string FirstQualifyingIntentId,
    int DeclaredAtDecisionIndex,
    bool ScarceResourceCommit,
    IReadOnlyList<Bt5CommitWitness> Commits,
    Bt5RunDiagnostics Diagnostics);

/// <summary>BT5 세 gate metric과 run별 honesty witness.</summary>
public sealed record Bt5Aggregate(
    int SuppliedRunCount,
    int ValidRunCount,
    int SpontaneousIntentRunCount,
    int ScarceResourceCommitRunCount,
    bool CohortConsistent,
    string CohortFailureReason,
    IReadOnlyList<Bt5RunEvidence> PerRun)
{
    public IReadOnlyList<H100GateEvaluator.ExternalObservation> ToBt5Observations()
        => new[]
        {
            new H100GateEvaluator.ExternalObservation(
                "cold_start_run_count",
                ValidRunCount,
                SuppliedRunCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt5-witness.json valid_runs={0}/{1} cohort_consistent={2} failure={3}",
                    ValidRunCount,
                    SuppliedRunCount,
                    CohortConsistent,
                    CohortFailureReason)),
            new H100GateEvaluator.ExternalObservation(
                "spontaneous_intent_run_count",
                SpontaneousIntentRunCount,
                ValidRunCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt5-witness.json spontaneous_runs={0}/{1}",
                    SpontaneousIntentRunCount,
                    ValidRunCount)),
            new H100GateEvaluator.ExternalObservation(
                "scarce_resource_commit_run_count",
                ScarceResourceCommitRunCount,
                ValidRunCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt5-witness.json commit_runs={0}/{1} commit_witnesses={2}",
                    ScarceResourceCommitRunCount,
                    ValidRunCount,
                    CohortConsistent
                        ? (PerRun ?? Array.Empty<Bt5RunEvidence>()).Sum(run => run.Commits?.Count ?? 0)
                        : 0)),
        };
}

public static class Bt5DesireCommitScorer
{
    public static Bt5Aggregate Score(
        IReadOnlyList<SealedScoredRunInput> inputs,
        string expectedPromptSchemaHash = null)
    {
        var evaluated = SealedDesireCommitEvaluation.Evaluate(inputs, expectedPromptSchemaHash);
        var validRunCount = evaluated.CohortConsistent
            ? evaluated.Runs.Count(run => run.Valid)
            : 0;
        var spontaneousCount = evaluated.CohortConsistent
            ? evaluated.Runs.Count(run => run.SpontaneousIntent)
            : 0;
        var commitCount = evaluated.CohortConsistent
            ? evaluated.Runs.Count(run => run.Commits.Count > 0)
            : 0;
        return new Bt5Aggregate(
            evaluated.SuppliedRunCount,
            validRunCount,
            spontaneousCount,
            commitCount,
            evaluated.CohortConsistent,
            evaluated.CohortFailureReason,
            evaluated.Runs.Select(run => run.ToBt5Evidence()).ToArray());
    }
}
