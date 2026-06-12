using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 2 팀 블랙보드 계약 — FocusMark(점수표+히스테리시스), carry 결정, frontline breach 판정,
/// CollapseWeakSide 약측 안정(1.0s) 신호, 그리고 posture 앵커 시프트(ProtectCarry/CollapseWeakSide)가
/// 홈 앵커 기하에 실제로 반영되는지를 고정한다. 모든 판정은 battle truth의 순수 함수라 같은 셋업에서
/// 항상 같은 값이 나와야 한다.
/// </summary>
[Category("FastUnit")]
public sealed class TeamBlackboardTests
{
    private static void AdvanceToNextBlackboardRefresh(BattleState state)
    {
        for (var i = 0; i < TeamBlackboardService.CadenceSteps; i++)
        {
            state.AdvanceStep();
        }
    }

    [Test]
    public void Carry_IsHighestPowerRangedUnit_TieBrokenByStableId()
    {
        var vanguard = CombatTestFactory.CreateUnit("ally_van", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, attack: 30f);
        var ranger = CombatTestFactory.CreateUnit("ally_rng", classId: "ranger", anchor: DeploymentAnchorId.BackTop, attack: 9f, attackRange: 5f);
        var mystic = CombatTestFactory.CreateUnit("ally_mys", classId: "mystic", anchor: DeploymentAnchorId.BackBottom, attack: 5f, attackRange: 2.6f);
        var enemy = CombatTestFactory.CreateUnit("enemy_dummy", race: "undead", classId: "vanguard");
        var state = CombatTestFactory.CreateBattleState(new[] { vanguard, ranger, mystic }, new[] { enemy }, seed: 7);

        var blackboard = state.GetTeamBlackboard(TeamSide.Ally);

        // carry = 원거리/캐스터 중 최고 화력. melee vanguard는 화력이 더 높아도 후보가 아니다.
        Assert.That(blackboard.CarryId, Is.EqualTo(state.Allies[1].Id),
            "the highest-power ranged unit (ranger) must be the carry — melee power does not qualify");
    }

    [Test]
    public void FocusMark_ScoresBacklineSupport_HoldsUnderHysteresis_SwitchesWhenBeatenBy30()
    {
        // 적 mystic(M)·ranger(R)·vanguard(V). V의 위치만 움직여 M의 점수를 3단계로 바꾼다:
        //   1) V가 멀다 → M=고립 mystic(60) > R=고립 ranger(55) → 마크 M.
        //   2) V가 M에서 1.8m(보호도 고립도 아님) → M=35, R=55지만 55 < 35+30 → 히스테리시스로 마크 유지.
        //   3) V가 M에 밀착(1.0m) → M=-15, R=55 ≥ -15+30 → 마크가 R로 넘어간다.
        var ally = CombatTestFactory.CreateUnit("ally_solo", classId: "vanguard");
        var mysticEnemy = CombatTestFactory.CreateUnit("enemy_mys", race: "undead", classId: "mystic", anchor: DeploymentAnchorId.BackTop, attackRange: 2.6f);
        var rangerEnemy = CombatTestFactory.CreateUnit("enemy_rng", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackBottom, attackRange: 5f);
        var vanguardEnemy = CombatTestFactory.CreateUnit("enemy_van", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { mysticEnemy, rangerEnemy, vanguardEnemy }, seed: 7);

        var mystic = state.Enemies[0];
        var ranger = state.Enemies[1];
        var vanguard = state.Enemies[2];
        mystic.SetPosition(new CombatVector2(4f, 2f));
        ranger.SetPosition(new CombatVector2(4f, -2f));
        vanguard.SetPosition(new CombatVector2(0f, 0f)); // 둘 다에서 멀다(>2m) — 둘 다 고립

        var first = state.GetTeamBlackboard(TeamSide.Ally);
        Assert.That(first.FocusMarkId, Is.EqualTo(mystic.Id), "isolated enemy mystic outscores the ranger");

        vanguard.SetPosition(new CombatVector2(4f, 0.2f)); // M에서 1.8m — 보호(≤1.5)도 고립(>2.0)도 아님
        AdvanceToNextBlackboardRefresh(state);
        var second = state.GetTeamBlackboard(TeamSide.Ally);
        Assert.That(second.FocusMarkId, Is.EqualTo(mystic.Id),
            "the ranger leads by less than the +30 hysteresis — the mark must not flicker");

        vanguard.SetPosition(new CombatVector2(4f, 1f)); // M에 밀착(1.0m) — 보호 페널티 -50
        AdvanceToNextBlackboardRefresh(state);
        var third = state.GetTeamBlackboard(TeamSide.Ally);
        Assert.That(third.FocusMarkId, Is.EqualTo(ranger.Id),
            "once the mystic is bodyguarded its score collapses by 50 — the mark moves to the ranger");

        Assert.That(state.ActivityTelemetry.FocusMarkSwitchCount, Is.EqualTo(1),
            "exactly one switch happened (initial assignment is not a switch)");
    }

