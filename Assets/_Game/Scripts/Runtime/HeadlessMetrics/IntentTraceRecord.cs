using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

public sealed record IntentHypothesisSnapshot(
    string ClaimedEdge,
    IReadOnlyList<PlayerVisibleEvidenceRef> EvidenceRefs,
    double Confidence,
    string OpenQuestion,
    string ExpectedPayoff,
    IReadOnlyList<string> NextAcquisitionPlan,
    string FalsificationSignal,
    int DeclaredAtDecisionIndex,
    int PayoffObservedAtDecisionIndex);

public sealed record IntentCommitConditionSnapshot(
    bool HasPriorEvidence,
    bool HasExpectedPayoff,
    bool HasNextAcquisitionPlan,
    bool ActionAdvancesOrInvests,
    bool HasPivotCondition,
    bool DeclaredBeforePayoff,
    bool Eligible);

public sealed record IntentStateSnapshot(
    string IntentId,
    string SourceLane,
    int NextDecisionIndex,
    int DeclaredAtDecisionIndex,
    int CommitDecisionIndex,
    string Status,
    int ConsecutiveNoProgressDecisions,
    int ProgressScore,
    IReadOnlyList<string> CompletedMilestones);

/// <summary>decision별 이유, milestone 진척, 사전 hypothesis와 commit_t 판정을 고정하는 BT1-E04 행.</summary>
public sealed record IntentTraceRecord
{
    public const string CurrentSchemaVersion = "intent-trace-bt1-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string TraceId { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    public string CampaignId { get; init; } = string.Empty;
    public PlayerVisibleTimelinePoint DecidedAt { get; init; } = new(0, 0, 0);
    public string PolicyId { get; init; } = string.Empty;
    public string Lane { get; init; } = string.Empty;
    public string DecisionKind { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public bool MilestoneAdvanced { get; init; }
    public bool ScarceResourceInvested { get; init; }
    public bool IsCommit { get; init; }
    public IntentCommitConditionSnapshot CommitConditions { get; init; } = new(
        false, false, false, false, false, false, false);
    public IntentHypothesisSnapshot Hypothesis { get; init; } = new(
        string.Empty,
        Array.Empty<PlayerVisibleEvidenceRef>(),
        0d,
        string.Empty,
        string.Empty,
        Array.Empty<string>(),
        string.Empty,
        -1,
        -1);
    public IntentStateSnapshot IntentState { get; init; } = new(
        string.Empty,
        string.Empty,
        0,
        0,
        -1,
        string.Empty,
        0,
        0,
        Array.Empty<string>());

    public static IntentTraceRecord Create(
        string runId,
        string campaignId,
        PlayerVisibleTimelinePoint decidedAt,
        string policyId,
        string lane,
        string decisionKind,
        string action,
        string reason,
        bool milestoneAdvanced,
        bool scarceResourceInvested,
        bool isCommit,
        IntentCommitConditionSnapshot commitConditions,
        IntentHypothesisSnapshot hypothesis,
        IntentStateSnapshot intentState)
    {
        if (decidedAt == null)
        {
            throw new ArgumentNullException(nameof(decidedAt));
        }

        if (commitConditions == null || hypothesis == null || intentState == null)
        {
            throw new ArgumentNullException(nameof(commitConditions), "Intent trace snapshots must be materialized.");
        }

        var traceHash = PlayerVisibleLedgerHash.Compute(
            runId,
            campaignId,
            decidedAt.CampaignIndex.ToString(CultureInfo.InvariantCulture),
            decidedAt.SiteIndex.ToString(CultureInfo.InvariantCulture),
            decidedAt.DecisionIndex.ToString(CultureInfo.InvariantCulture),
            policyId,
            lane,
            decisionKind,
            action,
            reason,
            intentState.IntentId,
            isCommit.ToString(CultureInfo.InvariantCulture));
        return new IntentTraceRecord
        {
            TraceId = $"intent-{traceHash}",
            RunId = runId ?? string.Empty,
            CampaignId = campaignId ?? string.Empty,
            DecidedAt = decidedAt,
            PolicyId = policyId ?? string.Empty,
            Lane = lane ?? string.Empty,
            DecisionKind = decisionKind ?? string.Empty,
            Action = action ?? string.Empty,
            Reason = reason ?? string.Empty,
            MilestoneAdvanced = milestoneAdvanced,
            ScarceResourceInvested = scarceResourceInvested,
            IsCommit = isCommit,
            CommitConditions = commitConditions,
            Hypothesis = hypothesis with
            {
                EvidenceRefs = (hypothesis.EvidenceRefs ?? Array.Empty<PlayerVisibleEvidenceRef>())
                    .Where(value => value != null && !string.IsNullOrWhiteSpace(value.FactId))
                    .GroupBy(value => value.FactId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .OrderBy(value => value.FactId, StringComparer.Ordinal)
                    .ToArray(),
                NextAcquisitionPlan = (hypothesis.NextAcquisitionPlan ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            },
            IntentState = intentState with
            {
                CompletedMilestones = (intentState.CompletedMilestones ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
            },
        };
    }
}
