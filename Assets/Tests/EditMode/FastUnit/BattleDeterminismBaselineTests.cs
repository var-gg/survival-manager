using System.Globalization;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 1 baseline (ADR anim-seam, GPT Pro review I1/I6): the deterministic combat sim must
/// produce a byte-identical <see cref="BattleSimulationStep"/> stream for the same seed across
/// independent runs. This pins gameplay truth BEFORE the C1 motion-intent channel lands, so any
/// later contract change can be proven not to alter sim results (the serializer here intentionally
/// excludes any future Motions channel, so the same string must hold across that change).
/// </summary>
[Category("FastUnit")]
public sealed class BattleDeterminismBaselineTests
{
    [TestCase(7)]
    [TestCase(23)]
    [TestCase(42)]
    public void StepStream_IsIdentical_AcrossTwoRuns_SameSeed_Melee1v1(int seed)
    {
        var first = SerializeRun(BuildMelee1v1(seed));
        var second = SerializeRun(BuildMelee1v1(seed));

        Assert.That(first.Stream, Is.EqualTo(second.Stream),
            $"seed={seed}: deterministic sim produced divergent step streams across two runs.");
        Assert.That(first.StepCount, Is.GreaterThan(0), $"seed={seed}: no steps produced.");
        Assert.That(first.Winner, Is.EqualTo(second.Winner), $"seed={seed}: winner diverged across runs.");
    }

    [TestCase(7)]
    [TestCase(23)]
    [TestCase(42)]
    public void StepStream_IsIdentical_AcrossTwoRuns_SameSeed_Skirmish2v2(int seed)
    {
        var first = SerializeRun(BuildSkirmish2v2(seed));
        var second = SerializeRun(BuildSkirmish2v2(seed));

        Assert.That(first.Stream, Is.EqualTo(second.Stream),
            $"seed={seed}: deterministic sim produced divergent step streams across two runs.");
        Assert.That(first.StepCount, Is.GreaterThan(0), $"seed={seed}: no steps produced.");
        Assert.That(first.Winner, Is.EqualTo(second.Winner), $"seed={seed}: winner diverged across runs.");
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

    private static BattleSimulator BuildSkirmish2v2(int seed)
    {
        var allies = new[]
        {
            CombatTestFactory.CreateUnit("ally_van", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 70f, attackRange: 1.2f),
            CombatTestFactory.CreateUnit("ally_duel", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 55f, attack: 6f, attackRange: 1.2f),
        };
        var enemies = new[]
        {
            CombatTestFactory.CreateUnit("enemy_van", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 70f, attackRange: 1.2f),
            CombatTestFactory.CreateUnit("enemy_duel", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 55f, attack: 6f, attackRange: 1.2f),
        };
        return new BattleSimulator(CombatTestFactory.CreateBattleState(allies, enemies, seed: seed), 250);
    }

    private static RunResult SerializeRun(BattleSimulator simulator)
    {
        var sb = new StringBuilder();
        AppendStep(sb, simulator.CurrentStep);
        var steps = 0;
        var guard = 0;
        while (!simulator.IsFinished && guard++ < 10000)
        {
            AppendStep(sb, simulator.Step());
            steps++;
        }

        return new RunResult(sb.ToString(), steps, simulator.Winner);
    }

    // Canonical gameplay-truth projection of one step. Deliberately omits any presentation-only or
    // future Motions data so the string stays stable across the C1 contract addition.
    private static void AppendStep(StringBuilder sb, BattleSimulationStep step)
    {
        sb.Append('S').Append(step.StepIndex)
          .Append('|').Append(step.IsFinished ? 'F' : '.')
          .Append(step.Winner?.ToString() ?? "-")
          .Append('|');
        foreach (var unit in step.Units)
        {
            sb.Append(unit.Id).Append(':')
              .Append(F(unit.Position.X)).Append(',')
              .Append(F(unit.Position.Y)).Append(',')
              .Append(F(unit.CurrentHealth)).Append(',')
              .Append(F(unit.CurrentEnergy)).Append(',')
              .Append((int)unit.ActionState).Append(',')
              .Append(unit.IsAlive ? '1' : '0').Append(';');
        }

        sb.Append('|');
        foreach (var battleEvent in step.Events)
        {
            sb.Append((int)battleEvent.LogCode).Append(':')
              .Append(battleEvent.ActorId.Value).Append(':')
              .Append(battleEvent.TargetId?.Value ?? "-").Append(':')
              .Append(F(battleEvent.Value)).Append(':')
              .Append((int)battleEvent.EventKind).Append(';');
        }

        sb.Append('\n');
    }

    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    private readonly struct RunResult
    {
        public RunResult(string stream, int stepCount, TeamSide? winner)
        {
            Stream = stream;
            StepCount = stepCount;
            Winner = winner;
        }

        public string Stream { get; }

        public int StepCount { get; }

        public TeamSide? Winner { get; }
    }
}
