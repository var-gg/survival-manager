using System.Collections.Generic;

namespace SM.HeadlessPolicies;

/// <summary>정책이 payoff 관측 전에 선언하는 구조화된 build 가설 sidecar.</summary>
public sealed class BuildHypothesis
{
    public BuildHypothesis(
        string claimedEdge,
        IReadOnlyList<string> evidenceRefs,
        double confidence,
        string openQuestion,
        string expectedPayoff,
        IReadOnlyList<string> nextAcquisitionPlan,
        string falsificationSignal,
        int declaredAtDecisionIndex,
        int payoffObservedAtDecisionIndex)
    {
        ClaimedEdge = claimedEdge;
        EvidenceRefs = evidenceRefs;
        Confidence = confidence;
        OpenQuestion = openQuestion;
        ExpectedPayoff = expectedPayoff;
        NextAcquisitionPlan = nextAcquisitionPlan;
        FalsificationSignal = falsificationSignal;
        DeclaredAtDecisionIndex = declaredAtDecisionIndex;
        PayoffObservedAtDecisionIndex = payoffObservedAtDecisionIndex;
    }

    public string ClaimedEdge { get; }
    public IReadOnlyList<string> EvidenceRefs { get; }
    public double Confidence { get; }
    public string OpenQuestion { get; }
    public string ExpectedPayoff { get; }
    public IReadOnlyList<string> NextAcquisitionPlan { get; }
    public string FalsificationSignal { get; }
    public int DeclaredAtDecisionIndex { get; }
    public int PayoffObservedAtDecisionIndex { get; }

    public bool DeclaredBeforePayoff
        => PayoffObservedAtDecisionIndex < 0 || DeclaredAtDecisionIndex < PayoffObservedAtDecisionIndex;
}