    [Test]
    public void FrontlineBreach_DetectsNonVanguardBehindLine_NearBackline()
    {
        var vanguard = CombatTestFactory.CreateUnit("ally_van", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter);
        var ranger = CombatTestFactory.CreateUnit("ally_rng", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, attackRange: 5f);
        var diver = CombatTestFactory.CreateUnit("enemy_diver", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter);
        var enemyVanguard = CombatTestFactory.CreateUnit("enemy_van", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop);
        var state = CombatTestFactory.CreateBattleState(new[] { vanguard, ranger }, new[] { diver, enemyVanguard }, seed: 7);

        var ownVanguard = state.Allies[0];
        var ownRanger = state.Allies[1];
        var enemyDiver = state.Enemies[0];
        var enemyTank = state.Enemies[1];
        ownVanguard.SetPosition(new CombatVector2(-2f, 0f));
        ownRanger.SetPosition(new CombatVector2(-5f, 0f));
        enemyDiver.SetPosition(new CombatVector2(-4f, 0.5f)); // 아군 vanguard 라인(-2) 뒤 + ranger 3m 내
        enemyTank.SetPosition(new CombatVector2(-4.5f, -0.5f)); // 더 깊지만 vanguard 클래스 — breach 아님

        var blackboard = state.GetTeamBlackboard(TeamSide.Ally);

        Assert.That(blackboard.IsFrontlineBreached, Is.True);
        Assert.That(blackboard.IsBreacher(enemyDiver.Id), Is.True,
            "a non-vanguard enemy behind our vanguard line and within 3m of the backline is a breacher");
        Assert.That(blackboard.IsBreacher(enemyTank.Id), Is.False,
            "an enemy vanguard pushing deep is an engagement, not a breach (master plan: non-vanguard only)");
        Assert.That(state.ActivityTelemetry.FrontlineBreachCount, Is.EqualTo(1));
    }

    [Test]
    public void WeakSideLane_BecomesStableAfterTwoRefreshes_AndShiftsCollapseHomeAnchor()
    {
        // 적이 위 레인에 몰려 있고 아래 레인이 비어 있다 → 약측 = 아래(-1). 첫 갱신에서는 raw 신호만,
        // 두 번째 갱신(1.0s)에서 stable로 승격되고 CollapseWeakSide 홈 앵커가 그쪽으로 크게 시프트한다.
        var duelist = CombatTestFactory.CreateUnit("ally_due", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter);
        var enemyTop1 = CombatTestFactory.CreateUnit("enemy_top1", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop);
        var enemyTop2 = CombatTestFactory.CreateUnit("enemy_top2", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackTop, attackRange: 5f);
        var state = CombatTestFactory.CreateBattleState(
            new[] { duelist },
            new[] { enemyTop1, enemyTop2 },
            allyPosture: TeamPostureType.CollapseWeakSide,
            seed: 7);

        state.Enemies[0].SetPosition(new CombatVector2(3f, 1.8f));
        state.Enemies[1].SetPosition(new CombatVector2(4.5f, 1.8f));

        var first = state.GetTeamBlackboard(TeamSide.Ally);
        Assert.That(first.WeakSideLane, Is.EqualTo(-1), "the empty bottom lane is the weak side");
        Assert.That(first.StableWeakSideLane, Is.EqualTo(0), "one observation is not yet stable");
        var homeBeforeStable = MovementResolver.ResolveHomePosition(state, state.Allies[0]);

        AdvanceToNextBlackboardRefresh(state);
        var second = state.GetTeamBlackboard(TeamSide.Ally);
        Assert.That(second.StableWeakSideLane, Is.EqualTo(-1), "two consecutive refreshes promote the weak side to stable");
        var homeAfterStable = MovementResolver.ResolveHomePosition(state, state.Allies[0]);

        Assert.That(homeAfterStable.Y, Is.LessThan(homeBeforeStable.Y - 0.5f),
            "a stable weak side shifts the CollapseWeakSide home anchor a lane-scale step toward it");
    }

