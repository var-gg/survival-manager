using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CampaignEnvelopeServiceFastTests
{
    [Test]
    public void BuildEnemyChapterPackages_ChapterOneIsInertEvenAtSiteTwo()
    {
        var authored = new CampaignChapterBalanceTemplate(1f, 1f, 0.01f, 0.005f);

        Assert.That(CampaignEnvelopeService.BuildEnemyChapterPackages(1, authored, 1), Is.Empty);
        Assert.That(CampaignEnvelopeService.BuildEnemyChapterPackages(1, authored, 2), Is.Empty);
    }

    [Test]
    public void BuildEnemyChapterPackages_ChapterFiveUsesAuthoredCenterSiteStepAndHardCaps()
    {
        var authored = new CampaignChapterBalanceTemplate(1.12f, 1.08f, 0.01f, 0.005f);
        var siteOne = ByStat(CampaignEnvelopeService.BuildEnemyChapterPackages(5, authored, 1));
        var siteTwo = ByStat(CampaignEnvelopeService.BuildEnemyChapterPackages(5, authored, 2));

        Assert.That(siteOne[StatKey.MaxHealth].Value, Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(siteOne[StatKey.PhysPower].Value, Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(siteOne[StatKey.MagPower].Value, Is.EqualTo(0.08f).Within(0.0001f));
        Assert.That(siteTwo[StatKey.MaxHealth].Value, Is.EqualTo(0.1312f).Within(0.0001f));
        Assert.That(siteTwo[StatKey.PhysPower].Value, Is.EqualTo(0.0854f).Within(0.0001f));

        var capped = ByStat(CampaignEnvelopeService.BuildEnemyChapterPackages(
            5,
            new CampaignChapterBalanceTemplate(2f, 2f, 1f, 1f),
            2));
        Assert.That(capped[StatKey.MaxHealth].Value, Is.EqualTo(0.14f).Within(0.0001f));
        Assert.That(capped[StatKey.PhysPower].Value, Is.EqualTo(0.10f).Within(0.0001f));
    }

    [Test]
    public void CampaignAndEndlessPackages_ComposeThroughSameEnemyNumericChannel()
    {
        var enemy = new BattleUnitLoadout(
            "enemy",
            "Enemy",
            "undead",
            "vanguard",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>(),
            Array.Empty<UnitRuleChain>(),
            Array.Empty<BattleSkillSpec>());
        var campaign = CampaignEnvelopeService.BuildEnemyChapterPackages(
            3,
            new CampaignChapterBalanceTemplate(1.06f, 1.04f, 0.01f, 0.005f));
        var withCampaign = PoliticalCombatConditionService.ApplyEnemyPackages(new[] { enemy }, campaign);
        var composed = PoliticalCombatConditionService.ApplyEnemyPackages(
            withCampaign,
            EndlessCycleService.BuildEnemyHeatPackages(2));

        Assert.That(
            composed.Single().NumericPackages.Select(value => value.SourceId),
            Is.EquivalentTo(new[] { "campaign_envelope:c3", "endless_heat:h2" }));
    }

    private static IReadOnlyDictionary<StatKey, StatModifier> ByStat(
        IReadOnlyList<CombatModifierPackage> packages)
    {
        Assert.That(packages.Count, Is.EqualTo(1));
        Assert.That(packages[0].SourceId, Does.StartWith("campaign_envelope:c"));
        Assert.That(packages[0].Modifiers.All(value => value.Op == ModifierOp.Increased), Is.True);
        return packages[0].Modifiers.ToDictionary(value => value.Stat, value => value);
    }
}
