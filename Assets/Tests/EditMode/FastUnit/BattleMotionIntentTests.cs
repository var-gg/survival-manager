using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 2 (C1 motion-intent channel): the sim must emit typed <see cref="BattleMotionIntent"/>
/// records for the position transitions it already computes, without altering gameplay. Gameplay
/// invariance is guarded by the determinism + spatial suites; these tests assert the new channel's
/// content (kind/source/discreteness), deterministic ordering, and step-index stamping.
/// </summary>
[Category("FastUnit")]
public sealed class BattleMotionIntentTests
{
    [Test]
    public void ApplyKnockback_RecordsKnockbackMotion_WithSourceActorAndDisplacement()
    {
        var attacker = CombatTestFactory.CreateUnit("ally_striker", anchor: DeploymentAnchorId.FrontCenter, attack: 12f);
        var target = CombatTestFactory.CreateUnit("enemy_victim", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 80f);
        var state = CombatTestFactory.CreateBattleState(new[] { attacker }, new[] { target }, seed: 11);
        var attackerUnit = state.Allies[0];
        var targetUnit = state.Enemies[0];
        attackerUnit.SetPosition(new CombatVector2(0f, 0f));
        targetUnit.SetPosition(new CombatVector2(1f, 0f));
        state.ResetStepMotions();
        var before = targetUnit.Position;

        MovementResolver.ApplyKnockback(state, attackerUnit, targetUnit, isCritical: false);

        Assert.That(state.StepMotions, Has.Count.EqualTo(1));
        var motion = state.StepMotions[0];
        Assert.That(motion.Kind, Is.EqualTo(BattleMotionKind.Knockback));
        Assert.That(motion.ActorId, Is.EqualTo(targetUnit.Id));
        Assert.That(motion.SourceActorId, Is.EqualTo(attackerUnit.Id));
        Assert.That(motion.IsDiscrete, Is.True);
        Assert.That(motion.From.X, Is.EqualTo(before.X).Within(1e-4f));
        Assert.That(motion.From.Y, Is.EqualTo(before.Y).Within(1e-4f));
        // 과거 SOE 원인: CombatVector2 합성 ToString이 Normalized 속성 때문에 무한 재귀했고
        // 단정 실패 메시지 포맷 단계에서 터졌다(ToString 오버라이드로 해소, CombatVector2FormattingTests가 고정).
        // 좌표 자체는 float 오차가 있는 값이라 허용오차 성분 비교를 유지한다.
        Assert.That(motion.To.X, Is.EqualTo(targetUnit.Position.X).Within(1e-5f));
        Assert.That(motion.To.Y, Is.EqualTo(targetUnit.Position.Y).Within(1e-5f));
        Assert.That(motion.SequenceInStep, Is.EqualTo(0));
    }

    [Test]
    public void MoveForIntent_RecordsApproachMotion_WhenClosingDistance()
    {
        var actor = CombatTestFactory.CreateUnit("ally_runner", anchor: DeploymentAnchorId.FrontCenter, moveSpeed: 1.9f, attackRange: 1.1f);
        var target = CombatTestFactory.CreateUnit("enemy_far", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 60f);
        var state = CombatTestFactory.CreateBattleState(new[] { actor }, new[] { target }, seed: 3);
        var actorUnit = state.Allies[0];
        var targetUnit = state.Enemies[0];
        actorUnit.SetPosition(new CombatVector2(-1f, 0f));
        targetUnit.SetPosition(new CombatVector2(6f, 0f));
        state.ResetStepMotions();
        var before = actorUnit.Position;

        MovementResolver.MoveForIntent(state, actorUnit, new EvaluatedAction(
            BattleActionType.BasicAttack,
            targetUnit,
            null,
            new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, TargetSelectorType.LowestHpEnemy),
            new FloatRange(0.8f, 1.1f),
            CombatActionState.Approach,
            ReevaluationReason.None,
            null));

        Assert.That(actorUnit.Position.X, Is.GreaterThan(before.X), "actor should advance toward target");
        Assert.That(state.StepMotions, Has.Count.EqualTo(1));
        var motion = state.StepMotions[0];
        Assert.That(motion.Kind, Is.EqualTo(BattleMotionKind.Approach));
        Assert.That(motion.ActorId, Is.EqualTo(actorUnit.Id));
        Assert.That(motion.IsDiscrete, Is.False);
        Assert.That(motion.SourceActorId, Is.Null);
        Assert.That(motion.To.X, Is.EqualTo(actorUnit.Position.X).Within(1e-5f));
        Assert.That(motion.To.Y, Is.EqualTo(actorUnit.Position.Y).Within(1e-5f));
    }

    [TestCase(7)]
    [TestCase(42)]
    public void StepMotions_AreDeterministic_AndStampedWithContainingStepIndex(int seed)
    {
        var firstRun = CollectMotions(BuildMelee1v1(seed));
        var secondRun = CollectMotions(BuildMelee1v1(seed));

        Assert.That(firstRun, Is.EqualTo(secondRun), $"seed={seed}: motion stream diverged across runs.");
        Assert.That(firstRun.Count, Is.GreaterThan(0), $"seed={seed}: expected motions to be emitted during a melee close.");
    }

    private static List<string> CollectMotions(BattleSimulator simulator)
    {
        var lines = new List<string>();
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            if (step.Motions == null)
            {
                continue;
            }

            foreach (var motion in step.Motions)
            {
                Assert.That(motion.StepIndex, Is.EqualTo(step.StepIndex),
                    "motion.StepIndex must match the step it is reported in (re-stamp invariant).");
                lines.Add(string.Join(
                    ":",
                    motion.StepIndex,
                    motion.SequenceInStep,
                    motion.ActorId.Value,
                    motion.Kind,
                    F(motion.From),
                    F(motion.To),
                    motion.SourceActorId?.Value ?? "-",
                    motion.IsDiscrete));
            }
        }

        return lines;
    }

    private static BattleSimulator BuildMelee1v1(int seed)
    {
        var ally = CombatTestFactory.CreateUnit(
            "ally_blade",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 2.1f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_blade",
            race: "undead",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 1.9f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        return new BattleSimulator(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: seed), 200);
    }

    private static string F(CombatVector2 value)
    {
        return value.X.ToString("R", CultureInfo.InvariantCulture) + "," + value.Y.ToString("R", CultureInfo.InvariantCulture);
    }
}
