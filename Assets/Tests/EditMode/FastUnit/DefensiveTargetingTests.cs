using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>Move 3 guarded 회피 압력과 Dive/marked 상성 면제 회귀.</summary>
[Category("FastUnit")]
public sealed class DefensiveTargetingTests
{
    [Test]
    public void GuardedTarget_FallsBehindOtherwiseEquivalentPlainTarget()
    {
        var state = CreateRangedTargetingState(out var actor, out var guarded, out var plain);

        var beforeGuarded = SelectNearest(state, actor);
        guarded.ApplyStatus(new StatusApplicationSpec("test.guard", "guarded", 5f, 1f));
        var afterGuarded = SelectNearest(state, actor);

        Assert.That(beforeGuarded?.Id, Is.EqualTo(guarded.Id),
            "guarded/barrier가 없는 기존 경로에서는 실제 최근접 표적을 그대로 선택해야 한다");
        Assert.That(afterGuarded?.Id, Is.EqualTo(plain.Id),
            "guarded 채널의 +0.35m bias가 동조건의 비-guarded 표적으로 화력을 미끄러뜨려야 한다");
    }

    [Test]
    public void MarkedGuardedTarget_IsExemptFromGuardedPenalty()
    {
        var ranger = CombatTestFactory.CreateUnit(
            "ally_ranger",
            classId: "ranger",
            attackRange: 5.6f);
        var markedGuarded = CombatTestFactory.CreateUnit("enemy_a_marked_guarded", race: "undead");
        var plain = CombatTestFactory.CreateUnit("enemy_z_plain", race: "undead");
        var state = CombatTestFactory.CreateBattleState(new[] { ranger }, new[] { markedGuarded, plain }, seed: 17);
        var actor = state.Allies[0];
        var marked = state.Enemies[0];
        var unmarked = state.Enemies[1];
        actor.SetPosition(new CombatVector2(0f, 0f));
        marked.SetPosition(new CombatVector2(3.2f, 0f));
        unmarked.SetPosition(new CombatVector2(3f, 0f));
        marked.ApplyStatus(new StatusApplicationSpec("test.mark", "marked", 5f, 0.2f));
        marked.ApplyStatus(new StatusApplicationSpec("test.guard", "guarded", 5f, 1f));

        var selected = SelectNearest(state, actor);

        Assert.That(selected?.Id, Is.EqualTo(marked.Id),
            "marked의 지목 의도는 guarded 회피 페널티보다 우선해야 한다");
    }

    [Test]
    public void DiveIntent_IgnoresGuardedTargetPenalty()
    {
        var duelist = CombatTestFactory.CreateUnit(
            "ally_duelist",
            classId: "duelist",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 2.1f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.7f);
        var allyVanguard = CombatTestFactory.CreateUnit(
            "ally_vanguard",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontTop,
            hp: 120f);
        var enemyVanguard = CombatTestFactory.CreateUnit(
            "enemy_vanguard",
            race: "undead",
            classId: "vanguard",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 120f);
        var enemyRanger = CombatTestFactory.CreateUnit(
            "enemy_ranger",
            race: "undead",
            classId: "ranger",
            anchor: DeploymentAnchorId.BackCenter,
            hp: 40f,
            attackRange: 5f);
        var state = CombatTestFactory.CreateBattleState(
            new[] { duelist, allyVanguard },
            new[] { enemyVanguard, enemyRanger },
            allyPosture: TeamPostureType.AllInBackline,
            enemyPosture: TeamPostureType.StandardAdvance,
            seed: 23);
        var actor = state.Allies[0];
        var support = state.Allies[1];
        var nearFront = state.Enemies[0];
        var guardedBackline = state.Enemies[1];
        actor.SetPosition(new CombatVector2(0f, 0f));
        support.SetPosition(new CombatVector2(-0.8f, 0.6f));
        nearFront.SetPosition(new CombatVector2(2f, 0f));
        guardedBackline.SetPosition(new CombatVector2(4.5f, 0f));
        guardedBackline.ApplyStatus(new StatusApplicationSpec("test.guard", "guarded", 5f, 1f));
        foreach (var unit in state.AllUnits)
        {
            unit.SetActionState(CombatActionState.AcquireTarget);
        }

        new BattleSimulator(state, 60).Step();

        Assert.That(actor.CurrentCombatIntent.Type, Is.EqualTo(CombatIntentType.Dive));
        Assert.That(actor.CurrentTargetId, Is.EqualTo(guardedBackline.Id),
            "Dive intent override는 guarded 일반 bias를 우회해 후열 표적을 계속 노려야 한다");
    }

    private static BattleState CreateRangedTargetingState(
        out UnitSnapshot actor,
        out UnitSnapshot guardedCandidate,
        out UnitSnapshot plainCandidate)
    {
        var ranger = CombatTestFactory.CreateUnit("ally_ranger", classId: "ranger", attackRange: 5.6f);
        var guarded = CombatTestFactory.CreateUnit("enemy_a_guarded", race: "undead");
        var plain = CombatTestFactory.CreateUnit("enemy_z_plain", race: "undead");
        var focusDecoy = CombatTestFactory.CreateUnit("enemy_focus_decoy", race: "undead", hp: 50f);
        var state = CombatTestFactory.CreateBattleState(new[] { ranger }, new[] { guarded, plain, focusDecoy }, seed: 11);
        actor = state.Allies[0];
        guardedCandidate = state.Enemies[0];
        plainCandidate = state.Enemies[1];
        var decoy = state.Enemies[2];
        actor.SetPosition(new CombatVector2(0f, 0f));
        guardedCandidate.SetPosition(new CombatVector2(2.9f, 0f));
        plainCandidate.SetPosition(new CombatVector2(3f, 0f));
        decoy.SetPosition(new CombatVector2(9f, 0f));
        decoy.TakeDamage(45f); // 후보 밖 low-HP decoy가 FocusMark bias를 고정해 두 비교 후보를 격리한다.
        return state;
    }

    private static UnitSnapshot? SelectNearest(BattleState state, UnitSnapshot actor)
        => TargetScoringService.SelectTarget(
            state,
            actor,
            TargetSelectorType.NearestEnemy,
            BattleActionType.BasicAttack,
            null);
}
