using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>사후 payoff를 보지 않고 commit_t의 AND 조건을 판정한다.</summary>
public static class IntentCommitEvaluator
{
    public static IntentCommitAssessment Assess(
        BuildHypothesis hypothesis,
        bool milestoneAdvanced,
        bool scarceResourceInvested,
        IReadOnlyList<string> pivotConditions)
    {
        if (hypothesis == null)
        {
            throw new ArgumentNullException(nameof(hypothesis));
        }

        return new IntentCommitAssessment(
            hasPriorEvidence: hypothesis.EvidenceRefs != null
                              && hypothesis.EvidenceRefs.Where(value => !string.IsNullOrWhiteSpace(value))
                                  .Distinct(StringComparer.Ordinal).Count() >= 2,
            hasExpectedPayoff: !string.IsNullOrWhiteSpace(hypothesis.ExpectedPayoff),
            hasNextAcquisitionPlan: hypothesis.NextAcquisitionPlan != null
                                    && hypothesis.NextAcquisitionPlan.Any(value => !string.IsNullOrWhiteSpace(value)),
            actionAdvancesOrInvests: milestoneAdvanced || scarceResourceInvested,
            hasPivotCondition: pivotConditions != null
                               && pivotConditions.Any(value => !string.IsNullOrWhiteSpace(value)),
            declaredBeforePayoff: hypothesis.DeclaredBeforePayoff);
    }
}