    [Test]
    public void ProtectCarry_PullsMysticHomeWithinCarryRadius()
    {
        // carry(ranger)가 위 레인, mystic이 아래 레인 — ProtectCarry에서 mystic 홈은 carry 1.8m 내로 당겨진다.
        var vanguard = CombatTestFactory.CreateUnit("ally_van", classId: "vanguard", anchor: DeploymentAnchorId.FrontBottom);
        var carry = CombatTestFactory.CreateUnit("ally_rng", classId: "ranger", anchor: DeploymentAnchorId.BackTop, attack: 9f, attackRange: 5f);
        var mystic = CombatTestFactory.CreateUnit("ally_mys", classId: "mystic", anchor: DeploymentAnchorId.BackBottom, attack: 5f, attackRange: 2.6f);
        var enemy = CombatTestFactory.CreateUnit("enemy_dummy", race: "undead", classId: "vanguard");
        var protectState = CombatTestFactory.CreateBattleState(
            new[] { vanguard, carry, mystic },
            new[] { enemy },
            allyPosture: TeamPostureType.ProtectCarry,
            seed: 7);

        var blackboard = protectState.GetTeamBlackboard(TeamSide.Ally);
        Assert.That(blackboard.CarryId, Is.EqualTo(protectState.Allies[1].Id));

        var mysticHome = MovementResolver.ResolveHomePosition(protectState, protectState.Allies[2]);
        var carryAnchor = protectState.Allies[1].AnchorPosition;
        Assert.That(mysticHome.DistanceTo(carryAnchor), Is.LessThanOrEqualTo(1.9f),
            "ProtectCarry must place the mystic's home anchor within ~1.8m of the carry");

        // 같은 스쿼드를 StandardAdvance로 돌리면 mystic 홈은 carry에서 멀다 — posture가 기하를 만든다.
        var standardState = CombatTestFactory.CreateBattleState(
            new[] { vanguard, carry, mystic },
            new[] { enemy },
            allyPosture: TeamPostureType.StandardAdvance,
            seed: 7);
        var standardMysticHome = MovementResolver.ResolveHomePosition(standardState, standardState.Allies[2]);
        var standardCarryAnchor = standardState.Allies[1].AnchorPosition;
        Assert.That(standardMysticHome.DistanceTo(standardCarryAnchor), Is.GreaterThan(1.9f),
            "under StandardAdvance the same squad keeps its spread — the pull is ProtectCarry-specific");
    }

    [Test]
    public void HoldLine_PullsFrontlineHomeBehindStandardAdvance()
    {
        var vanguard = CombatTestFactory.CreateUnit("ally_van", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter);
        var enemy = CombatTestFactory.CreateUnit("enemy_dummy", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter);

        var holdState = CombatTestFactory.CreateBattleState(
            new[] { vanguard }, new[] { enemy }, allyPosture: TeamPostureType.HoldLine, seed: 7);
        var standardState = CombatTestFactory.CreateBattleState(
            new[] { vanguard }, new[] { enemy }, allyPosture: TeamPostureType.StandardAdvance, seed: 7);

        var holdHome = MovementResolver.ResolveHomePosition(holdState, holdState.Allies[0]);
        var standardHome = MovementResolver.ResolveHomePosition(standardState, standardState.Allies[0]);

        // HoldLine 전열 홈은 StandardAdvance보다 분명히 뒤(아군 진영 쪽) — 과잉전진 감소의 기하적 토대.
        Assert.That(holdHome.X, Is.LessThan(standardHome.X - 0.6f),
            "a HoldLine frontline home anchor must sit clearly behind the StandardAdvance one");
    }
}
