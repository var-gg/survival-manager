using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 1 (GPT Pro J20 — canonical tick quantization). The integer <see cref="BattleCombatEventIntent.ContactTick"/>
/// fixed at BeginWindup must equal the step at which the float windup timer actually resolves. This is
/// what keeps Stage 1 gameplay byte-identical: the resolve trigger is unchanged (still the float timer),
/// we only record its tick. <see cref="BattleWindupTickMath.StepsUntilResolve"/> mirrors the sim's exact
/// <c>Max(0, remaining - dt)</c> decrement plus the <c>&gt; 0</c> gate, so off-by-one from float residue
/// cannot creep in.
/// </summary>
[Category("FastUnit")]
public sealed class BattleCombatEventIntentTickQuantizationTests
{
    [Test]
    public void StepsUntilResolve_IsAlwaysAtLeastOne_AndNonDecreasing()
    {
        var previous = 0;
        for (var windup = 0f; windup <= 1.2f; windup += 0.01f)
        {
            var steps = BattleWindupTickMath.StepsUntilResolve(windup, 0.1f);
            Assert.That(steps, Is.GreaterThanOrEqualTo(1), $"windup={windup}: earliest resolve is the next step.");
            Assert.That(steps, Is.GreaterThanOrEqualTo(previous), $"windup={windup}: step count must be non-decreasing in windup.");
            previous = steps;
        }
    }

    [Test]
    public void StepsUntilResolve_MirrorsTheExactSimDecrement()
    {
        // Tautological by construction, but it pins the algorithm: any future edit to the helper that
        // diverges from the sim's AdvanceTime + resolve-gate breaks this and the integration test below.
        for (var windup = 0f; windup <= 1.2f; windup += 0.01f)
        {
            Assert.That(
                BattleWindupTickMath.StepsUntilResolve(windup, 0.1f),
                Is.EqualTo(ReferenceStepsUntilResolve(windup, 0.1f)),
                $"windup={windup}");
        }
    }

    [TestCase(7, 0.1f)]
    [TestCase(7, 0.3f)]
    [TestCase(42, 0.25f)]
    [TestCase(23, 0.2f)]
    [TestCase(11, 0.45f)]
    public void PredictedContactTick_EqualsActualResolveStep(int seed, float windup)
    {
        var simulator = BuildMelee1v1(seed, windup);
        var contacted = new List<BattleCombatEventIntent>();
        var basicAttackDamageSteps = new List<int>();
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            foreach (var battleEvent in step.Events)
            {
                if (battleEvent.LogCode == BattleLogCode.BasicAttackDamage)
                {
                    basicAttackDamageSteps.Add(step.StepIndex);
                }
            }

            if (step.CombatEventIntents == null)
            {
                continue;
            }

            contacted.AddRange(step.CombatEventIntents.Where(intent => intent.Status == CombatEventIntentStatus.Contacted));
        }

        Assert.That(contacted, Is.Not.Empty, $"seed={seed} windup={windup}: expected resolved contacts.");

        foreach (var intent in contacted)
        {
            Assert.That(intent.StepIndex, Is.EqualTo(intent.ContactTick),
                $"id={intent.ActionInstanceId.Value}: the Contacted intent's reported step must equal its canonical ContactTick (predicted == actual resolve).");
        }

        // Every basic-attack damage event coincides with a predicted ContactTick — the byte-identical
        // proof that we recorded, not moved, the resolve.
        var contactTicks = new HashSet<int>(contacted.Select(intent => intent.ContactTick));
        foreach (var damageStep in basicAttackDamageSteps)
        {
            Assert.That(contactTicks.Contains(damageStep), Is.True,
                $"seed={seed} windup={windup}: BasicAttackDamage at step {damageStep} must coincide with a predicted ContactTick.");
        }
    }

    private static int ReferenceStepsUntilResolve(float windupSeconds, float dt)
    {
        var remaining = windupSeconds;
        var steps = 0;
        do
        {
            remaining = Math.Max(0f, remaining - dt);
            steps++;
        }
        while (remaining > 0f);

        return steps;
    }

    private static BattleSimulator BuildMelee1v1(int seed, float windup)
    {
        var ally = CombatTestFactory.CreateUnit(
            "ally_blade", anchor: DeploymentAnchorId.FrontCenter, hp: 60f,
            moveSpeed: 2.1f, attackRange: 1.2f, attackWindup: windup, attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy_blade", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 60f,
            moveSpeed: 1.9f, attackRange: 1.2f, attackWindup: windup, attackCooldown: 0.5f);
        return new BattleSimulator(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: seed), 200);
    }
}
