using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// P1 플레이어 타겟 지시 — 규칙 변환 계약, LoopA 소비, "지시는 선호지 강제가 아니다" 가드,
/// 그리고 같은 스쿼드에서 지시만 바꿔도 교전 분포가 달라지는 분리 골든.
/// </summary>
[Category("FastUnit")]
public sealed class PlayerTargetDirectiveTests
{
    [Test]
    public void Apply_RewritesEnemyRule_AndPreservesAuthoredEnvelope()
    {
        var authored = new TargetRule { MaxAcquireRange = 7.5f, MinimumCommitSeconds = 1.25f, ClusterRadius = 0.8f };

        var finish = PlayerTargetDirectiveRules.Apply(PlayerTargetDirective.FinishLowestHp, authored);
        Assert.That(finish.PrimarySelector, Is.EqualTo(TargetSelector.LowestHpPercentEnemy));
        Assert.That(finish.FallbackPolicy, Is.EqualTo(TargetFallbackPolicy.NearestReachableEnemy));
        Assert.That(finish.MaxAcquireRange, Is.EqualTo(7.5f), "authored 사거리 envelope은 보존");
        Assert.That(finish.MinimumCommitSeconds, Is.EqualTo(1.25f), "anti-thrash 커밋 계약 보존");

        var cluster = PlayerTargetDirectiveRules.Apply(PlayerTargetDirective.BreakLargestCluster, authored);
        Assert.That(cluster.PrimarySelector, Is.EqualTo(TargetSelector.LargestEnemyCluster));
        Assert.That(cluster.ClusterRadius, Is.GreaterThanOrEqualTo(PlayerTargetDirectiveRules.MinClusterRadius));

        Assert.That(PlayerTargetDirectiveRules.Apply(PlayerTargetDirective.Default, authored), Is.SameAs(authored));
    }

    [Test]
    public void Apply_IgnoresAllyDomain_AndForcedRules()
    {
        var allyRule = new TargetRule { Domain = TargetDomain.AlliedUnit, PrimarySelector = TargetSelector.LowestHpPercentAlly };
        Assert.That(PlayerTargetDirectiveRules.Apply(PlayerTargetDirective.FinishLowestHp, allyRule), Is.SameAs(allyRule),
            "힐/지원(아군 도메인) 규칙은 지시로 오염되지 않는다");

        var marked = new TargetRule { PrimarySelector = TargetSelector.MarkedEnemy };
        Assert.That(PlayerTargetDirectiveRules.Apply(PlayerTargetDirective.NearestEnemy, marked), Is.SameAs(marked),
            "강제 의미(marked)는 지시보다 우선");
    }

    [Test]
    public void StableIds_RoundTrip_AndUnknownDegradesToDefault()
    {
        foreach (PlayerTargetDirective directive in System.Enum.GetValues(typeof(PlayerTargetDirective)))
        {
            Assert.That(
                PlayerTargetDirectiveRules.ParseStableId(PlayerTargetDirectiveRules.ToStableId(directive)),
                Is.EqualTo(directive));
        }

        Assert.That(PlayerTargetDirectiveRules.ParseStableId("no_such_directive"), Is.EqualTo(PlayerTargetDirective.Default));
        Assert.That(PlayerTargetDirectiveRules.ParseStableId(null), Is.EqualTo(PlayerTargetDirective.Default));
    }

    [Test]
    public void RangedUnit_FollowsFinishDirective_OverNearestDefault()
    {
        var defaultState = CreateRangedPickScenario(PlayerTargetDirective.Default, out var defaultShooter);
        var directedState = CreateRangedPickScenario(PlayerTargetDirective.FinishLowestHp, out var directedShooter);

        var defaultPick = TacticEvaluator.Evaluate(defaultState, defaultShooter);
        var directedPick = TacticEvaluator.Evaluate(directedState, directedShooter);

        Assert.That(defaultPick.Target?.Definition.Id, Is.EqualTo("enemy_near"), "지시 없음 — 최근접 기본");
        Assert.That(directedPick.Target?.Definition.Id, Is.EqualTo("enemy_weak"), "마무리 지시 — 빈사 적 우선");
    }

