using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

/// <summary>
/// Q5 (GPT Pro): melee engagers pick the nearest reachable enemy rather than chasing a far focus target,
/// with focus-fire preserved only locally (within a small distance band of the nearest). Ranged units and
/// forced targeting keep strict focus-fire. This is the finisher for the residual focus-fire-pile-up
/// treadmill once the movement layer (A/B/C) is clean.
/// </summary>
[Category("FastUnit")]
public sealed class MeleeNearestEngagementOverrideTests
{
    private static UnitSnapshot MakeUnit(string id, TeamSide side, string classId = "vanguard", float attackRange = 1.2f, float hp = 50f)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(id, classId: classId, attackRange: attackRange, hp: hp);
        return new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, DeploymentAnchorId.FrontCenter),
            BattleFactory.ResolveSpawnPosition(side, DeploymentAnchorId.FrontCenter));
    }

    private static BattleState MakeState(UnitSnapshot[] allies, UnitSnapshot[] enemies)
    {
        return new BattleState(allies, enemies, TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance, BattleSimulator.DefaultFixedStepSeconds, 7);
    }

    private static void PrimeTarget(BattleState state, UnitSnapshot source, UnitSnapshot target, string statusId = "sunder")
    {
        state.ComboLedger.AddPrimer(new ComboPrimerWindow(
            state.ComboLedger.AllocateChainId(),
            source.Id,
            source.Side,
            target.Id,
            statusId,
            state.StepIndex,
            state.StepIndex + CombatComboService.PrimerWindowTicks));
    }

    [Test]
    public void Melee_DoesNotChaseFarLowHpFocus_PastANearerEnemy()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var farLowHp = MakeUnit("far_lowhp", TeamSide.Enemy, hp: 50f);
        var nearFullHp = MakeUnit("near_fullhp", TeamSide.Enemy, hp: 50f);
        farLowHp.TakeDamage(45f); // ratio 0.1 → focus-fire (lowest HP%) picks it, but it is far
        actor.SetPosition(new CombatVector2(0f, 0f));
        farLowHp.SetPosition(new CombatVector2(5f, 0f));
        nearFullHp.SetPosition(new CombatVector2(1.0f, 0f));

        var state = MakeState(new[] { actor }, new[] { farLowHp, nearFullHp });
        PrimeTarget(state, actor, farLowHp);
        var selected = TargetScoringService.SelectTarget(state, actor, TargetSelectorType.LowestHpEnemy, BattleActionType.BasicAttack, null);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected!.Id.Value, Is.EqualTo("near_fullhp"),
            "melee 최근접 가드는 활성 프라이머가 있는 먼 low-HP focus보다 상위다");
    }

    [Test]
    public void Ranged_PrefersFriendlyActivePrimer_WhenFocusMarkPinnedElsewhere()
    {
        // combo bias(0.40)는 focusMark(0.45)보다 의도적으로 약한 2차 신호다 — focusMark가 후보 중 하나를
        // 잡으면 그게 우선한다(설계 위계). combo bias를 고립 검증하려면 focusMark를 제3의 저HP 미끼로
        // 고정해(점수 lowHP40+isolated25 ≈ 65 vs 동일 위치라 상호 protected인 후보쌍 ≈ -50), 두 동조건
        // 후보에서 focusMark 교란을 제거한다. (미끼는 사거리 밖·원거리라 후보/선택엔 안 낀다.)
        var ranger = MakeUnit("ranger", TeamSide.Ally, classId: "ranger", attackRange: 5.6f);
        var focusDecoy = MakeUnit("enemy_m_decoy", TeamSide.Enemy);
        var plain = MakeUnit("enemy_a_plain", TeamSide.Enemy);
        var primed = MakeUnit("enemy_z_primed", TeamSide.Enemy);
        focusDecoy.TakeDamage(46f); // HealthRatio 0.08 (≤0.35) → focusMark 점수 최상
        ranger.SetPosition(new CombatVector2(0f, 0f));
        focusDecoy.SetPosition(new CombatVector2(9f, 0f));
        plain.SetPosition(new CombatVector2(3f, 0f));
        primed.SetPosition(new CombatVector2(3f, 0f));
        var state = MakeState(new[] { ranger }, new[] { focusDecoy, plain, primed });
        PrimeTarget(state, ranger, primed);

        var selected = TargetScoringService.SelectTarget(
            state,
            ranger,
            TargetSelectorType.NearestEnemy,
            BattleActionType.BasicAttack,
            null);

        Assert.That(selected?.Id, Is.EqualTo(primed.Id),
            "focusMark가 제3 미끼로 고정된 상태에서, 동조건 두 후보 중 아군 활성 프라이머 표적이 -0.40m bias로 먼저 선택된다");
    }

    [Test]
    public void Melee_KeepsLocalFocus_WhenWithinBandOfNearest()
    {
        var actor = MakeUnit("actor", TeamSide.Ally, attackRange: 1.2f);
        var focusLowHp = MakeUnit("focus_lowhp", TeamSide.Enemy, hp: 50f);
        var nearFullHp = MakeUnit("near_fullhp", TeamSide.Enemy, hp: 50f);
        focusLowHp.TakeDamage(45f); // lowest HP% → focus pick
        actor.SetPosition(new CombatVector2(0f, 0f));
        focusLowHp.SetPosition(new CombatVector2(1.3f, 0f));   // slightly farther...
        nearFullHp.SetPosition(new CombatVector2(1.1f, 0.4f)); // ...than this full-HP enemy, but within the focus band

        var state = MakeState(new[] { actor }, new[] { focusLowHp, nearFullHp });
        var selected = TargetScoringService.SelectTarget(state, actor, TargetSelectorType.LowestHpEnemy, BattleActionType.BasicAttack, null);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected!.Id.Value, Is.EqualTo("focus_lowhp"), "a low-HP focus that is near-equidistant to the nearest is finished off (local focus-fire)");
    }

    [Test]
    public void Ranged_KeepsFocus_NoNearestOverride()
    {
        var ranger = MakeUnit("ranger", TeamSide.Ally, classId: "ranger", attackRange: 5.6f);
        var farLowHp = MakeUnit("far_lowhp", TeamSide.Enemy, hp: 50f);
        var nearFullHp = MakeUnit("near_fullhp", TeamSide.Enemy, hp: 50f);
        farLowHp.TakeDamage(45f);
        ranger.SetPosition(new CombatVector2(0f, 0f));
        farLowHp.SetPosition(new CombatVector2(5f, 0f));
        nearFullHp.SetPosition(new CombatVector2(1.0f, 0f));

        var state = MakeState(new[] { ranger }, new[] { farLowHp, nearFullHp });
        var selected = TargetScoringService.SelectTarget(state, ranger, TargetSelectorType.LowestHpEnemy, BattleActionType.BasicAttack, null);

        Assert.That(selected, Is.Not.Null);
        Assert.That(selected!.Id.Value, Is.EqualTo("far_lowhp"), "ranged units keep strict focus-fire (no nearest override)");
    }
}
