using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class ConceptCommitPolicyFastTests
{
    [Test]
    public void SameSeed_ProducesByteIdenticalDecisionsAndIntentTrace()
    {
        var observation = IntentPolicyObservationFixture.Create(1701);
        var first = RunPolicy(observation, IntentPolicyObservationFixture.HumanThresholdIntent());
        var repeated = RunPolicy(observation, IntentPolicyObservationFixture.HumanThresholdIntent());

        Assert.That(repeated.DeploymentAction, Is.EqualTo(first.DeploymentAction));
        Assert.That(repeated.RewardOptionIndex, Is.EqualTo(first.RewardOptionIndex));
        Assert.That(repeated.TraceJson, Is.EqualTo(first.TraceJson));
        Assert.That(first.Trace[0].Reason, Is.EqualTo(IntentDecisionReason.Advance));
        Assert.That(first.Trace[0].IsCommit, Is.True);
        Assert.That(first.Trace[0].DecisionIndex, Is.EqualTo(0));
        Assert.That(first.Trace[0].CommitAssessment.Eligible, Is.True);
        Assert.That(first.Trace[0].HypothesisSnapshot.PayoffObservedAtDecisionIndex, Is.EqualTo(-1));
        Assert.That(first.Trace[0].HypothesisSnapshot.DeclaredBeforePayoff, Is.True);
    }

    [Test]
    public void CommitEvaluator_RejectsEachMissingAndConditionAndPostHocDeclaration()
    {
        var complete = new BuildHypothesis(
            "identity->payoff",
            new[] { "fact-a", "fact-b" },
            0.7d,
            "verify:milestone",
            "beat.payoff",
            new[] { "acquire:next" },
            "track_unavailable",
            0,
            -1);
        var pivot = new[] { "track_unavailable" };

        Assert.That(IntentCommitEvaluator.Assess(complete, true, false, pivot).Eligible, Is.True);
        Assert.That(IntentCommitEvaluator.Assess(
            Hypothesis(evidence: new[] { "fact-a" }), true, false, pivot).Eligible, Is.False,
            "prior evidence must contain at least two distinct facts");
        Assert.That(IntentCommitEvaluator.Assess(
            Hypothesis(expectedPayoff: string.Empty), true, false, pivot).Eligible, Is.False,
            "expected payoff is an AND condition");
        Assert.That(IntentCommitEvaluator.Assess(
            Hypothesis(plan: Array.Empty<string>()), true, false, pivot).Eligible, Is.False,
            "next acquisition plan is an AND condition");
        Assert.That(IntentCommitEvaluator.Assess(complete, false, false, pivot).Eligible, Is.False,
            "the action must advance a milestone or invest a scarce resource");
        Assert.That(IntentCommitEvaluator.Assess(complete, true, false, Array.Empty<string>()).Eligible, Is.False,
            "a pivot condition is an AND condition");
        Assert.That(IntentCommitEvaluator.Assess(
            Hypothesis(declaredAt: 2, payoffObservedAt: 1),
            true,
            false,
            pivot).Eligible, Is.False,
            "post-payoff declaration is post-hoc rationalization");
    }

    [Test]
    public void ReasonClassifier_UsesAdvanceSubstituteAndCounterAdaptForVisibleSituations()
    {
        var advance = new ConceptCommitPolicy(IntentPolicyObservationFixture.HumanThresholdIntent());
        advance.DecideDeployment(IntentPolicyObservationFixture.Create());
        Assert.That(advance.LastIntentDecision.Reason, Is.EqualTo(IntentDecisionReason.Advance));

        var substitute = new ConceptCommitPolicy(IntentPolicyObservationFixture.MissingPrimaryIntent(true));
        substitute.DecideDeployment(IntentPolicyObservationFixture.Create());
        Assert.That(substitute.LastIntentDecision.Reason, Is.EqualTo(IntentDecisionReason.Substitute));
        Assert.That(substitute.LastIntentDecision.Action, Does.Contain("hero-5"),
            "the visible hunter is the declared deterministic substitute");

        var counter = new ConceptCommitPolicy(IntentPolicyObservationFixture.HumanThresholdIntent());
        counter.DecideDeployment(IntentPolicyObservationFixture.Create(threatSkulls: 4));
        Assert.That(counter.LastIntentDecision.Reason, Is.EqualTo(IntentDecisionReason.CounterAdapt));
    }

    [Test]
    public void ReasonClassifier_EscalatesNoProgressFromKeepToPivotToAbandon()
    {
        var policy = new ConceptCommitPolicy(IntentPolicyObservationFixture.MissingPrimaryIntent(false));
        var observation = IntentPolicyObservationFixture.Create();

        policy.DecideDeployment(observation);
        policy.DecideReward(observation);
        policy.DecideReward(observation);

        Assert.That(
            policy.DecisionTrace.Select(value => value.Reason),
            Is.EqualTo(new[]
            {
                IntentDecisionReason.Keep,
                IntentDecisionReason.Pivot,
                IntentDecisionReason.Abandon,
            }));
    }

    [Test]
    public void DiscoveryLane_UsesOnlyPriorPlayerVisibleFactsAndNeverReferencesEvaluatorAssemblies()
    {
        var observation = IntentPolicyObservationFixture.CreateWithAuditableFacts(out var facts);
        var policy = new ConceptCommitPolicy();
        var deployment = policy.DecideDeployment(observation);
        var reward = policy.DecideReward(observation);
        var decisions = new[]
        {
            PlayerVisibleDecisionRecord.Create(
                "intent-test-run",
                "campaign-000000",
                new PlayerVisibleTimelinePoint(0, 0, 0),
                policy.Id,
                "deployment",
                policy.DecisionTrace[0].Action,
                deployment.Rationale,
                deployment.EstimatedValue,
                deployment.EvidenceFactIds),
            PlayerVisibleDecisionRecord.Create(
                "intent-test-run",
                "campaign-000000",
                new PlayerVisibleTimelinePoint(0, 0, 1),
                policy.Id,
                "reward",
                policy.DecisionTrace[1].Action,
                reward.Rationale,
                reward.EstimatedValue,
                reward.EvidenceFactIds),
        };

        var audit = PlayerVisibleFactLedgerAuditor.Audit(facts, decisions);
        Assert.That(policy.CurrentIntent.SourceLane, Is.EqualTo("discovery"));
        Assert.That(audit.PostDecisionInformationReferenceCount, Is.Zero);
        Assert.That(audit.NonUiSemanticInternalFieldReferenceCount, Is.Zero);
        Assert.That(audit.OracleOrTruthLeakCount, Is.Zero);
        Assert.That(audit.UnsupportedCertainClaimCount, Is.Zero);
        Assert.That(
            typeof(ConceptCommitPolicy).Assembly.GetReferencedAssemblies().Select(value => value.Name),
            Does.Not.Contain("SM.HeadlessCensus"));
    }

    [Test]
    public void EightSeedSmoke_EmitsOneReasonedTracePerDecisionWithNoMissingRows()
    {
        for (var seed = 1701; seed < 1709; seed++)
        {
            var policy = new ConceptCommitPolicy();
            var observation = IntentPolicyObservationFixture.Create(seed);
            policy.DecideDeployment(observation);
            policy.DecideReward(observation);

            Assert.That(policy.DecisionTrace.Count, Is.EqualTo(2), $"seed={seed}");
            Assert.That(policy.DecisionTrace.All(value => IntentDecisionReason.All.Contains(value.Reason)), Is.True, $"seed={seed}");
            Assert.That(policy.DecisionTrace.Count(value => value.IsCommit), Is.EqualTo(1), $"seed={seed}");
            Assert.That(policy.CurrentState.CommitDecisionIndex, Is.EqualTo(0), $"seed={seed}");
        }
    }

    [Test]
    public void ExistingSixPolicyCohortAndCoverageFactorySurfaceRemainUnchanged()
    {
        Assert.That(HeadlessPolicyFactory.ProductionPolicyIds, Is.EqualTo(new[]
        {
            HeadlessPolicyFactory.RandomLegalId,
            HeadlessPolicyFactory.GreedyId,
            HeadlessPolicyFactory.DoctrineId,
            HeadlessPolicyFactory.FormationId,
            HeadlessPolicyFactory.CounterAdaptiveId,
            HeadlessPolicyFactory.SearchPlannerId,
        }));
        Assert.That(HeadlessPolicyFactory.AllPolicyIds.Count, Is.EqualTo(7));
        Assert.That(HeadlessPolicyFactory.AllPolicyIds, Does.Contain(HeadlessPolicyFactory.CoverageId));
    }

    private static PolicyRun RunPolicy(HeadlessPolicyObservation observation, HeadlessConceptIntent intent)
    {
        var policy = new ConceptCommitPolicy(intent);
        var deployment = policy.DecideDeployment(observation);
        var reward = policy.DecideReward(observation);
        HeadlessPolicyGuard.ValidateDeploymentDecision(observation, deployment);
        HeadlessPolicyGuard.ValidateRewardDecision(observation, reward);
        return new PolicyRun(
            policy.DecisionTrace[0].Action,
            reward.OptionIndex,
            policy.DecisionTrace.ToArray(),
            HeadlessMetricJson.Serialize(policy.DecisionTrace));
    }

    private static BuildHypothesis Hypothesis(
        IReadOnlyList<string> evidence = null,
        string expectedPayoff = "beat.payoff",
        IReadOnlyList<string> plan = null,
        int declaredAt = 0,
        int payoffObservedAt = -1)
        => new(
            "identity->payoff",
            evidence ?? new[] { "fact-a", "fact-b" },
            0.7d,
            "verify:milestone",
            expectedPayoff,
            plan ?? new[] { "acquire:next" },
            "track_unavailable",
            declaredAt,
            payoffObservedAt);

    private sealed record PolicyRun(
        string DeploymentAction,
        int RewardOptionIndex,
        IReadOnlyList<HeadlessIntentDecision> Trace,
        string TraceJson);
}
