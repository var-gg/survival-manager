using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 7 reinforcement invariants (GPT Pro review): motion conservation (I7), stable ordering (I8),
/// assembly boundary (I14), and the mutable-aliasing leak-containment (#5). These harden the C1 channel
/// beyond the per-site contract tests of Stage 2.
/// </summary>
[Category("FastUnit")]
public sealed class BattleMotionInvariantTests
{
    private const float Tolerance = 1e-4f;

    // I7: every per-tick displacement of every actor is fully explained by a contiguous chain of
    // BattleMotionIntent records — first.From == previous truth, last.To == current truth.
    [TestCase(7)]
    [TestCase(23)]
    [TestCase(42)]
    public void MotionChain_ConservesDisplacement_EveryStep(int seed)
    {
        var simulator = BuildSkirmish(seed);
        var previous = simulator.CurrentStep;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            var previousPositions = previous.Units.ToDictionary(unit => unit.Id, unit => unit.Position, StringComparer.Ordinal);
            var chains = (step.Motions ?? Array.Empty<BattleMotionIntent>())
                .GroupBy(motion => motion.ActorId.Value, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(motion => motion.SequenceInStep).ToList(),
                    StringComparer.Ordinal);

            foreach (var unit in step.Units)
            {
                if (!previousPositions.TryGetValue(unit.Id, out var previousPosition))
                {
                    continue;
                }

                var moved = previousPosition.DistanceTo(unit.Position);
                if (moved <= Tolerance)
                {
                    continue;
                }

                Assert.That(chains.TryGetValue(unit.Id, out var chain), Is.True,
                    $"seed={seed} step={step.StepIndex} actor={unit.Id}: moved {moved:F4} with no motion intent.");
                Assert.That(chain![0].From.DistanceTo(previousPosition), Is.LessThan(Tolerance),
                    $"seed={seed} step={step.StepIndex} actor={unit.Id}: chain start != previous truth.");
                Assert.That(chain[^1].To.DistanceTo(unit.Position), Is.LessThan(Tolerance),
                    $"seed={seed} step={step.StepIndex} actor={unit.Id}: chain end != current truth.");
                for (var i = 0; i + 1 < chain.Count; i++)
                {
                    Assert.That(chain[i].To.DistanceTo(chain[i + 1].From), Is.LessThan(Tolerance),
                        $"seed={seed} step={step.StepIndex} actor={unit.Id}: chain not contiguous at {i}.");
                }
            }

            previous = step;
        }
    }

    // I8: SequenceInStep is contiguous from zero within each step (deterministic, dictionary-iteration free).
    [TestCase(7)]
    [TestCase(42)]
    public void MotionSequenceInStep_IsContiguousFromZero(int seed)
    {
        var simulator = BuildSkirmish(seed);
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            var step = simulator.Step();
            var sequences = (step.Motions ?? Array.Empty<BattleMotionIntent>()).Select(motion => motion.SequenceInStep).OrderBy(value => value).ToList();
            for (var i = 0; i < sequences.Count; i++)
            {
                Assert.That(sequences[i], Is.EqualTo(i),
                    $"seed={seed} step={step.StepIndex}: SequenceInStep not contiguous from 0.");
            }
        }
    }

    // I14: the deterministic sim assembly must stay free of the engine and the presentation layer,
    // including after C1 added the BattleMotionIntent public abstraction.
    [Test]
    public void SmCombatAssembly_StaysEngineAndPresentationFree()
    {
        var combat = typeof(BattleSimulator).Assembly;
        var forbidden = combat.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name != null
                && (name.StartsWith("UnityEngine", StringComparison.Ordinal)
                    || name.StartsWith("UnityEditor", StringComparison.Ordinal)
                    || string.Equals(name, "SM.Unity", StringComparison.Ordinal)))
            .ToList();

        Assert.That(forbidden, Is.Empty,
            $"SM.Combat must stay engine/presentation free; found refs: {string.Join(", ", forbidden)}");
        Assert.That(typeof(BattleMotionIntent).Assembly, Is.EqualTo(combat),
            "BattleMotionIntent must live in SM.Combat, not leak into a presentation assembly.");
    }

    // Leak path #5: a recorded step is immutable — its units/motions must not change as later steps run.
    [Test]
    public void RecordedStep_IsImmutable_AcrossLaterSteps()
    {
        var simulator = BuildSkirmish(7);
        BattleSimulationStep early = null!;
        for (var i = 0; i < 5 && !simulator.IsFinished; i++)
        {
            early = simulator.Step();
        }

        Assert.That(early, Is.Not.Null);
        var snapshot = SerializeStep(early);

        for (var i = 0; i < 25 && !simulator.IsFinished; i++)
        {
            simulator.Step();
        }

        Assert.That(SerializeStep(early), Is.EqualTo(snapshot),
            "a recorded BattleSimulationStep mutated after later steps advanced (aliasing leak).");
    }

    private static BattleSimulator BuildSkirmish(int seed)
    {
        var allies = new[]
        {
            CombatTestFactory.CreateUnit("ally_a", anchor: DeploymentAnchorId.FrontTop, hp: 50f, moveSpeed: 2.0f, attackRange: 1.1f, attackCooldown: 0.6f),
            CombatTestFactory.CreateUnit("ally_b", anchor: DeploymentAnchorId.FrontCenter, hp: 50f, moveSpeed: 2.0f, attackRange: 1.1f, attackCooldown: 0.6f),
            CombatTestFactory.CreateUnit("ally_c", anchor: DeploymentAnchorId.FrontBottom, hp: 50f, moveSpeed: 2.0f, attackRange: 1.1f, attackCooldown: 0.6f),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateUnit("enemy_a", race: "undead", anchor: DeploymentAnchorId.FrontTop, hp: 50f, moveSpeed: 1.9f, attackRange: 1.1f, attackCooldown: 0.6f),
            CombatTestFactory.CreateUnit("enemy_b", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 50f, moveSpeed: 1.9f, attackRange: 1.1f, attackCooldown: 0.6f),
            CombatTestFactory.CreateUnit("enemy_c", race: "undead", anchor: DeploymentAnchorId.FrontBottom, hp: 50f, moveSpeed: 1.9f, attackRange: 1.1f, attackCooldown: 0.6f),
        };
        return new BattleSimulator(CombatTestFactory.CreateBattleState(allies, enemies, seed: seed), 250);
    }

    private static string SerializeStep(BattleSimulationStep step)
    {
        var sb = new StringBuilder();
        foreach (var unit in step.Units)
        {
            sb.Append(unit.Id).Append('=')
              .Append(unit.Position.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
              .Append(unit.Position.Y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        sb.Append('|');
        foreach (var motion in step.Motions ?? Array.Empty<BattleMotionIntent>())
        {
            sb.Append(motion.SequenceInStep).Append(':')
              .Append(motion.ActorId.Value).Append(':')
              .Append((int)motion.Kind).Append(':')
              .Append(motion.To.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
              .Append(motion.To.Y.ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        return sb.ToString();
    }
}
