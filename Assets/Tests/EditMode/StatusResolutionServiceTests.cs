using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

public sealed class StatusResolutionServiceTests
{
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
}
