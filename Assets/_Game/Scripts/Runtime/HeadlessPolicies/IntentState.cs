using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

public static class IntentDecisionReason
{
    public const string Keep = "keep";
    public const string Advance = "advance";
    public const string Substitute = "substitute";
    public const string CounterAdapt = "counter-adapt";
    public const string Pivot = "pivot";
    public const string Abandon = "abandon";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Keep,
        Advance,
        Substitute,
        CounterAdapt,
        Pivot,
        Abandon,
    };

    public static void RequireKnown(string reason)
    {
        if (!All.Contains(reason))
        {
            throw new InvalidOperationException($"Unknown intent decision reason '{reason}'.");
        }
    }
}

public static class IntentStatus
{
    public const string Active = "active";
    public const string Pivoted = "pivoted";
    public const string Abandoned = "abandoned";
}

/// <summary>정책 instance가 한 campaign 동안 소유하는 비정적 의도 진행 상태.</summary>
public sealed class IntentState
{
    public IntentState(
        string intentId,
        string sourceLane,
        int nextDecisionIndex,
        int declaredAtDecisionIndex,
        int commitDecisionIndex,
        string status,
        int consecutiveNoProgressDecisions,
        int progressScore,
        IReadOnlyList<string> completedMilestones,
        BuildHypothesis hypothesis)
    {
        IntentId = intentId;
        SourceLane = sourceLane;
        NextDecisionIndex = nextDecisionIndex;
        DeclaredAtDecisionIndex = declaredAtDecisionIndex;
        CommitDecisionIndex = commitDecisionIndex;
        Status = status;
        ConsecutiveNoProgressDecisions = consecutiveNoProgressDecisions;
        ProgressScore = progressScore;
        CompletedMilestones = completedMilestones;
        Hypothesis = hypothesis;
    }

    public string IntentId { get; }
    public string SourceLane { get; }
    public int NextDecisionIndex { get; }
    public int DeclaredAtDecisionIndex { get; }
    public int CommitDecisionIndex { get; }
    public string Status { get; }
    public int ConsecutiveNoProgressDecisions { get; }
    public int ProgressScore { get; }
    public IReadOnlyList<string> CompletedMilestones { get; }
    public BuildHypothesis Hypothesis { get; }

    public bool IsCommitted => CommitDecisionIndex >= 0;
}

/// <summary>commit_t 다섯 조건과 사전 선언 순서를 독립적으로 드러내는 판정 snapshot.</summary>
public sealed class IntentCommitAssessment
{
    public IntentCommitAssessment(
        bool hasPriorEvidence,
        bool hasExpectedPayoff,
        bool hasNextAcquisitionPlan,
        bool actionAdvancesOrInvests,
        bool hasPivotCondition,
        bool declaredBeforePayoff)
    {
        HasPriorEvidence = hasPriorEvidence;
        HasExpectedPayoff = hasExpectedPayoff;
        HasNextAcquisitionPlan = hasNextAcquisitionPlan;
        ActionAdvancesOrInvests = actionAdvancesOrInvests;
        HasPivotCondition = hasPivotCondition;
        DeclaredBeforePayoff = declaredBeforePayoff;
    }

    public bool HasPriorEvidence { get; }
    public bool HasExpectedPayoff { get; }
    public bool HasNextAcquisitionPlan { get; }
    public bool ActionAdvancesOrInvests { get; }
    public bool HasPivotCondition { get; }
    public bool DeclaredBeforePayoff { get; }

    public bool Eligible => HasPriorEvidence
                            && HasExpectedPayoff
                            && HasNextAcquisitionPlan
                            && ActionAdvancesOrInvests
                            && HasPivotCondition
                            && DeclaredBeforePayoff;
}

/// <summary>정책 assembly가 보존하는 결정 직후 snapshot. Metrics adapter가 IntentTraceRecord로 투영한다.</summary>
public sealed class HeadlessIntentDecision
{
    public HeadlessIntentDecision(
        int decisionIndex,
        string decisionKind,
        string action,
        string reason,
        bool milestoneAdvanced,
        bool scarceResourceInvested,
        bool isCommit,
        IntentCommitAssessment commitAssessment,
        BuildHypothesis hypothesisSnapshot,
        IntentState stateSnapshot)
    {
        DecisionIndex = decisionIndex;
        DecisionKind = decisionKind;
        Action = action;
        Reason = reason;
        MilestoneAdvanced = milestoneAdvanced;
        ScarceResourceInvested = scarceResourceInvested;
        IsCommit = isCommit;
        CommitAssessment = commitAssessment;
        HypothesisSnapshot = hypothesisSnapshot;
        StateSnapshot = stateSnapshot;
    }

    public int DecisionIndex { get; }
    public string DecisionKind { get; }
    public string Action { get; }
    public string Reason { get; }
    public bool MilestoneAdvanced { get; }
    public bool ScarceResourceInvested { get; }
    public bool IsCommit { get; }
    public IntentCommitAssessment CommitAssessment { get; }
    public BuildHypothesis HypothesisSnapshot { get; }
    public IntentState StateSnapshot { get; }
}
