using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class FriendlySkillPayloadRoutingTests
{
    [TestCase(TeamSide.Ally)]
    [TestCase(TeamSide.Enemy)]
    public void CleansePayload_NeverBenefitsTheResolvedOpponent(TeamSide actorSide)
    {
        var skill = new BattleSkillSpec(
            "skill.cross_side_cleanse",
            "Cross-side cleanse",
            SkillKind.Strike,
            1f,
            10f,
            CleanseProfileId: "break_and_unstoppable");
        var state = CreateTwoSidedState(skill);
        var actor = UnitOnSide(state, actorSide);
        var opponent = UnitOnSide(state, Opposite(actorSide));
        var events = new List<BattleEvent>();

        actor.ApplyStatus(new StatusApplicationSpec("actor.root", "root", 10f, 0f));
        opponent.ApplyStatus(new StatusApplicationSpec("opponent.root", "root", 10f, 0f));

        StatusResolutionService.ApplySkillStatuses(state, actor, opponent, skill, events);

        Assert.That(actor.HasStatus("root"), Is.False);
        Assert.That(actor.HasStatus("unstoppable"), Is.True);
        Assert.That(opponent.HasStatus("root"), Is.True);
        Assert.That(opponent.HasStatus("unstoppable"), Is.False);

        var cleanseEvents = events.Where(@event => @event.EventKind == BattleEventKind.CleanseTriggered).ToList();
        Assert.That(cleanseEvents, Is.Not.Empty);
        Assert.That(cleanseEvents.All(@event => ResolveEventSide(state, @event.ActorId) == ResolveEventSide(state, @event.TargetId)), Is.True,
            "CleanseTriggered의 actor와 target은 항상 같은 편이어야 한다");
    }

    [Test]
    public void DefensiveBoonPayloads_NeverLandOnTheResolvedOpponent()
    {
        foreach (var actorSide in new[] { TeamSide.Ally, TeamSide.Enemy })
        {
            foreach (var (statusId, magnitude) in new[]
                     {
                         ("barrier", 5f),
                         ("guarded", 1f),
                         ("unstoppable", 1f),
                     })
            {
                var skill = new BattleSkillSpec(
                    $"skill.cross_side_{statusId}",
                    $"Cross-side {statusId}",
                    SkillKind.Strike,
                    1f,
                    10f,
                    AppliedStatuses: new[]
                    {
                        new StatusApplicationSpec($"apply.{statusId}", statusId, 10f, magnitude),
                    });
                var state = CreateTwoSidedState(skill);
                var actor = UnitOnSide(state, actorSide);
                var opponent = UnitOnSide(state, Opposite(actorSide));
                var events = new List<BattleEvent>();

                StatusResolutionService.ApplySkillStatuses(state, actor, opponent, skill, events);

                if (string.Equals(statusId, "barrier", StringComparison.Ordinal))
                {
                    Assert.That(actor.Barrier, Is.EqualTo(5f).Within(0.001f));
                    Assert.That(opponent.Barrier, Is.Zero);
                }
                else
                {
                    Assert.That(actor.HasStatus(statusId), Is.True);
                    Assert.That(opponent.HasStatus(statusId), Is.False);
                }

                Assert.That(events
                    .Where(@event => @event.EventKind == BattleEventKind.StatusApplied && @event.PayloadId == statusId)
                    .All(@event => @event.TargetId == actor.Id), Is.True);
            }
        }
    }

    [TestCase(SkillKind.Heal)]
    [TestCase(SkillKind.Shield)]
    public void HealOrShield_TargetingInjuredAlly_LandsDefensiveBoonOnAllyNotCaster(SkillKind kind)
    {
        var skill = new BattleSkillSpec(
            $"skill.allied_{kind.ToString().ToLowerInvariant()}",
            $"Allied {kind}",
            kind,
            0f,
            10f,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec("apply.allied.guarded", "guarded", 10f, 1f),
            });
        var state = CombatTestFactory.CreateBattleState(
            new[]
            {
                CombatTestFactory.CreateUnit("caster", skills: new[] { skill }),
                CombatTestFactory.CreateUnit("injured_ally"),
            },
            new[] { CombatTestFactory.CreateUnit("enemy", race: "undead") });
        var caster = state.Allies.Single(unit => unit.Definition.Id == "caster");
        var injuredAlly = state.Allies.Single(unit => unit.Definition.Id == "injured_ally");
        injuredAlly.TakeDamage(25f);
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, caster, injuredAlly, skill, events);

        Assert.That(injuredAlly.HealthRatio, Is.LessThan(1f));
        Assert.That(injuredAlly.HasStatus("guarded"), Is.True);
        Assert.That(caster.HasStatus("guarded"), Is.False);
        Assert.That(events.Any(@event =>
            @event.EventKind == BattleEventKind.StatusApplied
            && @event.PayloadId == "guarded"
            && @event.TargetId == injuredAlly.Id), Is.True);
    }

    [Test]
    public void MixedStrike_RealBattle_DamagesEnemyButKeepsCleanseAndBarrierFriendly()
    {
        var skill = new BattleSkillSpec(
            "skill.real_mixed_payload",
            "Real mixed payload",
            SkillKind.Strike,
            8f,
            20f,
            PowerFlat: 8f,
            PhysCoeff: 0f,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec("apply.real.barrier", "barrier", 0f, 5f),
            },
            CleanseProfileId: "cleanse_control",
            ResolvedSlotKind: ActionSlotKind.SignatureActive,
            ActivationModel: ActivationModel.Energy,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.EnemyUnit,
                PrimarySelector = TargetSelector.NearestReachableEnemy,
                FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
                Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable,
                MaxAcquireRange = 20f,
            });
        var actorLoadout = CombatTestFactory.CreateLoopAUnit(
            "mixed_actor",
            hp: 100f,
            physPower: 0f,
            armor: 0f,
            attackRange: 20f,
            signatureActive: skill,
            energy: new EnergyProfile(100f, 100f));
        var enemyLoadout = CombatTestFactory.CreateLoopAUnit(
            "mixed_enemy",
            race: "undead",
            hp: 100f,
            physPower: 0f,
            armor: 0f,
            attackRange: 20f,
            energy: new EnergyProfile(100f, 0f));
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { enemyLoadout }, seed: 29);
        var actor = state.Allies.Single();
        var enemy = state.Enemies.Single();
        actor.ApplyStatus(new StatusApplicationSpec("actor.root", "root", 100f, 0f));
        enemy.ApplyStatus(new StatusApplicationSpec("enemy.root", "root", 100f, 0f));
        var simulator = new BattleSimulator(state, 40);

        var resolved = false;
        for (var step = 0; step < 40 && !simulator.IsFinished; step++)
        {
            simulator.Step();
            resolved = state.TelemetryEvents.Any(record =>
                record.EventKind == TelemetryEventKind.SkillCastResolved
                && record.SkillId == skill.Id);
            if (resolved)
            {
                break;
            }
        }

        Assert.That(resolved, Is.True, "실 BattleSimulator에서 혼합 payload 스킬이 실제 시전되어야 한다");
        Assert.That(enemy.CurrentHealth, Is.LessThan(100f), "hostile damage는 적에게 남아야 한다");
        Assert.That(actor.HasStatus("root"), Is.False, "cleanse는 시전자 편에 적용되어야 한다");
        Assert.That(enemy.HasStatus("root"), Is.True, "플레이어가 적에게 건 상태를 혼합 스킬이 지우면 안 된다");
        Assert.That(actor.Barrier, Is.GreaterThan(0f), "barrier는 시전자 편에 적용되어야 한다");
        Assert.That(enemy.Barrier, Is.Zero, "barrier가 적에게 적용되면 안 된다");
    }

    private static BattleState CreateTwoSidedState(BattleSkillSpec skill)
    {
        return CombatTestFactory.CreateBattleState(
            new[] { CombatTestFactory.CreateUnit("ally", skills: new[] { skill }) },
            new[] { CombatTestFactory.CreateUnit("enemy", race: "undead", skills: new[] { skill }) });
    }

    private static UnitSnapshot UnitOnSide(BattleState state, TeamSide side)
    {
        return state.GetTeam(side).Single();
    }

    private static TeamSide ResolveEventSide(BattleState state, EntityId? id)
    {
        return state.AllUnits.Single(unit => unit.Id == id).Side;
    }

    private static TeamSide Opposite(TeamSide side)
    {
        return side == TeamSide.Ally ? TeamSide.Enemy : TeamSide.Ally;
    }
}