    [Test]
    public void MeleeUnit_DirectiveIsPreferenceNotForce_NearestGuardWins()
    {
        // melee가 지시 때문에 코앞의 적을 지나쳐 걸어가면 러닝머신(Q5)이 돌아온다 — 가드가 이긴다.
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("bruiser", classId: "duelist") with { TargetDirective = PlayerTargetDirective.FinishLowestHp } },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_adjacent", hp: 80f),
                CombatTestFactory.CreateLoopAUnit("enemy_weak_far", hp: 80f),
            },
            seed: 21);
        var bruiser = state.Allies.Single();
        var adjacent = state.Enemies.Single(unit => unit.Definition.Id == "enemy_adjacent");
        var weakFar = state.Enemies.Single(unit => unit.Definition.Id == "enemy_weak_far");
        bruiser.SetPosition(new CombatVector2(0f, 0f));
        adjacent.SetPosition(new CombatVector2(1.0f, 0f));
        weakFar.SetPosition(new CombatVector2(5f, 0f));
        weakFar.TakeDamage(70f);

        var pick = TacticEvaluator.Evaluate(state, bruiser);

        Assert.That(pick.Target?.Definition.Id, Is.EqualTo("enemy_adjacent"),
            "근접은 멀리 있는 빈사 적보다 코앞의 적을 문다(Q5 최근접 가드 유지)");
    }

    [Test]
    public void SeparationGolden_SameSquadDifferentDirective_FinishesDyingEnemySooner()
    {
        // Phase 2 이후 heat 합산은 지시를 분별하지 못한다 — default 런도 전투 후반에 빈사 적에게 커밋을
        // 쌓아 합계가 같아진다(측정: 지시는 첫 표적부터 갈리는데 heat는 15=15). "마무리 지시"의 정직한
        // 계약은 속도다: 같은 스쿼드·같은 시드에서 지시만 바꾸면 빈사 적이 분명히 더 빨리 죽는다.
        var defaultKillStep = RunSeparationScenario(PlayerTargetDirective.Default);
        var directedKillStep = RunSeparationScenario(PlayerTargetDirective.FinishLowestHp);

        Assert.That(directedKillStep, Is.LessThan(defaultKillStep - 5),
            $"마무리 지시는 빈사 적의 처치를 의미 있게 앞당겨야 한다 (default={defaultKillStep}, directed={directedKillStep})");
    }

    private static int RunSeparationScenario(PlayerTargetDirective directive)
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ally_van", anchor: DeploymentAnchorId.FrontCenter, hp: 90f, armor: 3f),
            CombatTestFactory.CreateLoopAUnit("ally_ranger", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, hp: 50f, attackRange: 7f)
                with { TargetDirective = directive },
        };
        // 약체는 전진하는 전열이어야 아군 레인저(AnchorFire — 제자리 사격)의 사거리에 실제로 들어온다.
        // 후열 약체는 9.9m 밖에서 평생 후보조차 안 되는 무풍 시나리오가 된다.
        var enemies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("enemy_tank", anchor: DeploymentAnchorId.FrontCenter, hp: 140f, armor: 4f),
            CombatTestFactory.CreateLoopAUnit("enemy_weak", anchor: DeploymentAnchorId.FrontTop, hp: 35f),
        };
        var state = BattleFactory.Create(allies, enemies, seed: 99);
        // 빈사 상태로 시작 — 마무리 지시가 물 표적.
        var weak = state.Enemies.Single(unit => unit.Definition.Id == "enemy_weak");
        weak.TakeDamage(20f);
        // Phase 2 FocusMark는 동점이면 빈사 쪽을 지목해 default 런까지 빈사 적으로 끌어당긴다. 팀 마크를
        // 탱크에 묶어 두면 계약이 더 강해진다: 팀 선호(FocusMark=탱크)를 플레이어 지시(FinishLowestHp)가
        // 이기고 빈사 적을 먼저 마무리해야 한다.
        state.Enemies.Single(unit => unit.Definition.Id == "enemy_tank")
            .ApplyStatus(new StatusApplicationSpec("status.marked", "marked", 60f, 0f));

        var sim = new BattleSimulator(state, 120);
        var step = 0;
        while (!sim.IsFinished && step < 120)
        {
            sim.Step();
            step++;
            if (!weak.IsAlive)
            {
                return step;
            }
        }

        return int.MaxValue;
    }

    private static BattleState CreateRangedPickScenario(PlayerTargetDirective directive, out UnitSnapshot shooter)
    {
        var state = BattleFactory.Create(
            new[] { CombatTestFactory.CreateLoopAUnit("shooter", classId: "ranger", attackRange: 8f) with { TargetDirective = directive } },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_near", hp: 80f),
                CombatTestFactory.CreateLoopAUnit("enemy_weak", hp: 80f),
            },
            seed: 17);
        shooter = state.Allies.Single();
        var near = state.Enemies.Single(unit => unit.Definition.Id == "enemy_near");
        var weak = state.Enemies.Single(unit => unit.Definition.Id == "enemy_weak");
        shooter.SetPosition(new CombatVector2(-2f, 0f));
        near.SetPosition(new CombatVector2(1f, 0f));
        weak.SetPosition(new CombatVector2(4f, 0f));
        weak.TakeDamage(80f * 0.7f); // 24/80 = 30% — 최저 체력 비율
        return state;
    }
}
