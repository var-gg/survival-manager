using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class StatusResolutionServiceTests
{
    [Test]
    public void ReapplyPolicy_UsesSingleSlotMaxMagnitudeBoundedStacksAndLatestSource()
    {
        var weakBurn = new BattleSkillSpec(
            "skill.burn.weak",
            "skill.burn.weak",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.burn.weak", "burn", 2f, 2f, 3) });
        var strongBurn = new BattleSkillSpec(
            "skill.burn.strong",
            "skill.burn.strong",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.burn.strong", "burn", 1f, 5f, 3) });
        var actorA = CombatTestFactory.CreateUnit("actor_a", classId: "mystic", skills: new[] { weakBurn });
        var actorB = CombatTestFactory.CreateUnit("actor_b", classId: "mystic", skills: new[] { strongBurn });
        var targetLoadout = CombatTestFactory.CreateUnit("target");
        var state = CombatTestFactory.CreateBattleState(new[] { actorA, actorB }, new[] { targetLoadout });
        var firstActor = state.Allies[0];
        var secondActor = state.Allies[1];
        var target = state.Enemies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, firstActor, target, weakBurn, events);
        StatusResolutionService.ApplySkillStatuses(state, secondActor, target, strongBurn, events);

        var burn = target.Statuses.Single(status => status.StatusId == "burn");
        Assert.That(burn.Stacks, Is.EqualTo(2));
        Assert.That(burn.RemainingSeconds, Is.EqualTo(2f).Within(0.001f));
        Assert.That(burn.DurationSeconds, Is.EqualTo(2f).Within(0.001f));
        Assert.That(burn.Magnitude, Is.EqualTo(5f).Within(0.001f));
        Assert.That(burn.SourceActorId, Is.EqualTo(secondActor.Id.Value));
        Assert.That(burn.SourceSkillId, Is.EqualTo("skill.burn.strong"));
        Assert.That(burn.SourceApplicationId, Is.EqualTo("apply.burn.strong"));
    }

    [Test]
    public void PeriodicDamageTelemetry_AttributesTickToStatusSource()
    {
        var burnSkill = new BattleSkillSpec(
            "skill.burn",
            "skill.burn",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.burn", "burn", 2f, 3f) });
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { burnSkill });
        var targetLoadout = CombatTestFactory.CreateUnit("target", hp: 20f);
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { targetLoadout });
        var actor = state.Allies.Single();
        var target = state.Enemies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, burnSkill, events);
        events.Clear();
        StatusResolutionService.AdvanceStatuses(state, events);

        var tick = events.Single(@event => @event.Note == "status_tick");
        Assert.That(tick.ActorId, Is.EqualTo(actor.Id));
        Assert.That(tick.TargetId, Is.EqualTo(target.Id));
        Assert.That(tick.PayloadId, Is.EqualTo("burn"));

        var telemetry = state.TelemetryEvents.Last(record => record.StringValueA == "status_tick");
        Assert.That(telemetry.Actor!.UnitInstanceId, Is.EqualTo(actor.Id.Value));
        Assert.That(telemetry.Target!.UnitInstanceId, Is.EqualTo(target.Id.Value));
        Assert.That(telemetry.StatusId, Is.EqualTo("burn"));
        Assert.That(telemetry.Explain!.SourceContentId, Is.EqualTo("burn"));
    }

    [Test]
    public void HardControl_TenacityAndDrWindow_ReduceReappliedDuration()
    {
        var targetBase = CombatTestFactory.CreateUnit("target");
        var targetStats = new Dictionary<StatKey, float>(targetBase.BaseStats)
        {
            [StatKey.Tenacity] = 0.5f,
        };
        var targetLoadout = targetBase with { BaseStats = targetStats };

        var stunSkill = new BattleSkillSpec(
            "skill.stun",
            "skill.stun",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.stun", "stun", 2f, 0f) });
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { stunSkill });
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { targetLoadout });
        var actor = state.Allies.Single();
        var target = state.Enemies.Single();
        var applyEvents = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, stunSkill, applyEvents);

        Assert.That(target.HasStatus("stun"), Is.True);
        Assert.That(target.Statuses.Single(status => status.StatusId == "stun").RemainingSeconds, Is.EqualTo(1f).Within(0.001f));

        var advanceEvents = new List<BattleEvent>();
        for (var index = 0; index < 10; index++)
        {
            StatusResolutionService.AdvanceStatuses(state, advanceEvents);
        }

        Assert.That(target.HasStatus("stun"), Is.False);
        Assert.That(target.ControlResistWindow, Is.Not.Null);
        Assert.That(advanceEvents.Any(@event => @event.EventKind == BattleEventKind.ControlResistApplied && @event.PayloadId == "stun"), Is.True);

        applyEvents.Clear();
        StatusResolutionService.ApplySkillStatuses(state, actor, target, stunSkill, applyEvents);

        Assert.That(target.Statuses.Single(status => status.StatusId == "stun").RemainingSeconds, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void BreakAndUnstoppable_CleansesHardControl_AndAppliesUnstoppableWindow()
    {
        var selfCleanse = new BattleSkillSpec(
            "skill.break",
            "skill.break",
            SkillKind.Utility,
            0f,
            0f,
            CleanseProfileId: "break_and_unstoppable");
        var actorLoadout = CombatTestFactory.CreateUnit("actor", skills: new[] { selfCleanse });
        var enemyLoadout = CombatTestFactory.CreateUnit("enemy");
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { enemyLoadout });
        var actor = state.Allies.Single();
        var events = new List<BattleEvent>();

        actor.ApplyStatus(new StatusApplicationSpec("apply.root", "root", 2f, 0f));
        actor.ApplyStatus(new StatusApplicationSpec("apply.burn", "burn", 3f, 2f));

        StatusResolutionService.ApplySkillStatuses(state, actor, actor, selfCleanse, events);

        Assert.That(actor.HasStatus("root"), Is.False);
        Assert.That(actor.HasStatus("unstoppable"), Is.True);
        Assert.That(actor.ControlResistWindow, Is.Not.Null);
        Assert.That(events.Any(@event => @event.EventKind == BattleEventKind.CleanseTriggered && @event.PayloadId == "break_and_unstoppable"), Is.True);
        Assert.That(events.Any(@event => @event.EventKind == BattleEventKind.ControlResistApplied), Is.True);
    }

    [Test]
    public void AuthoredStatusRules_DrivePeriodicDamageCleanseAndDiminishing()
    {
        var statusRules = new CombatStatusRules(
            new Dictionary<string, CombatStatusFamilyRule>
            {
                ["stagger"] = new("stagger", StatusGroupValue.Control, true, true, true, 0.25f, false, false),
                ["scorch"] = new("scorch", StatusGroupValue.Attrition, false, false, false, 0f, true, false),
                ["unstoppable"] = CombatStatusRules.Default.StatusFamilies["unstoppable"],
            },
            new Dictionary<string, CombatCleanseProfileRule>
            {
                ["cleanse_scorch"] = new("cleanse_scorch", new[] { "scorch" }, false, false, 0f),
            },
            new CombatControlDiminishingRule("custom_dr", 0.25f, 2.25f, Array.Empty<string>(), Array.Empty<string>()));
        var targetBase = CombatTestFactory.CreateUnit("target", hp: 20f);
        var targetStats = new Dictionary<StatKey, float>(targetBase.BaseStats)
        {
            [StatKey.Tenacity] = 0.4f,
        };
        var targetLoadout = targetBase with { BaseStats = targetStats };
        var statusSkill = new BattleSkillSpec(
            "skill.status",
            "skill.status",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec("apply.stagger", "stagger", 2f, 0f),
                new StatusApplicationSpec("apply.scorch", "scorch", 3f, 2f),
            });
        var cleanseSkill = new BattleSkillSpec(
            "skill.cleanse",
            "skill.cleanse",
            SkillKind.Utility,
            0f,
            1f,
            CleanseProfileId: "cleanse_scorch");
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { statusSkill, cleanseSkill });
        var state = CombatTestFactory.CreateBattleState(new[] { actorLoadout }, new[] { targetLoadout }, statusRules: statusRules);
        var actor = state.Allies.Single();
        var target = state.Enemies.Single();
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, statusSkill, events);

        Assert.That(target.Statuses.Single(status => status.StatusId == "stagger").RemainingSeconds, Is.EqualTo(1.8f).Within(0.001f));
        StatusResolutionService.AdvanceStatuses(state, events);
        Assert.That(target.CurrentHealth, Is.EqualTo(18f).Within(0.001f));

        StatusResolutionService.ApplySkillStatuses(state, actor, target, cleanseSkill, events);

        Assert.That(target.HasStatus("scorch"), Is.False);
    }
}
