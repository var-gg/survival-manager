using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Stats;
using SM.Meta;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PoliticalCombatConditionServiceTests
{
    private const string Council = "faction_north_council";
    private const string Militia = "faction_free_militia";

    [Test]
    public void Resolve_NoPledgedWarrant_Empty()
    {
        Assert.That(PoliticalCombatConditionService.Resolve(string.Empty, _ => 999), Is.Empty);
    }

    [Test]
    public void Resolve_NonPoliticalWarrant_Empty()
    {
        // build축 서약(issuer/opposed 없음)은 standing이 아무리 극단이어도 정치 조건 없음.
        Assert.That(PoliticalCombatConditionService.Resolve(WarrantCatalog.IntactId, _ => 999), Is.Empty);
        Assert.That(PoliticalCombatConditionService.Resolve(WarrantCatalog.IntactId, _ => -999), Is.Empty);
    }

    [Test]
    public void Resolve_HighIssuerTrust_OnlySupport()
    {
        var conditions = PoliticalCombatConditionService.Resolve(
            WarrantCatalog.CouncilMandateId,
            factionId => factionId == Council ? PoliticalCombatConditionService.SupportTrustThreshold : 0);

        Assert.That(conditions.Count, Is.EqualTo(1));
        Assert.That(conditions[0].Channel, Is.EqualTo(PoliticalChannel.AllySupport));
        Assert.That(conditions[0].SourceFactionId, Is.EqualTo(Council));
        Assert.That(conditions[0].ReasonCode, Is.EqualTo(PoliticalCombatConditionService.SupportReasonCode));
    }

    [Test]
    public void Resolve_LowOpposedStanding_OnlyAlert()
    {
        var conditions = PoliticalCombatConditionService.Resolve(
            WarrantCatalog.CouncilMandateId,
            factionId => factionId == Militia ? PoliticalCombatConditionService.AlertStandingThreshold : 0);

        Assert.That(conditions.Count, Is.EqualTo(1));
        Assert.That(conditions[0].Channel, Is.EqualTo(PoliticalChannel.EnemyAlertness));
        Assert.That(conditions[0].SourceFactionId, Is.EqualTo(Militia));
        Assert.That(conditions[0].ReasonCode, Is.EqualTo(PoliticalCombatConditionService.AlertReasonCode));
    }

    [Test]
    public void Resolve_HighIssuerAndLowOpposed_BothChannels()
    {
        // 서약 2회 이행 시점: issuer +4(지원) AND opposed −2(경계) → 양방향 1 cycle.
        var conditions = PoliticalCombatConditionService.Resolve(
            WarrantCatalog.CouncilMandateId,
            factionId => factionId switch
            {
                Council => PoliticalCombatConditionService.SupportTrustThreshold,
                Militia => PoliticalCombatConditionService.AlertStandingThreshold,
                _ => 0,
            });

        Assert.That(conditions.Count, Is.EqualTo(2));
        Assert.That(conditions.Any(c => c.Channel == PoliticalChannel.AllySupport), Is.True);
        Assert.That(conditions.Any(c => c.Channel == PoliticalChannel.EnemyAlertness), Is.True);
    }

    [Test]
    public void Resolve_NeitherThreshold_Empty()
    {
        // issuer 신뢰 부족 + opposed 적대 미달 → 조건 없음.
        Assert.That(PoliticalCombatConditionService.Resolve(WarrantCatalog.CouncilMandateId, _ => 0), Is.Empty);
    }

    [Test]
    public void AllyAndEnemyPackages_SplitByChannel()
    {
        var conditions = PoliticalCombatConditionService.Resolve(
            WarrantCatalog.CouncilMandateId,
            factionId => factionId switch
            {
                Council => PoliticalCombatConditionService.SupportTrustThreshold,
                Militia => PoliticalCombatConditionService.AlertStandingThreshold,
                _ => 0,
            });

        var ally = PoliticalCombatConditionService.AllyPackages(conditions);
        var enemy = PoliticalCombatConditionService.EnemyPackages(conditions);
        Assert.That(ally.Count, Is.EqualTo(1));
        Assert.That(ally[0].SourceId, Is.EqualTo($"faction_support:{Council}"));
        Assert.That(enemy.Count, Is.EqualTo(1));
        Assert.That(enemy[0].SourceId, Is.EqualTo($"faction_alert:{Militia}"));
    }

    [Test]
    public void ApplyEnemyPackages_FoldsIntoEveryEnemy()
    {
        var enemies = new[] { MakeEnemy("enemy-1"), MakeEnemy("enemy-2") };
        var package = new CombatModifierPackage($"faction_alert:{Militia}", ModifierSource.Other, new[]
        {
            new StatModifier(StatKey.MaxHealth, ModifierOp.Flat, 2f, ModifierSource.Other, $"faction_alert:{Militia}"),
        });

        var result = PoliticalCombatConditionService.ApplyEnemyPackages(enemies, new[] { package });

        Assert.That(
            result.All(enemy => enemy.NumericPackages.Any(p => p.SourceId == $"faction_alert:{Militia}")),
            Is.True,
            "경계 package는 적 전원에 접혀야 한다");
    }

    [Test]
    public void ApplyEnemyPackages_NoPackages_ReturnsInputUnchanged()
    {
        var enemies = new[] { MakeEnemy("enemy-1") };
        Assert.That(
            PoliticalCombatConditionService.ApplyEnemyPackages(enemies, Array.Empty<CombatModifierPackage>()),
            Is.SameAs(enemies));
    }

    private static BattleUnitLoadout MakeEnemy(string id) => new(
        id,
        id,
        "human",
        "vanguard",
        DeploymentAnchorId.FrontCenter,
        new Dictionary<StatKey, float>(),
        Array.Empty<UnitRuleChain>(),
        Array.Empty<BattleSkillSpec>());
}
