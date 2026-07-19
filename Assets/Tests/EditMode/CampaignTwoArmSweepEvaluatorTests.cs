using System.Linq;
using NUnit.Framework;
using SM.Editor.Validation;

namespace SM.Tests.EditMode;

/// <summary>Phase A two-arm fixture 판정 witness. 실 콘텐츠 장시간 sweep와 분리해 arm/gap/band 산술을 고정한다.</summary>
[Category("BatchOnly")]
public sealed class CampaignTwoArmSweepEvaluatorTests
{
    [Test]
    public void DefaultConfig_BindsDistinctArms_AndBuildsCanonical480CellGrid()
    {
        var config = CampaignBalanceSweepConfig.Default;

        config.Validate();

        Assert.That(config.Arms.Select(arm => arm.PolicyId).Distinct().Count(), Is.EqualTo(2));
        Assert.That(config.Arms.Single(arm => arm.ArmId == "naive").PolicyId, Is.EqualTo("greedy-v1"));
        Assert.That(config.Arms.Single(arm => arm.ArmId == "informed").PolicyId, Is.EqualTo("concept-preview-grounded-v1"));
        Assert.That(config.FullGridCellCount, Is.EqualTo(480));
        Assert.That(config.BuildGrid(), Has.Count.EqualTo(480));
        Assert.That(config.Guardrails.BossGapMinimum, Is.EqualTo(.30).Within(.0001));
        Assert.That(config.Guardrails.AuthoredDecisionOpportunityRatioMinimum, Is.EqualTo(1.50).Within(.0001));
        var learning = config.BossLearningSpecs.Single();
        Assert.That(learning.EncounterId, Is.EqualTo("site_wolfpine_trail_boss_1"));
        Assert.That(learning.AnswerTags, Is.EquivalentTo(new[]
        {
            CampaignBossAnswerTag.BacklineGuardAnchor,
            CampaignBossAnswerTag.DurableBackCornerBait,
            CampaignBossAnswerTag.MarkFocusBurst,
        }));
        Assert.That(learning.PatternTaxPI, Is.InRange(12, 18));
        Assert.That(learning.LessonRetryClearRates, Is.EqualTo(new[] { .70, .80 }));
        Assert.That(learning.BossGapMin, Is.EqualTo(.30).Within(.0001));
    }

    [Test]
    public void BossFixture_ComputesDistinctArms_AndPassesBandGapAndAnswerTag()
    {
        var config = FixtureConfig();
        var report = CampaignTwoArmBandEvaluator.EvaluateNode(
            config,
            BossAggregate(
                naiveWins: 3,
                infoWins: 8,
                naiveAnswerTagWins: 3));

        Assert.That(report.Naive.WinRate, Is.EqualTo(.30).Within(.0001));
        Assert.That(report.Informed.WinRate, Is.EqualTo(.80).Within(.0001));
        Assert.That(report.Gap, Is.EqualTo(.50).Within(.0001));
        Assert.That(report.Target.ArmGapBand.Minimum, Is.EqualTo(.30).Within(.0001));
        Assert.That(report.NaiveBandPass, Is.True);
        Assert.That(report.InfoBandPass, Is.True);
        Assert.That(report.GapBandPass, Is.True);
        Assert.That(report.AnswerTagPass, Is.True);
        Assert.That(report.Status, Is.EqualTo("PASS"));
    }

    [Test]
    public void FlatBossFixture_IsReportedAsBaselineGap_NotMeasurementFailure()
    {
        var report = CampaignTwoArmBandEvaluator.EvaluateNode(
            FixtureConfig(),
            BossAggregate(
                naiveWins: 9,
                infoWins: 9,
                naiveAnswerTagWins: 9));

        Assert.That(report.Gap, Is.Zero.Within(.0001));
        Assert.That(report.GapBandPass, Is.False);
        Assert.That(report.Status, Is.EqualTo("BASELINE-GAP"));
    }

    private static CampaignBalanceSweepConfig FixtureConfig()
        => CampaignBalanceSweepConfig.Default with
        {
            Guardrails = CampaignBalanceSweepConfig.Default.Guardrails with { BossGapMinimum = .60 },
            MinimumEffectiveSamplesPerArmPerNode = 10,
            MaximumWilsonHalfWidth = .30,
        };

    private static CampaignTwoArmNodeAggregate BossAggregate(
        int naiveWins,
        int infoWins,
        int naiveAnswerTagWins)
    {
        var config = CampaignBalanceSweepConfig.Default;
        var naive = config.Arms.Single(arm => arm.ArmId == "naive");
        var informed = config.Arms.Single(arm => arm.ArmId == "informed");
        return new CampaignTwoArmNodeAggregate(
            "chapter_ashen_gate",
            ChapterOrder: 1,
            "site_wolfpine_trail",
            SiteOrder: 2,
            "site_wolfpine_trail_boss_1",
            NodeOrder: 4,
            "site_wolfpine_trail_boss_1",
            IsElite: false,
            IsBoss: true,
            new CampaignArmSampleAggregate(naive.ArmId, naive.PolicyId, 10, naiveWins, naiveAnswerTagWins),
            new CampaignArmSampleAggregate(informed.ArmId, informed.PolicyId, 10, infoWins, 0),
            config.ReferenceSquads.Select(squad => new CampaignSquadArmSampleAggregate(
                squad.SquadId,
                new CampaignArmSampleAggregate(naive.ArmId, naive.PolicyId, 10, naiveWins, naiveAnswerTagWins),
                new CampaignArmSampleAggregate(informed.ArmId, informed.PolicyId, 10, infoWins, 0))).ToArray());
    }
}
