using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>정책 sidecar snapshot을 metrics-owned IntentTraceRecord로만 투영한다.</summary>
internal sealed class H100IntentTraceCollector
{
    private readonly string _runId;
    private readonly List<IntentTraceRecord> _records = new();

    public H100IntentTraceCollector(string runId)
    {
        _runId = runId ?? string.Empty;
    }

    public IReadOnlyList<IntentTraceRecord> Records => _records;

    public void Record(
        string campaignId,
        int campaignIndex,
        int siteIndex,
        int decisionIndex,
        IHeadlessPolicy policy)
    {
        if (policy is not ConceptCommitPolicy conceptPolicy)
        {
            return;
        }

        var trace = conceptPolicy.LastIntentDecision;
        if (trace.DecisionIndex != decisionIndex)
        {
            throw new InvalidOperationException(
                $"Intent trace index drifted from campaign decision order (policy={trace.DecisionIndex}, campaign={decisionIndex}).");
        }

        var hypothesis = trace.HypothesisSnapshot;
        var state = trace.StateSnapshot;
        _records.Add(IntentTraceRecord.Create(
            _runId,
            campaignId,
            new PlayerVisibleTimelinePoint(campaignIndex, siteIndex, decisionIndex),
            policy.Id,
            state.SourceLane,
            trace.DecisionKind,
            trace.Action,
            trace.Reason,
            trace.MilestoneAdvanced,
            trace.ScarceResourceInvested,
            trace.IsCommit,
            new IntentCommitConditionSnapshot(
                trace.CommitAssessment.HasPriorEvidence,
                trace.CommitAssessment.HasExpectedPayoff,
                trace.CommitAssessment.HasNextAcquisitionPlan,
                trace.CommitAssessment.ActionAdvancesOrInvests,
                trace.CommitAssessment.HasPivotCondition,
                trace.CommitAssessment.DeclaredBeforePayoff,
                trace.CommitAssessment.Eligible),
            new IntentHypothesisSnapshot(
                hypothesis.ClaimedEdge,
                hypothesis.EvidenceRefs.Select(value => new PlayerVisibleEvidenceRef(value)).ToArray(),
                hypothesis.Confidence,
                hypothesis.OpenQuestion,
                hypothesis.ExpectedPayoff,
                hypothesis.NextAcquisitionPlan,
                hypothesis.FalsificationSignal,
                hypothesis.DeclaredAtDecisionIndex,
                hypothesis.PayoffObservedAtDecisionIndex),
            new IntentStateSnapshot(
                state.IntentId,
                state.SourceLane,
                state.NextDecisionIndex,
                state.DeclaredAtDecisionIndex,
                state.CommitDecisionIndex,
                state.Status,
                state.ConsecutiveNoProgressDecisions,
                state.ProgressScore,
                state.CompletedMilestones)));
    }
}
