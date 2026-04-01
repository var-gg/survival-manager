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
        var edgeDistance = allyView.Position.DistanceTo(enemyView.Position) - allyView.NavigationRadius - enemyView.NavigationRadius;
        Assert.That(edgeDistance, Is.LessThanOrEqualTo(1.15f));
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
            healEvent = step.Events.FirstOrDefault(evt => evt.LogCode == BattleLogCode.ActiveSkillHeal);
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

    [Test]
    public void Melee_Slotting_Separates_Attackers_Around_Same_Target()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateUnit("ally_slot_a", anchor: DeploymentAnchorId.FrontTop, moveSpeed: 2.1f, attackRange: 1.1f, attackCooldown: 0.75f),
            CombatTestFactory.CreateUnit("ally_slot_b", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 2.05f, attackRange: 1.1f, attackCooldown: 0.75f),
            CombatTestFactory.CreateUnit("ally_slot_c", anchor: DeploymentAnchorId.FrontBottom, moveSpeed: 2.15f, attackRange: 1.1f, attackCooldown: 0.75f),
        };
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_anchor",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 80f,
            attack: 2f,
            defense: 4f,
            moveSpeed: 1.2f,
            attackCooldown: 1.2f);

        var state = CombatTestFactory.CreateBattleState(allies, new[] { enemy });
        var simulator = new BattleSimulator(state, 120);

        BattleSimulationStep latest = simulator.CurrentStep;
        for (var step = 0; step < 60 && !simulator.IsFinished; step++)
        {
            latest = simulator.Step();
        }

        var allyUnits = latest.Units.Where(unit => unit.Side == TeamSide.Ally).ToList();
        Assert.That(allyUnits, Has.Count.EqualTo(3));
        Assert.That(FindMinDistance(allyUnits), Is.GreaterThan(0.55f));
    }

    [Test]
    public void Ranged_Uses_Mobility_To_Break_Contact_When_Pressed()
    {
        var rangerMobility = new MobilityActionProfile(MobilityStyle.Roll, MobilityPurpose.MaintainRange, 1.6f, 2.5f, 0f, 0.2f, 0f, 1.5f, 0.5f);
        var ranger = CombatTestFactory.CreateUnit(
            "ally_mobile_ranger",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            moveSpeed: 1.8f,
            attackRange: 3.2f,
            preferredDistance: 2.8f,
            attackWindup: 0.08f,
            attackCooldown: 0.55f,
            mobility: rangerMobility);
        var pursuer = CombatTestFactory.CreateUnit(
            "enemy_pursuer",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontCenter,
            moveSpeed: 2.6f,
            attackRange: 1.15f,
            attackCooldown: 0.5f);

        var state = CombatTestFactory.CreateBattleState(new[] { ranger }, new[] { pursuer });
        var simulator = new BattleSimulator(state, 160);

        var sawClosePressure = false;
        var sawMobilityCommit = false;
        var sawRecoveredDistance = false;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            var ally = step.Units.First(unit => unit.Id.Contains("ally_mobile_ranger"));
            var enemyUnit = step.Units.First(unit => unit.Id.Contains("enemy_pursuer"));
            var edgeDistance = ally.Position.DistanceTo(enemyUnit.Position) - ally.NavigationRadius - enemyUnit.NavigationRadius;
            if (edgeDistance < 2f)
            {
                sawClosePressure = true;
            }

            if (state.Allies[0].MobilityCooldownRemaining > 0f)
            {
                sawMobilityCommit = true;
            }

            if (sawClosePressure
                && sawMobilityCommit
                && edgeDistance > 2.6f)
            {
                sawRecoveredDistance = true;
                break;
            }
        }

        Assert.That(sawClosePressure, Is.True);
        Assert.That(sawMobilityCommit, Is.True);
        Assert.That(sawRecoveredDistance, Is.True);
    }

    [Test]
    public void Large_Footprint_Profile_Preserves_Spacing()
    {
        var largeFootprint = new FootprintProfile(
            0.8f,
            1.1f,
            1.3f,
            new FloatRange(0.95f, 1.25f),
            4,
            1.7f,
            BodySizeCategory.Large,
            2.5f);
        var giantA = CombatTestFactory.CreateUnit("ally_giant_a", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 40f, footprint: largeFootprint);
        var giantB = CombatTestFactory.CreateUnit("ally_giant_b", classId: "vanguard", anchor: DeploymentAnchorId.FrontBottom, hp: 40f, footprint: largeFootprint);
        var enemy = CombatTestFactory.CreateUnit("enemy_large_target", race: "undead", classId: "vanguard", hp: 70f, attack: 2f, defense: 4f);

        var state = CombatTestFactory.CreateBattleState(new[] { giantA, giantB }, new[] { enemy });
        var simulator = new BattleSimulator(state, 120);

        BattleSimulationStep latest = simulator.CurrentStep;
        for (var step = 0; step < 50 && !simulator.IsFinished; step++)
        {
            latest = simulator.Step();
        }

        var allies = latest.Units.Where(unit => unit.Side == TeamSide.Ally).ToList();
        Assert.That(FindMinDistance(allies), Is.GreaterThan(1.2f));
    }

    [Test]
    public void Overflow_Slotting_Uses_SecurePosition_When_Target_Slots_Are_Full()
    {
        var targetFootprint = new FootprintProfile(
            0.6f,
            0.85f,
            1.2f,
            new FloatRange(0.95f, 1.2f),
            1,
            1.2f,
            BodySizeCategory.Large,
            2.3f);
        var attackers = new[]
        {
            CombatTestFactory.CreateUnit("ally_overflow_a", anchor: DeploymentAnchorId.FrontTop, moveSpeed: 2.15f, attackRange: 1.1f, attackCooldown: 0.8f),
            CombatTestFactory.CreateUnit("ally_overflow_b", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 2.05f, attackRange: 1.1f, attackCooldown: 0.8f),
            CombatTestFactory.CreateUnit("ally_overflow_c", anchor: DeploymentAnchorId.FrontBottom, moveSpeed: 2.1f, attackRange: 1.1f, attackCooldown: 0.8f),
        };
        var target = CombatTestFactory.CreateUnit(
            "enemy_overflow_target",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 120f,
            attack: 1f,
            defense: 5f,
            moveSpeed: 1.1f,
            attackCooldown: 1.4f,
            footprint: targetFootprint);

        var state = CombatTestFactory.CreateBattleState(attackers, new[] { target });
        var simulator = new BattleSimulator(state, 140);

        var sawSecurePosition = false;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            if (step.Units.Any(unit => unit.Side == TeamSide.Ally && unit.ActionState == CombatActionState.SecurePosition))
            {
                sawSecurePosition = true;
                break;
            }
        }

        Assert.That(sawSecurePosition, Is.True);
    }

    private static float FindMinDistance(System.Collections.Generic.IReadOnlyList<BattleUnitReadModel> units)
    {
        var min = float.MaxValue;
        for (var i = 0; i < units.Count; i++)
        {
            for (var j = i + 1; j < units.Count; j++)
            {
                min = System.Math.Min(min, units[i].Position.DistanceTo(units[j].Position));
            }
        }

        return min;
    }
}
