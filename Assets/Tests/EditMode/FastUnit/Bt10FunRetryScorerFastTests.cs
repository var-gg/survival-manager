using System;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class Bt10FunRetryScorerFastTests
{
    [Test]
    public void GreenSixRunCohort_WithManifestBoundOwnerKey_PassesAllEightMetrics()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort();
        var owner = Bt5Bt10SyntheticFixture.OwnerApproval(fixtures);

        var aggregate = Bt10FunRetryScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(fixtures),
            owner);

        Assert.That(aggregate.ValidRunCount, Is.EqualTo(6));
        Assert.That(aggregate.DesireFormedRunCount, Is.EqualTo(6));
        Assert.That(aggregate.EvidenceGroundedCommitRunCount, Is.EqualTo(6));
        Assert.That(aggregate.PayoffOrLegibleNearMissRunCount, Is.EqualTo(6));
        Assert.That(aggregate.NextConceptNamedRunCount, Is.EqualTo(6));
        Assert.That(aggregate.ComplaintRepeatedTwiceCount, Is.Zero);
        Assert.That(aggregate.EvaluationSentenceTelemetryLinkRate, Is.EqualTo(1d));
        Assert.That(aggregate.OwnerApproval, Is.EqualTo(1));

        var observations = aggregate.ToBt10Observations().ToDictionary(value => value.MetricId);
        Assert.That(observations.Count, Is.EqualTo(8));
        Assert.That(observations["owner_approval"].Value, Is.EqualTo(1d));
    }

    [Test]
    public void PollutedTelemetryIdAndMissingSentence_ForceRateBelowOne()
    {
        var polluted = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 0
                ? new Bt5Bt10SyntheticRunOptions
                {
                    TelemetryEventIdOverride = "fact-not-in-ledger",
                }
                : null);
        var pollutedAggregate = Bt10FunRetryScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(polluted),
            Bt5Bt10SyntheticFixture.OwnerApproval(polluted));
        Assert.That(pollutedAggregate.EvaluationSentenceUnitCount, Is.EqualTo(6));
        Assert.That(pollutedAggregate.EvaluationSentenceTelemetryLinkRate, Is.EqualTo(5d / 6d));
        Assert.That(
            pollutedAggregate.PerRun[0].UnresolvedTelemetryIds,
            Does.Contain("fact-not-in-ledger"));

        var sentenceMissing = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 0
                ? new Bt5Bt10SyntheticRunOptions { IncludeEvaluationSentence = false }
                : null);
        var missingAggregate = Bt10FunRetryScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(sentenceMissing),
            Bt5Bt10SyntheticFixture.OwnerApproval(sentenceMissing));
        Assert.That(missingAggregate.EvaluationSentenceUnitCount, Is.EqualTo(6));
        Assert.That(missingAggregate.EvaluationSentenceTelemetryLinkRate, Is.EqualTo(5d / 6d));
        Assert.That(missingAggregate.PerRun[0].SentenceUnitCount, Is.EqualTo(1));
        Assert.That(missingAggregate.PerRun[0].ResolvedSentenceCount, Is.Zero);
    }

    [Test]
    public void FactOnlyLedger_StillResolvesFactTelemetryFromTheDefinedUnion()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort();
        var inputs = fixtures
            .Select(fixture => new SealedScoredRunInput(
                fixture.Input.Run,
                fixture.Facts,
                Array.Empty<PlayerVisibleDecisionRecord>()))
            .ToArray();

        var aggregate = Bt10FunRetryScorer.Score(inputs);

        Assert.That(aggregate.EvaluationSentenceUnitCount, Is.EqualTo(6));
        Assert.That(aggregate.EvaluationSentenceTelemetryLinkRate, Is.EqualTo(1d));
        Assert.That(aggregate.DesireFormedRunCount, Is.Zero);
    }

    [Test]
    public void SameNormalizedComplaintAcrossTwoRuns_CountsOneRepeatedComplaint()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index < 2
                ? new Bt5Bt10SyntheticRunOptions
                {
                    Complaints = index == 0
                        ? new[] { "  Offers   felt opaque " }
                        : new[] { "offers felt opaque" },
                }
                : null);

        var aggregate = Bt10FunRetryScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(fixtures),
            Bt5Bt10SyntheticFixture.OwnerApproval(fixtures));

        Assert.That(aggregate.ComplaintRepeatedTwiceCount, Is.EqualTo(1));
        Assert.That(aggregate.RepeatedComplaints, Is.EqualTo(new[] { "offers felt opaque" }));
    }

    [Test]
    public void OwnerApproval_IsNeverMachineDerivedAndRejectsManifestDrift()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort();
        var inputs = Bt5Bt10SyntheticFixture.Inputs(fixtures);

        var absent = Bt10FunRetryScorer.Score(inputs);
        Assert.That(absent.OwnerApproval, Is.Zero);
        Assert.That(absent.OwnerApprovalSampleCount, Is.Zero);

        var hashes = fixtures
            .Select(fixture => SealedDecisionTraceHash.ComputeManifest(fixture.Trace))
            .ToArray();
        hashes[0] = "stale-trace-manifest";
        var mismatched = Bt10FunRetryScorer.Score(
            inputs,
            Bt5Bt10SyntheticFixture.OwnerApproval(fixtures, hashes));
        Assert.That(mismatched.OwnerApproval, Is.Zero);
        Assert.That(mismatched.OwnerApprovalSampleCount, Is.EqualTo(1));
    }

    [Test]
    public void CohortCollapse_ForcesAllBt10ValuesIncludingRateAndOwnerToFailingValues()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 5
                ? new Bt5Bt10SyntheticRunOptions { PromptSchemaHash = "forked-prompt" }
                : null);

        var aggregate = Bt10FunRetryScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(fixtures),
            Bt5Bt10SyntheticFixture.OwnerApproval(fixtures));

        Assert.That(aggregate.CohortConsistent, Is.False);
        Assert.That(aggregate.ValidRunCount, Is.Zero);
        Assert.That(aggregate.DesireFormedRunCount, Is.Zero);
        Assert.That(aggregate.EvidenceGroundedCommitRunCount, Is.Zero);
        Assert.That(aggregate.PayoffOrLegibleNearMissRunCount, Is.Zero);
        Assert.That(aggregate.NextConceptNamedRunCount, Is.Zero);
        Assert.That(aggregate.ComplaintRepeatedTwiceCount, Is.Zero);
        Assert.That(aggregate.EvaluationSentenceTelemetryLinkRate, Is.Zero);
        Assert.That(aggregate.OwnerApproval, Is.Zero);
    }
}
