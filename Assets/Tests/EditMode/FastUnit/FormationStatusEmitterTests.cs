using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Move 2 formation -> status emit 통합 seam. 판정 결과가 실제 action event stream에 exposed를 남기고,
/// 그 StatusApplied가 같은 step의 CombatComboService 입력으로 소비되는지 고정한다.
/// </summary>
[Category("FastUnit")]
public sealed class FormationStatusEmitterTests
{
    private static readonly BehaviorProfile CleanBackline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Backline);

    private static readonly BehaviorProfile CleanFrontline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Frontline,
        FrontlineGuardRadius: 3f);

    [Test]
    public void ScreenedHit_EmitsExposedOnAttacker_AndHonorsScreenerIcd()
    {
        var state = CreateScreenState(includeFinisher: false);
        var screen = state.Allies.Single(unit => unit.Definition.Id == "screen");
        var carry = state.Allies.Single(unit => unit.Definition.Id == "carry");
        var attacker = state.Enemies.Single();

        var firstEvents = ResolveBasic(state, attacker, carry);

        var exposed = attacker.Statuses.Single(status => status.StatusId == "exposed");
        Assert.That(exposed.RemainingSeconds, Is.EqualTo(1.2f).Within(0.001f));
        Assert.That(exposed.Magnitude, Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(exposed.SourceActorId, Is.EqualTo(screen.Id.Value));
        Assert.That(exposed.SourceSkillId, Is.Empty, "formation 방출은 authored skill attribution을 만들지 않는다");
        AssertStatusApplied(firstEvents, screen, attacker);

        for (var step = 0; step < 24; step++)
        {
            state.AdvanceStep();
        }

        var secondEvents = ResolveBasic(state, attacker, carry);

        Assert.That(secondEvents.Count(IsExposedApplied), Is.Zero,
            "같은 스크리너의 2.5s ICD 안(2.4s 경과)에서는 StatusApplied를 재방출하지 않는다");
        Assert.That(attacker.Statuses.Single(status => status.StatusId == "exposed").Stacks, Is.EqualTo(1));
    }

    [Test]
    public void Phalanx_ScreenedRiposte_ExtendsExposedByPointSixSeconds()
    {
        var state = CreateScreenState(includeFinisher: false, phalanx: true);
        var carry = state.Allies.Single(unit => unit.Definition.Id == "carry");
        var attacker = state.Enemies.Single();

        ResolveBasic(state, attacker, carry);

        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.PhalanxRuleId), Is.True);
        Assert.That(attacker.Statuses.Single(status => status.StatusId == "exposed").RemainingSeconds,
            Is.EqualTo(1.8f).Within(0.001f),
            "phalanx는 기존 riposte exposed 1.2s에 정확히 +0.6s만 더한다");
    }

    [Test]
    public void RearHit_EmitsExposedOnVictim()
    {
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("attacker", physPower: 4f),
                CombatTestFactory.CreateLoopAUnit("bait"),
            },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("victim", hp: 200f, behavior: CleanFrontline),
            },
            seed: 17);
        var attacker = state.Allies.Single(unit => unit.Definition.Id == "attacker");
        var bait = state.Allies.Single(unit => unit.Definition.Id == "bait");
        var victim = state.Enemies.Single();
        victim.SetPosition(new CombatVector2(0f, 0f));
        bait.SetPosition(new CombatVector2(-2f, 0f));
        victim.SetCurrentTarget(bait.Id); // victim facing -X
        attacker.SetPosition(new CombatVector2(2f, 0f)); // attacker is behind (+X)

        var events = ResolveBasic(state, attacker, victim);

        var exposed = victim.Statuses.Single(status => status.StatusId == "exposed");
        Assert.That(exposed.RemainingSeconds, Is.EqualTo(0.8f).Within(0.001f));
        Assert.That(exposed.SourceActorId, Is.EqualTo(attacker.Id.Value));
        Assert.That(exposed.SourceSkillId, Is.Empty);
        AssertStatusApplied(events, attacker, victim);
    }

    [Test]
    public void RearHit_WhenVictimHasBarrier_BouncesExposed()
    {
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("attacker", physPower: 4f),
                CombatTestFactory.CreateLoopAUnit("bait"),
            },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("victim", hp: 200f, behavior: CleanFrontline),
            },
            seed: 29);
        var attacker = state.Allies.Single(unit => unit.Definition.Id == "attacker");
        var bait = state.Allies.Single(unit => unit.Definition.Id == "bait");
        var victim = state.Enemies.Single();
        victim.SetPosition(new CombatVector2(0f, 0f));
        bait.SetPosition(new CombatVector2(-2f, 0f));
        victim.SetCurrentTarget(bait.Id);
        attacker.SetPosition(new CombatVector2(2f, 0f));
        victim.AddBarrier(10f);

        var events = ResolveBasic(state, attacker, victim);

        Assert.That(victim.HasStatus("exposed"), Is.False,
            "Move 2 rear-flank exposed도 TacticalMark 적용 truth의 barrier 가드를 우회하면 안 된다");
        var resisted = events.Single(@event =>
            @event.EventKind == BattleEventKind.StatusResisted && @event.PayloadId == "exposed");
        Assert.That(resisted.ActorId, Is.EqualTo(attacker.Id));
        Assert.That(resisted.TargetId, Is.EqualTo(victim.Id));
        Assert.That(events.Count(IsExposedApplied), Is.Zero);
    }

    [Test]
    public void RiposteExposed_SameStepFollowup_OpensAndConsumesCombo()
    {
        var state = CreateScreenState(includeFinisher: true);
        var carry = state.Allies.Single(unit => unit.Definition.Id == "carry");
        var finisher = state.Allies.Single(unit => unit.Definition.Id == "finisher");
        var attacker = state.Enemies.Single();
        var stepEvents = new List<BattleEvent>();

        stepEvents.AddRange(ResolveBasic(state, attacker, carry));
        stepEvents.AddRange(ResolveBasic(state, finisher, attacker));
        var healthBeforePayoff = attacker.CurrentHealth;

        CombatComboService.ProcessStep(state, stepEvents);

        var payoff = stepEvents.Single(@event => @event.LogCode == BattleLogCode.ComboPayoffDamage);
        var beats = state.DrainStepBeats();
        Assert.That(payoff.Value, Is.GreaterThan(0f));
        Assert.That(attacker.CurrentHealth, Is.LessThan(healthBeforePayoff));
        Assert.That(beats.Select(beat => beat.Type),
            Is.EqualTo(new[] { CombatBeatType.ComboPrimerApplied, CombatBeatType.ComboConsumed }));
        Assert.That(beats[0].Tag, Is.EqualTo("exposed"));
        Assert.That(beats[1].ChainId, Is.EqualTo(beats[0].ChainId));
    }

    [Test]
    public void FrontalUnscreenedHit_DoesNotEmitStatus()
    {
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("attacker", physPower: 4f) },
            new[] { CombatTestFactory.CreateLoopAUnit("victim", hp: 200f, behavior: CleanFrontline) },
            seed: 19);
        var attacker = state.Allies.Single();
        var victim = state.Enemies.Single();
        attacker.SetPosition(new CombatVector2(-2f, 0f));
        victim.SetPosition(new CombatVector2(2f, 0f)); // enemy team forward is -X: frontal contact

        var events = ResolveBasic(state, attacker, victim);

        Assert.That(attacker.HasStatus("exposed"), Is.False);
        Assert.That(victim.HasStatus("exposed"), Is.False);
        Assert.That(events.Count(IsExposedApplied), Is.Zero);
    }

    private static BattleState CreateScreenState(bool includeFinisher, bool phalanx = false)
    {
        var allies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateLoopAUnit("screen", hp: 200f, behavior: CleanFrontline),
            CombatTestFactory.CreateLoopAUnit("carry", hp: 200f, behavior: CleanBackline),
        };
        if (includeFinisher)
        {
            allies.Add(CombatTestFactory.CreateLoopAUnit("finisher", hp: 200f, physPower: 4f, behavior: CleanFrontline));
        }

        if (phalanx)
        {
            allies.Add(CombatTestFactory.CreateLoopAUnit("phalanx_a", classId: "duelist", behavior: CleanFrontline));
            allies.Add(CombatTestFactory.CreateLoopAUnit("phalanx_b", classId: "mystic", behavior: CleanFrontline));
        }

        var state = BattleFactory.Create(
            allies,
            new[] { CombatTestFactory.CreateLoopAUnit("attacker", hp: 300f, physPower: 4f, behavior: CleanFrontline) },
            seed: 13);
        state.Allies.Single(unit => unit.Definition.Id == "carry").SetPosition(new CombatVector2(-4f, 0f));
        state.Allies.Single(unit => unit.Definition.Id == "screen").SetPosition(new CombatVector2(-3.2f, 0f));
        state.Enemies.Single().SetPosition(new CombatVector2(2f, 0f));
        if (includeFinisher)
        {
            state.Allies.Single(unit => unit.Definition.Id == "finisher").SetPosition(new CombatVector2(0f, 0f));
        }

        return state;
    }

    private static IReadOnlyList<BattleEvent> ResolveBasic(BattleState state, UnitSnapshot actor, UnitSnapshot target)
    {
        actor.BeginWindup(BattleActionType.BasicAttack, target.Id, null);
        return CombatActionResolver.Resolve(state, actor);
    }

    private static void AssertStatusApplied(
        IEnumerable<BattleEvent> events,
        UnitSnapshot expectedSource,
        UnitSnapshot expectedTarget)
    {
        var applied = events.Single(IsExposedApplied);
        Assert.That(applied.ActorId, Is.EqualTo(expectedSource.Id));
        Assert.That(applied.TargetId, Is.EqualTo(expectedTarget.Id));
        Assert.That(applied.Value, Is.EqualTo(0.12f).Within(0.0001f));
    }

    private static bool IsExposedApplied(BattleEvent @event)
        => @event.EventKind == BattleEventKind.StatusApplied && @event.PayloadId == "exposed";
}
