using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CombatContractsTests
{
    [Test]
    public void HitResolution_Resolves_Dodge_Before_Crit_Block_And_Armor()
    {
        var attacker = CreateLoadout(
            "attacker",
            "duelist",
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20f,
                [StatKey.PhysPower] = 10f,
                [StatKey.AttackRange] = 1.2f,
                [StatKey.MoveSpeed] = 2f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.5f,
                [StatKey.LeashDistance] = 6f,
                [StatKey.CritChance] = 1f,
                [StatKey.CritMultiplier] = 1f,
            });
        var defender = CreateLoadout(
            "defender",
            "ranger",
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20f,
                [StatKey.Armor] = 50f,
                [StatKey.AttackRange] = 3f,
                [StatKey.MoveSpeed] = 2f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.5f,
                [StatKey.LeashDistance] = 6f,
            },
            behavior: new BehaviorProfile(0.25f, 0.2f, 0.4f, 0.7f, 0.5f, 0.5f, 1f, 1f, 0.6f, 0.3f));

        var state = BattleFactory.Create(new[] { attacker }, new[] { defender });
        var result = HitResolutionService.ResolveBasicAttack(state, state.Allies[0], state.Enemies[0]);

        Assert.That(result.WasDodged, Is.True);
        Assert.That(result.WasCritical, Is.False);
        Assert.That(result.WasBlocked, Is.False);
        Assert.That(result.Value, Is.EqualTo(0f));
    }

    [Test]
    public void HitResolution_Applies_Crit_Then_Block_Then_Armor()
    {
        var attacker = CreateLoadout(
            "attacker",
            "duelist",
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20f,
                [StatKey.PhysPower] = 10f,
                [StatKey.AttackRange] = 1.2f,
                [StatKey.MoveSpeed] = 2f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.5f,
                [StatKey.LeashDistance] = 6f,
                [StatKey.CritChance] = 1f,
                [StatKey.CritMultiplier] = 1f,
            });
        var defender = CreateLoadout(
            "defender",
            "vanguard",
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 20f,
                [StatKey.Armor] = 2f,
                [StatKey.AttackRange] = 1.2f,
                [StatKey.MoveSpeed] = 1.8f,
                [StatKey.AttackWindup] = 0.1f,
                [StatKey.AttackCooldown] = 0.5f,
                [StatKey.LeashDistance] = 6f,
            },
            behavior: new BehaviorProfile(0.4f, 0.16f, 0.04f, 0.05f, 0.34f, 0.82f, 0f, 1f, 0.5f, 0.88f));

        var state = BattleFactory.Create(new[] { attacker }, new[] { defender });
        var result = HitResolutionService.ResolveBasicAttack(state, state.Allies[0], state.Enemies[0]);

        Assert.That(result.WasDodged, Is.False);
        Assert.That(result.WasCritical, Is.True);
        Assert.That(result.WasBlocked, Is.True);
        Assert.That(result.Value, Is.EqualTo(8.3333f).Within(0.01f));
    }

    private static BattleUnitLoadout CreateLoadout(
        string id,
        string classId,
        Dictionary<StatKey, float> baseStats,
        BehaviorProfile? behavior = null)
    {
        return new BattleUnitLoadout(
            id,
            id,
            "human",
            classId,
            DeploymentAnchorId.FrontCenter,
            baseStats,
            new[]
            {
                new UnitRuleChain("rules", new[]
                {
                    new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy)
                })
            },
            new BattleSkillSpec[0],
            Behavior: behavior);
    }
}
