using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Content;

namespace SM.Tests.EditMode;

/// <summary>Move 3 barrier가 TacticalMark 적용 truth를 튕기는 상태 경제 회귀.</summary>
[Category("FastUnit")]
public sealed class BarrierTacticalMarkTests
{
    [TestCase("marked")]
    [TestCase("exposed")]
    public void Barrier_BouncesCanonicalTacticalMark_WithResistanceEvent(string statusId)
    {
        var (state, actor, target, skill) = CreateStatusScenario(statusId);
        target.AddBarrier(12f);
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);

        Assert.That(target.HasStatus(statusId), Is.False);
        Assert.That(target.Barrier, Is.EqualTo(12f).Within(0.001f), "표식 저항은 barrier를 소모하지 않는다");
        var resisted = events.Single(@event => @event.EventKind == BattleEventKind.StatusResisted);
        Assert.That(resisted.ActorId, Is.EqualTo(actor.Id));
        Assert.That(resisted.TargetId, Is.EqualTo(target.Id));
        Assert.That(resisted.PayloadId, Is.EqualTo(statusId));
        Assert.That(resisted.Value, Is.EqualTo(12f).Within(0.001f));
        Assert.That(events.Any(@event => @event.EventKind == BattleEventKind.StatusApplied), Is.False);
    }

    [TestCase("marked")]
    [TestCase("exposed")]
    public void ZeroBarrier_PreservesCanonicalTacticalMarkApplication(string statusId)
    {
        var (state, actor, target, skill) = CreateStatusScenario(statusId);
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);

        Assert.That(target.HasStatus(statusId), Is.True);
        Assert.That(events, Is.EqualTo(new[]
        {
            new BattleEvent(
                state.StepIndex,
                state.ElapsedSeconds,
                actor.Id,
                actor.Definition.Name,
                BattleActionType.ActiveSkill,
                BattleLogCode.Generic,
                target.Id,
                target.Definition.Name,
                0.2f,
                BattleEventKind.StatusApplied,
                statusId),
        }), "barrier=0인 기존 상태 적용 event record는 byte-identical shape를 유지해야 한다");
    }

    [Test]
    public void BarrierGuard_UsesAuthoredTacticalMarkGroup_NotCanonicalIds()
    {
        const string derivedMarkId = "brand";
        var rules = new CombatStatusRules(
            new Dictionary<string, CombatStatusFamilyRule>
            {
                [derivedMarkId] = new(
                    derivedMarkId,
                    StatusGroupValue.TacticalMark,
                    false,
                    false,
                    false,
                    0f,
                    false,
                    false,
                    AmplifiesIncomingDamage: true),
            },
            null,
            null);
        var (state, actor, target, skill) = CreateStatusScenario(derivedMarkId, rules);
        target.AddBarrier(5f);
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);

        Assert.That(target.HasStatus(derivedMarkId), Is.False);
        Assert.That(events.Single().EventKind, Is.EqualTo(BattleEventKind.StatusResisted));
        Assert.That(events.Single().PayloadId, Is.EqualTo(derivedMarkId));
    }

    private static (BattleState State, UnitSnapshot Actor, UnitSnapshot Target, BattleSkillSpec Skill)
        CreateStatusScenario(string statusId, CombatStatusRules? rules = null)
    {
        var skill = new BattleSkillSpec(
            $"skill.{statusId}",
            $"skill.{statusId}",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec($"apply.{statusId}", statusId, 2f, 0.2f) });
        var actorLoadout = CombatTestFactory.CreateUnit("actor", classId: "mystic", skills: new[] { skill });
        var targetLoadout = CombatTestFactory.CreateUnit("target", race: "undead");
        var state = CombatTestFactory.CreateBattleState(
            new[] { actorLoadout },
            new[] { targetLoadout },
            statusRules: rules);
        return (state, state.Allies[0], state.Enemies[0], skill);
    }
}
