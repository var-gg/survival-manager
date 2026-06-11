using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// P3 스킬 강제이동 — 돌진/넉백/끌기의 결정 산술, unstoppable 저항, micro-knockback 대체,
/// 그리고 "넉백이 전열 스크린을 깨서 후열을 노출시킨다" 전술 사건 골든.
/// </summary>
[Category("FastUnit")]
public sealed class SkillDisplacementTests
{
    private static readonly BehaviorProfile PushableFrontline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0f,
        FormationLine: FormationLine.Frontline,
        FrontlineGuardRadius: 3f);

    private static readonly BehaviorProfile SteadyTarget = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Frontline);

    private static readonly BehaviorProfile CleanBackline = new(
        0.25f, 0.2f, 0f, 0f, 0.5f, 0.5f,
        DodgeChance: 0f, BlockChance: 0f, BlockMitigation: 0f, Stability: 0.5f,
        FormationLine: FormationLine.Backline);

    [Test]
    public void Knockback_PushesEnemyAway_ByAuthoredDistanceTimesStability()
    {
        var state = CreateDuelState(out var caster, out var target, targetBehavior: SteadyTarget);
        caster.SetPosition(new CombatVector2(-1f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));

        var moved = MovementResolver.TryApplySkillTargetDisplacement(state, caster, target, Skill(SkillDisplacementKind.EnemyAwayFromCaster, 1f));

        Assert.That(moved, Is.True);
        // 방향은 시전자→대상 축(+X), 거리 1.0 × stabilityFactor(1−0.5)=0.5 — roll 없는 결정 산술.
        Assert.That(target.Position.X, Is.EqualTo(1.5f).Within(0.02f));
        Assert.That(target.Position.Y, Is.EqualTo(0f).Within(0.02f));
    }

    [Test]
    public void Pull_DrawsEnemyTowardCaster_ButKeepsMinGap()
    {
        var state = CreateDuelState(out var caster, out var target, targetBehavior: SteadyTarget);
        caster.SetPosition(new CombatVector2(0f, 0f));
        target.SetPosition(new CombatVector2(3f, 0f));

        var moved = MovementResolver.TryApplySkillTargetDisplacement(state, caster, target, Skill(SkillDisplacementKind.EnemyTowardCaster, 10f));

        Assert.That(moved, Is.True);
        // 끌기는 겹침 방지 최소 간격(0.6m)을 남긴다 — 과저작 거리도 간격까지만.
        Assert.That(caster.Position.DistanceTo(target.Position), Is.EqualTo(0.6f).Within(0.02f));
    }

    [Test]
    public void Unstoppable_IgnoresSkillDisplacement()
    {
        var state = CreateDuelState(out var caster, out var target, targetBehavior: SteadyTarget);
        caster.SetPosition(new CombatVector2(-1f, 0f));
        target.SetPosition(new CombatVector2(1f, 0f));
        target.ApplyStatus(new StatusApplicationSpec("test:unstoppable", "unstoppable", 5f, 1f));

        var moved = MovementResolver.TryApplySkillTargetDisplacement(state, caster, target, Skill(SkillDisplacementKind.EnemyAwayFromCaster, 1f));

        Assert.That(moved, Is.False);
        Assert.That(target.Position.X, Is.EqualTo(1f).Within(0.001f), "unstoppable은 강제이동을 무시한다");
    }

    [Test]
    public void SelfDash_ClosesTowardTarget_AndStopsAtContactGap()
    {
        var state = CreateDuelState(out var caster, out var target, targetBehavior: SteadyTarget);
        caster.SetPosition(new CombatVector2(0f, 0f));
        target.SetPosition(new CombatVector2(4f, 0f));
        var gapBefore = MovementResolver.ComputeEdgeDistance(caster, target);

        var moved = MovementResolver.TryApplySkillSelfDash(state, caster, target, Skill(SkillDisplacementKind.SelfTowardTarget, 2f));

        Assert.That(moved, Is.True);
        Assert.That(caster.Position.X, Is.EqualTo(2f).Within(0.02f), "저작 거리만큼 전진(간격 여유 충분)");

        // 이미 접촉 간격 안이면 no-op.
        caster.SetPosition(new CombatVector2(3.8f, 0f));
        Assert.That(MovementResolver.TryApplySkillSelfDash(state, caster, target, Skill(SkillDisplacementKind.SelfTowardTarget, 2f)), Is.False);
        Assert.That(gapBefore, Is.GreaterThan(2f), "전제: 시작 간격이 저작 거리보다 크다");
    }

    [Test]
    public void Resolver_UsesAuthoredDisplacement_InsteadOfMicroKnockback()
    {
        var skill = Skill(SkillDisplacementKind.EnemyAwayFromCaster, 1f) with
        {
            CastWindupSeconds = 0.1f,
        };
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("caster", physPower: 6f, flexActive: skill) },
            new[] { CombatTestFactory.CreateLoopAUnit("victim", hp: 200f, behavior: SteadyTarget) },
            seed: 41);
        var caster = state.Allies.Single();
        var victim = state.Enemies.Single();
        caster.SetPosition(new CombatVector2(-1f, 0f));
        victim.SetPosition(new CombatVector2(0.5f, 0f));

        caster.BeginWindup(BattleActionType.ActiveSkill, victim.Id, skill.Id);
        CombatActionResolver.Resolve(state, caster);

        // micro-knockback(0.28×지터×각도)이 아니라 저작 강제이동(1.0×0.5, 축 정렬)이 정확히 적용된다.
        Assert.That(victim.Position.X, Is.EqualTo(1.0f).Within(0.02f));
        Assert.That(victim.Position.Y, Is.EqualTo(0f).Within(0.02f));
    }

    [Test]
    public void Golden_KnockbackBreaksScreen_AndExposesBackline()
    {
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("breaker", physPower: 6f) },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_screen", behavior: PushableFrontline),
                CombatTestFactory.CreateLoopAUnit("enemy_carry", classId: "ranger", behavior: CleanBackline),
            },
            seed: 47);
        var breaker = state.Allies.Single();
        var screen = state.Enemies.Single(unit => unit.Definition.Id == "enemy_screen");
        var carry = state.Enemies.Single(unit => unit.Definition.Id == "enemy_carry");
        breaker.SetPosition(new CombatVector2(-1f, 0f));
        carry.SetPosition(new CombatVector2(2f, 0f));
        screen.SetPosition(new CombatVector2(2.4f, 0f));

        Assert.That(BattleFormationConsequence.IsBacklineExposed(state, carry), Is.False, "전제: 스크린이 멀쩡하다");
        Assert.That(BattleFormationConsequence.ResolveScreenMitigation(state, breaker, carry), Is.GreaterThan(0f));

        // 넉백 한 방이 스크린을 가드 반경(3m) 밖으로 밀어낸다 — Stability 0 전열, 저작 거리 3m.
        var moved = MovementResolver.TryApplySkillTargetDisplacement(
            state, breaker, screen, Skill(SkillDisplacementKind.EnemyAwayFromCaster, 3f));

        Assert.That(moved, Is.True);
        Assert.That(screen.Position.DistanceTo(carry.Position), Is.GreaterThan(3f), "스크린이 가드 반경 밖으로 밀렸다");
        Assert.That(BattleFormationConsequence.IsBacklineExposed(state, carry), Is.True, "후열이 노출됐다 — 넉백이 만든 전술 사건");
        Assert.That(BattleFormationConsequence.ResolveScreenMitigation(state, breaker, carry), Is.Zero, "다음 타격부터 차단 경감이 사라진다");
    }

    private static BattleSkillSpec Skill(SkillDisplacementKind kind, float distance)
    {
        return new BattleSkillSpec(
            "skill_test_displacement",
            "Test Displacement",
            SkillKind.Strike,
            5f,
            8f,
            CanCrit: false,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            LockRule: ActionLockRule.SoftCommit,
            TargetRuleData: new TargetRule(),
            DisplacementKind: kind,
            DisplacementDistance: distance);
    }

    private static BattleState CreateDuelState(out UnitSnapshot caster, out UnitSnapshot target, BehaviorProfile targetBehavior)
    {
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("caster") },
            new[] { CombatTestFactory.CreateLoopAUnit("target", behavior: targetBehavior) },
            seed: 31);
        caster = state.Allies.Single();
        target = state.Enemies.Single();
        return state;
    }
}
