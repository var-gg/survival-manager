using System;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class FormationStageFourFastTests
{
    [Test]
    public void Projector_EmitsTypedFiveChannelEligibilityFiringAndLegibility()
    {
        var battle = new BattleMetricRecord
        {
            RunId = "run",
            BattleId = "battle",
            PolicyId = "policy",
            Seed = 17,
            FlankStrikeCount = 3,
            RearStrikeCount = 1,
            SaveMomentCount = 1,
        };

        var record = FormationMetricProjector.Project(
            battle,
            new FormationEligibilityTracker(),
            "pair",
            "set",
            "variant",
            false,
            true,
            false,
            string.Empty,
            true,
            true);

        Assert.That(record.Channels.Count, Is.EqualTo(5));
        Assert.That(Channel(record, FormationChannelIds.Flank).EventCount, Is.EqualTo(2),
            "activity flank count includes rear, while the five-channel log keeps side flank separate");
        Assert.That(Channel(record, FormationChannelIds.Rear).EventCount, Is.EqualTo(1));
        Assert.That(Channel(record, FormationChannelIds.Save).Fired, Is.True);
        Assert.That(record.Channels.Where(value => value.Fired).All(value => value.Eligible && value.Legible), Is.True);
    }

    [Test]
    public void CausalEvaluator_MarksSameSeedEventPresenceWhenOutcomeChanges()
    {
        var baseline = Record(
            "base",
            isDefault: true,
            isPolicyChoice: false,
            winner: "enemy",
            power: -0.2f,
            firedChannel: null);
        var policy = Record(
            "policy",
            isDefault: false,
            isPolicyChoice: true,
            winner: "ally",
            power: 0.2f,
            firedChannel: FormationChannelIds.Flank);

        var result = FormationCausalEvaluator.Evaluate(new[] { baseline, policy });
        var eventLog = result.EventLogs.Single(value => value.BattleId == "policy"
                                                        && value.ChannelId == FormationChannelIds.Flank);
        Assert.That(eventLog.Eligible, Is.True);
        Assert.That(eventLog.Fired, Is.True);
        Assert.That(eventLog.Causal, Is.True);
        Assert.That(eventLog.Legible, Is.True);
        Assert.That(eventLog.CausalMethod, Is.EqualTo(FormationCausalEvaluator.CausalMethodId));
        Assert.That(result.PolicySummaries.Single().ImpactRate, Is.EqualTo(1d));
    }

    [Test]
    public void PlacementAndHealerEvaluators_EmitMarginalRowsWithoutFixingHealerFrequency()
    {
        var placement = PlacementLeverageEvaluator.Evaluate(new[]
        {
            Record("default-a", true, false, "enemy", -0.1f, null) with
            {
                PlacementSetId = "set", PlacementVariantId = "default", Seed = 1,
            },
            Record("default-b", true, false, "ally", 0.1f, null) with
            {
                PlacementSetId = "set", PlacementVariantId = "default", Seed = 2,
            },
            Record("best-a", false, false, "ally", 0.2f, FormationChannelIds.ScreenBlock) with
            {
                PlacementSetId = "set", PlacementVariantId = "medoid", Seed = 1,
            },
            Record("best-b", false, false, "ally", 0.2f, FormationChannelIds.ScreenBlock) with
            {
                PlacementSetId = "set", PlacementVariantId = "medoid", Seed = 2,
            },
        });
        Assert.That(placement.Records.Single().WinRateLeverage, Is.EqualTo(0.5d));

        var healer = HealerMarginalValueEvaluator.Evaluate(new[]
        {
            Record("with", false, false, "ally", 0.3f, FormationChannelIds.Save) with
            {
                IsHealerComparison = true, HealerComparisonId = "heal", ContainsHealer = true,
                CompetentSelectedHealer = true,
            },
            Record("without", false, false, "enemy", -0.2f, null) with
            {
                IsHealerComparison = true, HealerComparisonId = "heal", ContainsHealer = false,
                CompetentSelectedHealer = true,
            },
        });
        var row = healer.Records.Single();
        Assert.That(row.PositiveMarginalValue, Is.True);
        Assert.That(row.SelectionAligned, Is.True);
        Assert.That(healer.PositiveSelectionAlignmentRate, Is.EqualTo(1d));
    }

    [Test]
    public void Q5Evaluator_FlagsStageFiveWhenCoverageWorksButCompetentDoesNot()
    {
        var coverageChannels = FormationChannelIds.All.Select(id =>
            new FormationPolicySummary.ChannelSummary(id, 1, 1, 0, 1, 1d, 0d, 1d)).ToArray();
        var competentChannels = FormationChannelIds.All.Select(id =>
            new FormationPolicySummary.ChannelSummary(id, 1, 0, 0, 0, 0d, 0d, 0d)).ToArray();
        var causal = new FormationCausalEvaluator.Result(
            Array.Empty<FormationEventLogRecord>(),
            new[]
            {
                new FormationPolicySummary("qa-formation-coverage-v1", 1, 1, 1, 0, 1d, 0d, 1d, coverageChannels),
                new FormationPolicySummary("competent-formation-v1", 1, 1, 0, 0, 0d, 0d, 0d, competentChannels),
            });
        var placement = new PlacementLeverageEvaluator.Result(
            Array.Empty<PlacementLeverageRecord>(), 0d, 0d, 0d, 0d);
        var healer = new HealerMarginalValueEvaluator.Result(
            Array.Empty<HealerMarginalValueRecord>(), 0, 0, 0d);

        var report = FormationQ5Evaluator.Evaluate(
            "run",
            "qa-formation-coverage-v1",
            "competent-formation-v1",
            causal,
            placement,
            healer);

        Assert.That(report.CoveragePass, Is.True);
        Assert.That(report.CompetentQ5Pass, Is.False);
        Assert.That(report.NeedsStageFiveBalance, Is.True);
        Assert.That(report.ChannelsNeedingTuning, Is.EquivalentTo(FormationChannelIds.All));
    }

    private static FormationBattleRecord Record(
        string battleId,
        bool isDefault,
        bool isPolicyChoice,
        string winner,
        float power,
        string? firedChannel)
        => new()
        {
            RunId = "run",
            BattleId = battleId,
            PairingId = "pair",
            PlacementSetId = "set",
            PlacementVariantId = battleId,
            PolicyId = "competent-formation-v1",
            Seed = 1,
            IsDefaultPlacement = isDefault,
            IsPolicyChoice = isPolicyChoice,
            WinnerSide = winner,
            NormalizedFinalPowerDifference = power,
            Channels = FormationChannelIds.All.Select(channelId =>
            {
                var fired = string.Equals(channelId, firedChannel, StringComparison.Ordinal);
                return new FormationBattleRecord.ChannelEvidence(
                    channelId,
                    fired,
                    fired,
                    fired,
                    fired ? 1 : 0,
                    fired ? $"typed {channelId}=1" : string.Empty);
            }).ToArray(),
        };

    private static FormationBattleRecord.ChannelEvidence Channel(
        FormationBattleRecord record,
        string channelId)
        => record.Channels.Single(value => value.ChannelId == channelId);
}
