using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

public sealed class BattleSimulationSpatialTests
{
    [Test]
    public void Melee_Closes_Before_First_Attack()
    {
        var melee = CombatTestFactory.CreateUnit(
            "ally_melee",
            anchor: DeploymentAnchorId.BackCenter,
            moveSpeed: 2.2f,
            attackRange: 1.1f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_melee",
            race: "undead",
            anchor: DeploymentAnchorId.FrontCenter,
            moveSpeed: 1.8f,
            attackRange: 1.1f);

        var state = CombatTestFactory.CreateBattleState(new[] { melee }, new[] { enemy });
        var simulator = new BattleSimulator(state, 120);

        BattleSimulationStep attackStep = null!;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            if (step.Events.Any(evt => evt.ActionType == BattleActionType.BasicAttack))
            {
                attackStep = step;
                break;
            }
        }

        Assert.That(attackStep, Is.Not.Null);
        var allyView = attackStep.Units.First(unit => unit.Id.Contains("ally_melee"));
        var enemyView = attackStep.Units.First(unit => unit.Id.Contains("enemy_melee"));
        Assert.That(allyView.Position.DistanceTo(enemyView.Position), Is.LessThanOrEqualTo(1.45f));
        Assert.That(allyView.Position.X, Is.GreaterThan(-5.8f));
    }

    [Test]
    public void Ranged_Unit_Maintains_Spacing_While_Attacking()
    {
        var ranger = CombatTestFactory.CreateUnit(
            "ally_ranger",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            moveSpeed: 1.8f,
            attackRange: 3.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_vanguard",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            moveSpeed: 1.7f,
            attackRange: 1.2f);

        var state = CombatTestFactory.CreateBattleState(new[] { ranger }, new[] { enemy });
        var simulator = new BattleSimulator(state, 140);

        BattleSimulationStep attackStep = null!;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            if (step.Events.Any(evt => evt.ActorName == "ally_ranger"))
            {
                attackStep = step;
                break;
            }
        }

        Assert.That(attackStep, Is.Not.Null);
        var allyView = attackStep.Units.First(unit => unit.Id.Contains("ally_ranger"));
        var enemyView = attackStep.Units.First(unit => unit.Id.Contains("enemy_vanguard"));
        Assert.That(allyView.Position.DistanceTo(enemyView.Position), Is.GreaterThan(2.2f));
    }

    [Test]
    public void Healer_Supports_Lowest_Health_Ally()
    {
        var healSkill = new SkillDefinition("heal", "Heal", SkillKind.Heal, 4f, 3f);
        var healer = CombatTestFactory.CreateUnit(
            "ally_healer",
            classId: "mystic",
            anchor: DeploymentAnchorId.BackCenter,
            healPower: 4f,
            attackRange: 2.8f,
            tactics: new[]
            {
                new TacticRule(0, TacticConditionType.AllyHpBelow, 0.7f, BattleActionType.ActiveSkill, TargetSelectorType.LowestHpAlly, healSkill.Id),
                new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
            },
            skills: new[] { healSkill });
        var tank = CombatTestFactory.CreateUnit("ally_tank", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 24f);
        var enemy = CombatTestFactory.CreateUnit("enemy_duelist", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter);

        var state = CombatTestFactory.CreateBattleState(new[] { healer, tank }, new[] { enemy });
        state.Allies[1].TakeDamage(12f);

        var simulator = new BattleSimulator(state, 160);
        BattleEvent? healEvent = null;
        BattleSimulationStep healStep = null!;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            healEvent = step.Events.FirstOrDefault(evt => evt.Note == "heal_skill");
            if (healEvent != null)
            {
                healStep = step;
                break;
            }
        }

        Assert.That(healEvent, Is.Not.Null);
        Assert.That(healEvent!.TargetName, Is.EqualTo("ally_tank"));
        var healerView = healStep.Units.First(unit => unit.Id.Contains("ally_healer"));
        var tankView = healStep.Units.First(unit => unit.Id.Contains("ally_tank"));
        Assert.That(healerView.Position.DistanceTo(tankView.Position), Is.LessThanOrEqualTo(3.25f));
    }

    [Test]
    public void Retarget_Happens_After_Target_Switch_Delay()
    {
        var assassin = CombatTestFactory.CreateUnit(
            "ally_assassin",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontCenter,
            attack: 12f,
            attackCooldown: 0.4f,
            attackWindup: 0.05f,
            targetSwitchDelay: 0.4f);
        var enemyA = CombatTestFactory.CreateUnit("enemy_a", race: "undead", hp: 6f, anchor: DeploymentAnchorId.FrontCenter);
        var enemyB = CombatTestFactory.CreateUnit("enemy_b", race: "undead", hp: 18f, anchor: DeploymentAnchorId.BackCenter);

        var state = CombatTestFactory.CreateBattleState(new[] { assassin }, new[] { enemyA, enemyB });
        var simulator = new BattleSimulator(state, 160);

        BattleEvent? firstKill = null;
        BattleEvent? retargetAttack = null;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            firstKill ??= step.Events.FirstOrDefault(evt => evt.TargetName == "enemy_a");
            if (firstKill != null)
            {
                retargetAttack = step.Events.FirstOrDefault(evt => evt.TargetName == "enemy_b");
                if (retargetAttack != null)
                {
                    break;
                }
            }
        }

        Assert.That(firstKill, Is.Not.Null);
        Assert.That(retargetAttack, Is.Not.Null);
        Assert.That(retargetAttack!.TimeSeconds - firstKill!.TimeSeconds, Is.GreaterThanOrEqualTo(0.4f));
    }
}
