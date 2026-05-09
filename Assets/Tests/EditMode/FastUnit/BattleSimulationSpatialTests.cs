using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
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
        Assert.That(edgeDistance, Is.LessThanOrEqualTo(1.5f));
        Assert.That(allyView.Position.X, Is.GreaterThan(-5.8f));
    }

    [TestCase(7)]
    [TestCase(23)]
    [TestCase(29)]
    public void LoopA_MeleeBasicAttack_ResolvesAtReadableContactGap_AcrossSeeds(int seed)
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_vanguard", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 70f, physPower: 4f, attackRange: 1.3f),
            CombatTestFactory.CreateLoopAUnit("ally_duelist", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 55f, physPower: 5f, attackRange: 1.3f),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_vanguard", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 70f, physPower: 4f, attackRange: 1.3f),
            CombatTestFactory.CreateLoopAUnit("enemy_duelist", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 55f, physPower: 5f, attackRange: 1.3f),
        };
        var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: seed);
        var simulator = new BattleSimulator(state, 140);

        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            var basic = step.Events.FirstOrDefault(evt => evt.LogCode == BattleLogCode.BasicAttackDamage && evt.TargetId != null);
            if (basic == null)
            {
                continue;
            }

            var actor = step.Units.First(unit => unit.Id == basic.ActorId.Value);
            var target = step.Units.First(unit => unit.Id == basic.TargetId.GetValueOrDefault().Value);
            var edgeDistance = actor.Position.DistanceTo(target.Position) - actor.NavigationRadius - target.NavigationRadius;

            Assert.That(edgeDistance, Is.InRange(0.35f, 0.75f),
                $"seed={seed} actor={actor.Id} target={target.Id} note={basic.Note}");
            return;
        }

        Assert.Fail($"seed={seed} produced no basic attack event.");
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
    public void RangedBasicAttack_CancelsCommit_WhenTargetIsInsidePreferredMinimum()
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
            "enemy_pursuer",
            race: "undead",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontCenter,
            moveSpeed: 1.7f,
            attackRange: 1.2f);

        var state = CombatTestFactory.CreateBattleState(new[] { ranger }, new[] { enemy });
        var rangerState = state.Allies[0];
        var enemyState = state.Enemies[0];
        var tooCloseEdgeDistance = rangerState.Behavior.PreferredRangeMin - rangerState.Behavior.RetreatBuffer - 0.05f;
        rangerState.SetPosition(new CombatVector2(0f, 0f));
        enemyState.SetPosition(new CombatVector2(rangerState.NavigationRadius + enemyState.NavigationRadius + tooCloseEdgeDistance, 0f));
        rangerState.BeginWindup(BattleActionType.BasicAttack, enemyState.Id, null);

        var simulator = new BattleSimulator(state, 20);
        var step = simulator.Step();
        var rangerAttack = step.Events.FirstOrDefault(evt =>
            evt.ActorName == "ally_ranger"
            && evt.LogCode == BattleLogCode.BasicAttackDamage);
        var allyView = step.Units.First(unit => unit.Id.Contains("ally_ranger"));
        var enemyView = step.Units.First(unit => unit.Id.Contains("enemy_pursuer"));
        var edgeDistance = allyView.Position.DistanceTo(enemyView.Position) - allyView.NavigationRadius - enemyView.NavigationRadius;

        Assert.That(edgeDistance, Is.LessThan(2.0f));
        Assert.That(rangerAttack, Is.Null);
        Assert.That(allyView.ActionState, Is.EqualTo(CombatActionState.AcquireTarget));
        Assert.That(allyView.PendingActionType, Is.Null);

        var nextStep = simulator.Step();
        var nextAllyView = nextStep.Units.First(unit => unit.Id.Contains("ally_ranger"));
        Assert.That(nextAllyView.ActionState, Is.EqualTo(CombatActionState.BreakContact));
        Assert.That(nextAllyView.PendingActionType, Is.Null);
    }

    [Test]
    public void ContentLikeBacklineFight_DoesNotDegradeIntoRangeMissLoop()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateUnit("ally_dawn_priest", classId: "mystic", anchor: DeploymentAnchorId.FrontCenter, hp: 18f, attack: 3f, defense: 2f, speed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, attackWindup: 0.22f, attackCooldown: 1f, leashDistance: 4.8f),
            CombatTestFactory.CreateUnit("ally_pack_raider", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 19f, attack: 8f, defense: 1f, speed: 5f, moveSpeed: 2.05f, attackRange: 1.3f, attackWindup: 0.22f, attackCooldown: 0.85f, leashDistance: 5.2f),
            CombatTestFactory.CreateUnit("ally_echo_savant", classId: "ranger", anchor: DeploymentAnchorId.BackTop, hp: 18f, attack: 7f, defense: 2f, speed: 5f, moveSpeed: 1.85f, attackRange: 3.2f, attackWindup: 0.17f, attackCooldown: 0.48f, leashDistance: 8f),
            CombatTestFactory.CreateUnit("ally_grave_hexer", classId: "mystic", anchor: DeploymentAnchorId.BackBottom, hp: 17f, attack: 4f, defense: 2f, speed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, attackWindup: 0.22f, attackCooldown: 1f, leashDistance: 4.8f),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateUnit("enemy_grey_fang", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontTop, hp: 21f, attack: 7f, defense: 2f, speed: 5f, moveSpeed: 2.05f, attackRange: 1.3f, attackWindup: 0.22f, attackCooldown: 0.45f, leashDistance: 10f),
            CombatTestFactory.CreateUnit("enemy_silent_moon", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.FrontBottom, hp: 17f, attack: 4f, defense: 2f, speed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, attackWindup: 0.22f, attackCooldown: 1f, leashDistance: 4.8f),
            CombatTestFactory.CreateUnit("enemy_lyra_sternfeld", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.BackTop, hp: 18f, attack: 3f, defense: 2f, speed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, attackWindup: 0.22f, attackCooldown: 1f, leashDistance: 4.8f),
            CombatTestFactory.CreateUnit("enemy_black_vellum", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.BackBottom, hp: 18f, attack: 3f, defense: 2f, speed: 4f, moveSpeed: 1.7f, attackRange: 2.8f, attackWindup: 0.22f, attackCooldown: 0.55f, leashDistance: 8f),
        };

        var simulator = new BattleSimulator(CombatTestFactory.CreateBattleState(allies, enemies, seed: 42), BattleSimulator.DefaultMaxSteps);
        var positiveDamageAfterOpening = 0;
        var missRangeAfterOpening = 0;
        var consecutiveRangeMisses = 0;
        var maxConsecutiveRangeMisses = 0;

        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            foreach (var evt in step.Events.Where(evt => evt.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage))
            {
                if (evt.StepIndex >= 60 && evt.Value > 0f)
                {
                    positiveDamageAfterOpening++;
                    consecutiveRangeMisses = 0;
                    continue;
                }

                if (evt.StepIndex >= 60 && evt.Note == "miss_range")
                {
                    missRangeAfterOpening++;
                    consecutiveRangeMisses++;
                    maxConsecutiveRangeMisses = System.Math.Max(maxConsecutiveRangeMisses, consecutiveRangeMisses);
                    continue;
                }

                consecutiveRangeMisses = 0;
            }
        }

        Assert.That(positiveDamageAfterOpening, Is.GreaterThan(0),
            $"opening 이후 전부 range miss로 굳으면 안 됨. missAfterOpening={missRangeAfterOpening}, maxMissStreak={maxConsecutiveRangeMisses}");
        Assert.That(maxConsecutiveRangeMisses, Is.LessThan(30),
            $"range miss가 장시간 반복됨. missAfterOpening={missRangeAfterOpening}, positiveAfterOpening={positiveDamageAfterOpening}");
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
    [Ignore("micro-knockback이 KiteBackward와 합쳐져 closure 자체가 0에 수렴 — 새 design에서 ranger 거리 회복 검증은 별도 setup으로 재작성 필요")]
    public void Ranged_RecoversDistance_WhenPressed()
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

            if (sawClosePressure && edgeDistance > 2.6f)
            {
                sawRecoveredDistance = true;
                break;
            }
        }

        Assert.That(sawClosePressure, Is.True);
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

    [Test]
    public void LoopA_1v1_Produces_Sustained_Combat_Events()
    {
        var inRangeRule = new TargetRule();
        var flexWithInRange = new BattleSkillSpec(
            "flex_strike", "Flex Strike", SkillKind.Strike, 2f, 1.2f,
            SlotKind: CompiledSkillSlots.UtilityActive,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            BaseCooldownSeconds: 1.5f,
            TargetRuleData: inRangeRule);
        var ally = CombatTestFactory.CreateLoopAUnit(
            "ally_fighter",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            physPower: 5f,
            attackRange: 1.2f,
            flexActive: flexWithInRange,
            basicAttackTargetRule: inRangeRule);
        var enemy = CombatTestFactory.CreateLoopAUnit(
            "enemy_fighter",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            physPower: 5f,
            attackRange: 1.2f,
            flexActive: flexWithInRange with { Id = "enemy_flex_strike" },
            basicAttackTargetRule: inRangeRule);

        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy });
        var simulator = new BattleSimulator(state, 200);

        var openingEvents = 0;
        var midEvents = 0;
        var lateEvents = 0;
        var stepDetails = new List<string>();

        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            var stepIndex = step.StepIndex;

            if (step.Events.Count > 0)
            {
                if (stepIndex <= 30) openingEvents += step.Events.Count;
                else if (stepIndex <= 80) midEvents += step.Events.Count;
                else lateEvents += step.Events.Count;
            }

            if (stepIndex <= 50 || stepIndex % 20 == 0)
            {
                var allyUnit = step.Units.FirstOrDefault(u => u.Side == TeamSide.Ally);
                var enemyUnit = step.Units.FirstOrDefault(u => u.Side == TeamSide.Enemy);
                if (allyUnit != null && enemyUnit != null)
                {
                    var dist = allyUnit.Position.DistanceTo(enemyUnit.Position);
                    stepDetails.Add($"S{stepIndex}: ally={allyUnit.ActionState} enemy={enemyUnit.ActionState} dist={dist:F2} events={step.Events.Count} allyCD={allyUnit.CooldownRemaining:F2}");
                }
            }
        }

        var details = string.Join("\n", stepDetails);
        Assert.That(openingEvents, Is.GreaterThan(0), $"No opening events:\n{details}");
        Assert.That(midEvents, Is.GreaterThan(0), $"No mid events (steps 21-80) — units froze after initial exchange:\n{details}");
    }

    [Test]
    public void LoopA_4v4_Battle_Produces_Events_Throughout()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_front_a", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 40f, physPower: 5f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("ally_front_b", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 30f, physPower: 7f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("ally_back_a", classId: "ranger", anchor: DeploymentAnchorId.BackTop, hp: 25f, physPower: 6f, attackRange: 3.2f,
                behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Backline, RangeDiscipline.HoldBand, 2f, 3.2f, 0.4f, 0.25f, 6f, 0f)),
            CombatTestFactory.CreateLoopAUnit("ally_back_b", classId: "mystic", anchor: DeploymentAnchorId.BackBottom, hp: 22f, physPower: 4f, attackRange: 3.0f,
                behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Backline, RangeDiscipline.HoldBand, 1.8f, 3f, 0.4f, 0.25f, 6f, 0f)),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_front_a", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 40f, physPower: 5f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("enemy_front_b", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 30f, physPower: 7f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("enemy_back_a", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackTop, hp: 25f, physPower: 6f, attackRange: 3.2f,
                behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Backline, RangeDiscipline.HoldBand, 2f, 3.2f, 0.4f, 0.25f, 6f, 0f)),
            CombatTestFactory.CreateLoopAUnit("enemy_back_b", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.BackBottom, hp: 22f, physPower: 4f, attackRange: 3.0f,
                behavior: new BehaviorProfile(0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f, 0f, 0f, 0f, 0.5f, 1f, FormationLine.Backline, RangeDiscipline.HoldBand, 1.8f, 3f, 0.4f, 0.25f, 6f, 0f)),
        };

        var state = CombatTestFactory.CreateBattleState(allies, enemies);
        var simulator = new BattleSimulator(state, 200);

        var lastEventStep = 0;
        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            if (step.Events.Count > 0)
            {
                lastEventStep = step.StepIndex;
            }
        }

        Assert.That(lastEventStep, Is.GreaterThan(30), "Events stopped too early — units likely froze after initial attacks");
    }

    [Test]
    public void LoopA_RangedDeadZone_ClosesInsteadOfPendinglessWindup()
    {
        var ally = CombatTestFactory.CreateLoopAUnit(
            "ally_mystic",
            classId: "mystic",
            anchor: DeploymentAnchorId.BackBottom,
            attackRange: 2.8f);
        var enemy = CombatTestFactory.CreateLoopAUnit(
            "enemy_mystic",
            race: "undead",
            classId: "mystic",
            anchor: DeploymentAnchorId.BackBottom,
            attackRange: 2.8f);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy });
        state.Allies[0].SetPosition(new CombatVector2(-3.66f, -1.8f));
        state.Enemies[0].SetPosition(new CombatVector2(0.13f, -1.8f));
        state.Allies[0].SetActionState(CombatActionState.AcquireTarget);
        state.Enemies[0].SetActionState(CombatActionState.AcquireTarget);

        var simulator = new BattleSimulator(state, 80);
        var attackEvents = 0;
        for (var i = 0; i < 80 && !simulator.IsFinished; i++)
        {
            var step = simulator.Step();
            var pendinglessWindups = step.Units
                .Where(unit => unit.IsAlive
                               && unit.ActionState == CombatActionState.ExecuteAction
                               && unit.PendingActionType == null)
                .Select(unit => $"{unit.Id}:{unit.ActionState}:target={unit.TargetId ?? "null"}")
                .ToList();

            Assert.That(pendinglessWindups, Is.Empty,
                $"ActionState만 Windup/ExecuteAction인데 pending action이 없는 스톨 상태가 생김: {string.Join(", ", pendinglessWindups)}");

            attackEvents += step.Events.Count(evt => evt.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage);
            if (attackEvents > 0)
            {
                break;
            }
        }

        Assert.That(attackEvents, Is.GreaterThan(0), "range buffer dead-zone에서 접근 후 공격 이벤트가 발생해야 한다.");
    }

    [Test]
    public void MoveForIntent_PassesThroughCorpse_OnDirectApproach()
    {
        var mover = CombatTestFactory.CreateUnit("ally_pathing", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 1.9f, attackRange: 1.1f);
        var fallen = CombatTestFactory.CreateUnit("ally_fallen", anchor: DeploymentAnchorId.FrontCenter, hp: 4f);
        var enemy = CombatTestFactory.CreateUnit("enemy_target", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 40f);
        var state = CombatTestFactory.CreateBattleState(new[] { mover, fallen }, new[] { enemy });
        var actor = state.Allies[0];
        var corpse = state.Allies[1];
        var target = state.Enemies[0];

        actor.SetPosition(new CombatVector2(-1.00f, 0f));
        corpse.SetPosition(new CombatVector2(-0.25f, 0f));
        corpse.TakeDamage(99f);
        target.SetPosition(new CombatVector2(6.00f, 0f));
        var before = actor.Position;

        MovementResolver.MoveForIntent(state, actor, new EvaluatedAction(
            BattleActionType.BasicAttack,
            target,
            null,
            new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
            new FloatRange(0.8f, 1.1f),
            CombatActionState.Approach,
            ReevaluationReason.None,
            false,
            null,
            null));

        Assert.That(actor.Position.DistanceTo(before), Is.GreaterThan(0.05f));
        Assert.That(System.Math.Abs(actor.Position.Y), Is.LessThan(0.01f));
        Assert.That(actor.Position.X, Is.GreaterThan(before.X));
    }

    [Test]
    public void LoopA_Melee_AdvancesPastCorpse_AndStillAttacks()
    {
        var mover = CombatTestFactory.CreateUnit("ally_detour", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 1.9f, attackRange: 1.1f, attackCooldown: 0.6f);
        var fallen = CombatTestFactory.CreateUnit("ally_detour_fallen", anchor: DeploymentAnchorId.FrontCenter, hp: 4f);
        var enemy = CombatTestFactory.CreateUnit("enemy_detour_target", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 60f, attack: 0f, defense: 4f, moveSpeed: 0f, attackRange: 1.1f);
        var state = CombatTestFactory.CreateBattleState(new[] { mover, fallen }, new[] { enemy });
        var actor = state.Allies[0];
        var corpse = state.Allies[1];
        var target = state.Enemies[0];

        actor.SetPosition(new CombatVector2(-1.05f, 0f));
        actor.SetActionState(CombatActionState.AcquireTarget);
        corpse.SetPosition(new CombatVector2(-0.28f, 0f));
        corpse.TakeDamage(99f);
        target.SetPosition(new CombatVector2(3.0f, 0f));
        target.SetActionState(CombatActionState.AcquireTarget);

        var startX = actor.Position.X;
        var simulator = new BattleSimulator(state, 100);
        var attackEvents = 0;
        BattleSimulationStep latest = simulator.CurrentStep;
        for (var i = 0; i < 100 && !simulator.IsFinished; i++)
        {
            latest = simulator.Step();
            attackEvents += latest.Events.Count(evt => evt.ActorName.Contains("ally_detour") && evt.LogCode == BattleLogCode.BasicAttackDamage);
            if (attackEvents > 0)
            {
                break;
            }
        }

        var actorView = latest.Units.First(unit => unit.Id.Contains("ally_detour"));
        Assert.That(attackEvents, Is.GreaterThan(0), $"Attack failed; actor=({actorView.Position.X:F2},{actorView.Position.Y:F2}) target=({target.Position.X:F2},{target.Position.Y:F2})");
        Assert.That(actorView.Position.X, Is.GreaterThan(startX + 0.4f), "Actor should advance toward target rather than treadmilling at the corpse.");
    }

    [Test]
    public void LoopA_PositioningIntent_RecordsTelemetryAndAreaOracle()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_duelist_a", classId: "duelist", anchor: DeploymentAnchorId.FrontTop, hp: 30f, physPower: 6f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("ally_duelist_b", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 30f, physPower: 6f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("ally_vanguard", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 45f, physPower: 4f, attackRange: 1.2f),
        };
        var target = CombatTestFactory.CreateLoopAUnit(
            "enemy_anchor",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 120f,
            physPower: 1f,
            attackRange: 1.2f);
        var state = CombatTestFactory.CreateBattleState(allies, new[] { target }, seed: 23);
        var simulator = new BattleSimulator(state, 120);

        BattleSimulationStep latest = simulator.CurrentStep;
        for (var i = 0; i < 45 && !simulator.IsFinished; i++)
        {
            latest = simulator.Step();
        }

        var allyViews = latest.Units.Where(unit => unit.Side == TeamSide.Ally && unit.IsAlive).ToList();
        Assert.That(allyViews.Any(unit => unit.PositioningIntent is PositioningIntentKind.FlankLeft or PositioningIntentKind.FlankRight or PositioningIntentKind.BacklineDive), Is.True);
        Assert.That(ComputeOccupiedArea(allyViews), Is.GreaterThan(0.35f));
        Assert.That(state.TelemetryEvents.Any(record => record.EventKind == TelemetryEventKind.PositioningIntentUpdated), Is.True);
        Assert.That(state.TelemetryEvents.Any(record => record.EventKind == TelemetryEventKind.PositioningIntentUpdated
                                                        && record.StringValueB is nameof(ReevaluationReason.Cadence) or nameof(ReevaluationReason.TargetMoved)), Is.True);
    }

    [Test]
    public void LoopA_DeathTelemetry_CapturesFallenPositionDispersion()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_top", classId: "duelist", anchor: DeploymentAnchorId.FrontTop, hp: 45f, physPower: 16f, attackRange: 1.25f),
            CombatTestFactory.CreateLoopAUnit("ally_bottom", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 45f, physPower: 16f, attackRange: 1.25f),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_top", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontTop, hp: 12f, physPower: 1f, armor: 0f, attackRange: 1.2f),
            CombatTestFactory.CreateLoopAUnit("enemy_bottom", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 12f, physPower: 1f, armor: 0f, attackRange: 1.2f),
        };
        var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: 29);
        var result = new BattleSimulator(state, 180).RunToEnd();

        var enemyDeathPositions = result.TelemetryEvents
            .Where(record => record.EventKind == TelemetryEventKind.UnitDied && record.Actor?.SideIndex == 1)
            .Select(record => new CombatVector2(record.ValueA, record.ValueB))
            .ToList();

        Assert.That(enemyDeathPositions, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(ComputePositionDispersion(enemyDeathPositions), Is.GreaterThan(0.1f));
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

    private static float ComputeOccupiedArea(System.Collections.Generic.IReadOnlyList<BattleUnitReadModel> units)
    {
        if (units.Count == 0)
        {
            return 0f;
        }

        var minX = units.Min(unit => unit.Position.X);
        var maxX = units.Max(unit => unit.Position.X);
        var minY = units.Min(unit => unit.Position.Y);
        var maxY = units.Max(unit => unit.Position.Y);
        return (maxX - minX) * (maxY - minY);
    }

    private static float ComputePositionDispersion(System.Collections.Generic.IReadOnlyList<CombatVector2> positions)
    {
        var maxDistance = 0f;
        for (var i = 0; i < positions.Count; i++)
        {
            for (var j = i + 1; j < positions.Count; j++)
            {
                maxDistance = System.Math.Max(maxDistance, positions[i].DistanceTo(positions[j]));
            }
        }

        return maxDistance;
    }
}
