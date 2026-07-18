using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.SealedLlmBridge;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class Bt5DesireCommitScorerFastTests
{
    [Test]
    public void GreenSixRunCohort_ReDerivesAllBt5MetricsFromSealedBytesAndLedger()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort();
        var aggregate = Bt5DesireCommitScorer.Score(Bt5Bt10SyntheticFixture.Inputs(fixtures));

        Assert.That(fixtures.All(fixture => fixture.Input.Run.Shape.Valid), Is.True);
        Assert.That(aggregate.CohortConsistent, Is.True);
        Assert.That(aggregate.SuppliedRunCount, Is.EqualTo(6));
        Assert.That(aggregate.ValidRunCount, Is.EqualTo(6));
        Assert.That(aggregate.SpontaneousIntentRunCount, Is.EqualTo(6));
        Assert.That(aggregate.ScarceResourceCommitRunCount, Is.EqualTo(6));
        Assert.That(aggregate.PerRun.All(run => run.Commits.Count == 1), Is.True);

        var observation = aggregate.ToBt5Observations().ToDictionary(value => value.MetricId);
        Assert.That(observation["cold_start_run_count"].Value, Is.EqualTo(6));
        Assert.That(observation["spontaneous_intent_run_count"].Value, Is.EqualTo(6));
        Assert.That(observation["scarce_resource_commit_run_count"].Value, Is.EqualTo(6));
    }

    [Test]
    public void WriterReaderRoundTrip_RecoversVisibleIdsAndRecruitJoinWithoutPolicyReference()
    {
        var fixture = Bt5Bt10SyntheticFixture.CreateRun(0);
        var entry = fixture.Trace.Entries[0];
        var join = SealedObservationJoinReader.Read(entry.ObservationCanonicalBytes, "recruit");

        Assert.That(join.Available, Is.True, join.FailureReason);
        Assert.That(join.VisibleIds, Does.Contain("recruit-0"));
        Assert.That(join.VisibleIds, Does.Contain("support"));
        Assert.That(join.RecruitOffers.Count, Is.EqualTo(1));
        Assert.That(join.RecruitOffers[0].ArchetypeId, Is.EqualTo("recruit-0"));
        Assert.That(join.RecruitOffers[0].FlexActiveSkillId, Is.EqualTo("next-skill-0"));
        Assert.That(join.PassiveNodes.Single().NodeId, Is.EqualTo("node-0"));
        Assert.That(join.RefitItems.Single().ItemInstanceId, Is.EqualTo("item-instance-0"));

        var rewardItem = new HeadlessItemMechanicsObservation(
            "reward-item",
            "reward-instance",
            new[] { "weapon" },
            "weapon-sword",
            Array.Empty<HeadlessStatModifierObservation>(),
            new[]
            {
                new HeadlessAffixMechanicsObservation(
                    "affix-a",
                    new[] { "physical" },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<HeadlessStatModifierObservation>(),
                    Array.Empty<HeadlessRuleModifierObservation>()),
            },
            Array.Empty<HeadlessSkillObservation>());
        var rewardObservation = new HeadlessPolicyObservation(
            1701,
            0,
            "chapter-synthetic",
            "site-synthetic",
            Array.Empty<HeadlessHeroObservation>(),
            Array.Empty<SM.Combat.Model.DeploymentAnchorId>(),
            HeadlessEnemyPreview.Unavailable,
            new[]
            {
                new HeadlessRewardOption(
                    2,
                    HeadlessRewardKind.Item,
                    "reward-item",
                    0,
                    0,
                    0,
                    new HeadlessRewardMechanicsObservation(rewardItem, null)),
            },
            new HeadlessWalletObservation(0, 0),
            Array.Empty<HeadlessAugmentMechanicsObservation>(),
            Array.Empty<HeadlessSynergyCountObservation>(),
            Array.Empty<HeadlessSynergyObservation>(),
            new Dictionary<string, string>());
        var rewardJoin = SealedObservationJoinReader.Read(
            SealedLlmObservationCodec.CanonicalBytes(rewardObservation),
            "reward");
        Assert.That(rewardJoin.Available, Is.True, rewardJoin.FailureReason);
        Assert.That(rewardJoin.RewardOptions.Single().ItemId, Is.EqualTo("reward-item"));
        Assert.That(rewardJoin.RewardOptions.Single().FamilyIds, Does.Contain("physical"));
    }

    [Test]
    public void SyntheticCaptureAndOffSurfaceTrack_FailClosedAtTheirOwningPredicates()
    {
        var synthetic = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 0
                ? new Bt5Bt10SyntheticRunOptions
                {
                    CaptureSource = SealedDecisionTraceCaptureSource.SyntheticStandIn,
                }
                : null);
        var syntheticAggregate = Bt5DesireCommitScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(synthetic));
        Assert.That(syntheticAggregate.ValidRunCount, Is.EqualTo(5));
        Assert.That(syntheticAggregate.PerRun[0].Valid, Is.False);

        var offSurface = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 0
                ? new Bt5Bt10SyntheticRunOptions
                {
                    TrackTokenOverride = "archetype:not-player-visible",
                }
                : null);
        var offSurfaceAggregate = Bt5DesireCommitScorer.Score(
            Bt5Bt10SyntheticFixture.Inputs(offSurface));
        Assert.That(offSurfaceAggregate.SpontaneousIntentRunCount, Is.EqualTo(5));
        Assert.That(offSurfaceAggregate.PerRun[0].SpontaneousIntent, Is.False);
        Assert.That(
            offSurfaceAggregate.PerRun[0].Diagnostics.InadmissibleTrackTokenCount,
            Is.EqualTo(1));
    }

    [Test]
    public void FamilyOnlyMatch_IsDiagnosticAndNeverCountsAsExactCommit()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 0
                ? new Bt5Bt10SyntheticRunOptions
                {
                    TrackTokenOverride = "tag:support",
                }
                : null);

        var aggregate = Bt5DesireCommitScorer.Score(Bt5Bt10SyntheticFixture.Inputs(fixtures));

        Assert.That(aggregate.SpontaneousIntentRunCount, Is.EqualTo(6));
        Assert.That(aggregate.ScarceResourceCommitRunCount, Is.EqualTo(5));
        Assert.That(aggregate.PerRun[0].ScarceResourceCommit, Is.False);
        Assert.That(aggregate.PerRun[0].Diagnostics.FamilyOnlyCommitCount, Is.EqualTo(1));
    }

    [Test]
    public void EvidenceJoin_UsesFullTimelinePointInsteadOfDecisionIndexOnly()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 0
                ? new Bt5Bt10SyntheticRunOptions
                {
                    FactObservedAt = new PlayerVisibleTimelinePoint(0, 1, 0),
                    DecisionAt = new PlayerVisibleTimelinePoint(0, 0, 0),
                }
                : null);

        var aggregate = Bt5DesireCommitScorer.Score(Bt5Bt10SyntheticFixture.Inputs(fixtures));

        Assert.That(aggregate.PerRun[0].SpontaneousIntent, Is.False);
        Assert.That(aggregate.PerRun[0].Diagnostics.EvidenceJoinFailureCount, Is.EqualTo(1));
        Assert.That(aggregate.SpontaneousIntentRunCount, Is.EqualTo(5));
    }

    [Test]
    public void CohortHeaderMismatch_CollapsesEveryBt5NumeratorToZero()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort(index =>
            index == 5
                ? new Bt5Bt10SyntheticRunOptions { BuildManifestHash = "forked-build" }
                : null);

        var aggregate = Bt5DesireCommitScorer.Score(Bt5Bt10SyntheticFixture.Inputs(fixtures));

        Assert.That(aggregate.CohortConsistent, Is.False);
        Assert.That(aggregate.ValidRunCount, Is.Zero);
        Assert.That(aggregate.SpontaneousIntentRunCount, Is.Zero);
        Assert.That(aggregate.ScarceResourceCommitRunCount, Is.Zero);
    }

    [Test]
    public void TamperedPersistedHash_MakesRunInvalidInsteadOfVacuouslyPassing()
    {
        var fixtures = Bt5Bt10SyntheticFixture.CreateCohort().ToArray();
        var original = fixtures[0];
        var entries = original.Trace.Entries.ToArray();
        entries[0] = entries[0] with { ObservationHash = "tampered-observation-hash" };
        var tamperedTrace = original.Trace with { Entries = entries };
        fixtures[0] = Bt5Bt10SyntheticFixture.FromTrace(
            tamperedTrace,
            original.Facts,
            original.Decisions);

        var aggregate = Bt5DesireCommitScorer.Score(Bt5Bt10SyntheticFixture.Inputs(fixtures));

        Assert.That(fixtures[0].Input.Run.Shape.Valid, Is.False);
        Assert.That(aggregate.ValidRunCount, Is.EqualTo(5));
        Assert.That(aggregate.PerRun[0].SpontaneousIntent, Is.False);
        Assert.That(aggregate.PerRun[0].ScarceResourceCommit, Is.False);
    }
}
