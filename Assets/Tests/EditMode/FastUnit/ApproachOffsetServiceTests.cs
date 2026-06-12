using System;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 2 결정적 접근 offset 계약 — engagement slot lease의 대체. 같은 타겟의 근접 공격자들이
/// (rolePriority, stableId) index의 고정 각도(정면/±45°/±80°)로 흩어지고, lease/게이트 없이 막히면
/// null(일반 추격 폴백), 의도(PositioningIntent)는 각도에서 유도된다.
/// </summary>
[Category("FastUnit")]
public sealed class ApproachOffsetServiceTests
{
    private static UnitSnapshot MakeUnit(
        string id,
        TeamSide side,
        string classId = "vanguard",
        float attackRange = 1.2f,
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(id, classId: classId, anchor: anchor, attackRange: attackRange);
        var unit = new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, anchor),
            BattleFactory.ResolveSpawnPosition(side, anchor));
        unit.SetActionState(CombatActionState.AcquireTarget);
        return unit;
    }

    private static BattleState MakeState(UnitSnapshot[] allies, UnitSnapshot[] enemies, int seed = 7)
    {
        return new BattleState(
            allies, enemies,
            TeamPostureType.StandardAdvance, TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds, seed);
    }

    [Test]
    public void IsMeleeEngagement_TrueForMelee_FalseForRanged()
    {
        var melee = MakeUnit("melee", TeamSide.Ally, attackRange: 1.2f);
        var ranged = MakeUnit("ranged", TeamSide.Ally, classId: "ranger", attackRange: 4.0f);

        Assert.That(ApproachOffsetService.IsMeleeEngagement(melee, new FloatRange(0.5f, 1.4f)), Is.True);
        Assert.That(ApproachOffsetService.IsMeleeEngagement(ranged, new FloatRange(3.0f, 5.0f)), Is.False);
    }

    [Test]
    public void LoneAttacker_GetsDirectFront_OnOwnSideOfTarget()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally);
        var target = MakeUnit("target", TeamSide.Enemy);
        attacker.SetPosition(new CombatVector2(-1f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));
        attacker.SetCurrentTarget(target.Id);
        var state = MakeState(new[] { attacker }, new[] { target });

        var point = ApproachOffsetService.TryResolveDesiredApproachPoint(state, attacker, target);

        Assert.That(point, Is.Not.Null);
        Assert.That(point!.Value.X, Is.LessThan(target.Position.X), "ally attacker stands on the ally side (direct front)");
        Assert.That(MathF.Abs(point.Value.Y - target.Position.Y), Is.LessThan(0.01f), "direct front has no lateral offset");

        var edge = point.Value.DistanceTo(target.Position) - attacker.NavigationRadius - target.NavigationRadius;
        Assert.That(edge, Is.InRange(0.10f, 0.20f), "radius = targetR + attackerR + 0.15 (contact gap)");
    }

    [Test]
    public void ThreeAttackers_FanOut_FrontAndBothFlanks_VanguardTakesFront()
    {
        var vanguard = MakeUnit("a_van", TeamSide.Ally, classId: "vanguard");
        var duelist = MakeUnit("b_duel", TeamSide.Ally, classId: "duelist");
        var duelist2 = MakeUnit("c_duel", TeamSide.Ally, classId: "duelist");
        var target = MakeUnit("target", TeamSide.Enemy);
        vanguard.SetPosition(new CombatVector2(-2f, 0.4f));
        duelist.SetPosition(new CombatVector2(-2f, 0f));
        duelist2.SetPosition(new CombatVector2(-2f, -0.4f));
        target.SetPosition(new CombatVector2(1f, 0f));
        var state = MakeState(new[] { vanguard, duelist, duelist2 }, new[] { target });
        foreach (var ally in state.Allies)
        {
            ally.SetCurrentTarget(target.Id);
        }

        var vanguardPoint = ApproachOffsetService.TryResolveDesiredApproachPoint(state, vanguard, target)!.Value;
        var duelistPoint = ApproachOffsetService.TryResolveDesiredApproachPoint(state, duelist, target)!.Value;
        var duelist2Point = ApproachOffsetService.TryResolveDesiredApproachPoint(state, duelist2, target)!.Value;

        // rolePriority: vanguard(0)가 정면, duelist 둘은 stableId 순으로 ±45° 양 측면 — 화면에서 포위로 읽힌다.
        // (팬은 팀 진영 축 기준이라 ally의 +45°는 월드 -Y로 미러된다 — 어느 쪽이든 두 측면이 반대편이면 계약 충족.)
        Assert.That(MathF.Abs(vanguardPoint.Y - target.Position.Y), Is.LessThan(0.01f), "vanguard takes direct front");
        Assert.That(MathF.Abs(duelistPoint.Y - target.Position.Y), Is.GreaterThan(0.3f), "first duelist is fanned to a flank");
        Assert.That(MathF.Abs(duelist2Point.Y - target.Position.Y), Is.GreaterThan(0.3f), "second duelist is fanned to a flank");
        Assert.That(MathF.Sign(duelistPoint.Y - target.Position.Y), Is.Not.EqualTo(MathF.Sign(duelist2Point.Y - target.Position.Y)),
            "the two duelists take opposite flanks");

        // 산개 보장: 세 점이 모두 서로 떨어져 있다.
        Assert.That(vanguardPoint.DistanceTo(duelistPoint), Is.GreaterThan(0.4f));
        Assert.That(duelistPoint.DistanceTo(duelist2Point), Is.GreaterThan(0.4f));
    }

    [Test]
    public void ObstructedOffset_ReturnsNull_NormalPursuitFallback()
    {
        var attacker = MakeUnit("attacker", TeamSide.Ally);
        var bystander = MakeUnit("bystander", TeamSide.Ally);
        var target = MakeUnit("target", TeamSide.Enemy);
        attacker.SetPosition(new CombatVector2(-2f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));
        attacker.SetCurrentTarget(target.Id);
        var state = MakeState(new[] { attacker, bystander }, new[] { target });

        var openPoint = ApproachOffsetService.TryResolveDesiredApproachPoint(state, attacker, target);
        Assert.That(openPoint, Is.Not.Null);

        // 정지점 위에 다른 유닛이 서 있으면 offset은 포기된다 — lease로 자리를 주장하지 않는다(마스터 플랜
        // "if obstructed: ignore offset and use normal pursuit").
        bystander.SetPosition(openPoint!.Value);
        var blocked = ApproachOffsetService.TryResolveDesiredApproachPoint(state, attacker, target);
        Assert.That(blocked, Is.Null, "an obstructed offset yields to normal pursuit instead of claiming a lease");
    }

    [Test]
    public void PositioningIntent_DerivedFromAngle_AndDiveReadsAsBacklineDive()
    {
        var vanguard = MakeUnit("a_van", TeamSide.Ally, classId: "vanguard");
        var duelist = MakeUnit("b_duel", TeamSide.Ally, classId: "duelist");
        var target = MakeUnit("target", TeamSide.Enemy);
        vanguard.SetPosition(new CombatVector2(-2f, 0.4f));
        duelist.SetPosition(new CombatVector2(-2f, -0.4f));
        target.SetPosition(new CombatVector2(1f, 0f));
        var state = MakeState(new[] { vanguard, duelist }, new[] { target });
        var band = new FloatRange(0.5f, 1.4f);
        vanguard.SetCurrentTarget(target.Id);
        duelist.SetCurrentTarget(target.Id);

        Assert.That(ApproachOffsetService.ResolvePositioningIntent(state, vanguard, target, band),
            Is.EqualTo(PositioningIntentKind.Frontline), "front offset reads as Frontline");
        var duelistIntent = ApproachOffsetService.ResolvePositioningIntent(state, duelist, target, band);
        Assert.That(duelistIntent, Is.EqualTo(PositioningIntentKind.FlankLeft).Or.EqualTo(PositioningIntentKind.FlankRight),
            "a flank offset reads as a flank intent");

        // Dive 의도가 실리면 각도와 무관하게 BacklineDive로 읽힌다(연출/텔레메트리 채널).
        duelist.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, target.Id, null, default, 50, 90));
        Assert.That(ApproachOffsetService.ResolvePositioningIntent(state, duelist, target, band),
            Is.EqualTo(PositioningIntentKind.BacklineDive));

        // 원거리는 산개 대상이 아니다.
        var ranger = MakeUnit("ranger", TeamSide.Ally, classId: "ranger", attackRange: 5f);
        Assert.That(ApproachOffsetService.ResolvePositioningIntent(state, ranger, target, new FloatRange(3f, 5f)),
            Is.EqualTo(PositioningIntentKind.MaintainRange));
    }

    [Test]
    public void AttackerIndex_IsStateless_RetargetingPeerReshufflesDeterministically()
    {
        var a = MakeUnit("a_van", TeamSide.Ally, classId: "vanguard");
        var b = MakeUnit("b_van", TeamSide.Ally, classId: "vanguard");
        var target = MakeUnit("target", TeamSide.Enemy);
        var other = MakeUnit("other", TeamSide.Enemy);
        a.SetPosition(new CombatVector2(-2f, 0.4f));
        b.SetPosition(new CombatVector2(-2f, -0.4f));
        target.SetPosition(new CombatVector2(1f, 0f));
        other.SetPosition(new CombatVector2(1.5f, -1f));
        var state = MakeState(new[] { a, b }, new[] { target, other });
        a.SetCurrentTarget(target.Id);
        b.SetCurrentTarget(target.Id);

        // 둘 다 같은 타겟 → stableId 순: a=정면, b=+45°.
        var bShared = ApproachOffsetService.TryResolveDesiredApproachPoint(state, b, target)!.Value;
        Assert.That(MathF.Abs(bShared.Y - target.Position.Y), Is.GreaterThan(0.3f), "second attacker is fanned out");

        // 동료가 다른 타겟으로 빠지면 집합이 줄어 b가 정면을 받는다 — 상태 없는 순수 재계산.
        a.SetCurrentTarget(other.Id);
        var bAlone = ApproachOffsetService.TryResolveDesiredApproachPoint(state, b, target)!.Value;
        Assert.That(MathF.Abs(bAlone.Y - target.Position.Y), Is.LessThan(0.01f), "with the peer gone, b takes direct front");
    }
}
