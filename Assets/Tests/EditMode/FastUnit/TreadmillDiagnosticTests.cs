using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

/// <summary>
/// DIAGNOSTIC (not a gate yet). Reproduces the dense-melee "treadmill" the user reports and MEASURES it
/// instead of inferring: it runs a deterministic crowded fight where units die mid-battle (corpses present
/// while the scrum continues), then attributes every unit of travel to its sim source via
/// <see cref="BattleSimulationStep.Motions"/> (Approach = slot-chase/collision-sidestep, Reposition =
/// separation-jitter/post-attack). The treadmill signal is: per-step net displacement above the locomotion
/// threshold (so the walk clip plays) while the multi-step net displacement is ~0 (the unit goes nowhere).
///
/// Output is written to Captures/treadmill-diag/report.txt for analysis. The asserts are intentionally loose
/// — this test's job is to produce numbers, which then drive the fix design. After the sim fix lands it will
/// be tightened into a real regression gate (treadmill incidence must drop).
/// </summary>
[Category("FastUnit")]
public sealed class TreadmillDiagnosticTests
{
    private const float StepSeconds = 0.1f;
    private const float LocomotionThreshold = 0.15f; // BattleLocomotionCadence.LocomotionSpeedThreshold
    private const float LocomotingStepDisplacement = LocomotionThreshold * StepSeconds; // > this per step => walk plays
    private const int Window = 10; // 1.0s
    private const float WindowMovedFloor = 0.20f; // must have "walked" at least this in the window to count
    private const float WindowEfficiencyCeil = 0.35f; // ... but netted < 35% of it => treadmilling

    // Default (selector = LowestHpEnemy) reproduces the original focus-fire scenario byte-identically (null
    // tactics → CombatTestFactory's default rule). Passing NearestEnemy is the Q5 confirmation variant:
    // melee engages the nearest enemy instead of all converging on one focus target.
    private static (List<BattleUnitLoadout> Allies, List<BattleUnitLoadout> Enemies) BuildDenseMelee(
        TargetSelectorType selector = TargetSelectorType.LowestHpEnemy)
    {
        var tactics = selector == TargetSelectorType.LowestHpEnemy ? null : MeleeTactics(selector);
        var allies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateUnit("ally_van_1", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 60f, attack: 4f, defense: 3f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f, tactics: tactics),
            CombatTestFactory.CreateUnit("ally_van_2", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 60f, attack: 4f, defense: 3f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f, tactics: tactics),
            CombatTestFactory.CreateUnit("ally_van_3", classId: "vanguard", anchor: DeploymentAnchorId.FrontBottom, hp: 60f, attack: 4f, defense: 3f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f, tactics: tactics),
            CombatTestFactory.CreateUnit("ally_due_1", classId: "duelist", anchor: DeploymentAnchorId.BackTop, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, tactics: tactics),
            CombatTestFactory.CreateUnit("ally_due_2", classId: "duelist", anchor: DeploymentAnchorId.BackCenter, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, tactics: tactics),
            CombatTestFactory.CreateUnit("ally_due_3", classId: "duelist", anchor: DeploymentAnchorId.BackBottom, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, tactics: tactics),
        };
        var enemies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateUnit("enemy_van_1", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontTop, hp: 60f, attack: 4f, defense: 3f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f, tactics: tactics),
            CombatTestFactory.CreateUnit("enemy_van_2", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontCenter, hp: 60f, attack: 4f, defense: 3f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f, tactics: tactics),
            CombatTestFactory.CreateUnit("enemy_van_3", race: "undead", classId: "vanguard", anchor: DeploymentAnchorId.FrontBottom, hp: 60f, attack: 4f, defense: 3f, moveSpeed: 1.9f, attackRange: 1.2f, attackCooldown: 0.9f, tactics: tactics),
            CombatTestFactory.CreateUnit("enemy_due_1", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.BackTop, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, tactics: tactics),
            CombatTestFactory.CreateUnit("enemy_due_2", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.BackCenter, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, tactics: tactics),
            CombatTestFactory.CreateUnit("enemy_due_3", race: "undead", classId: "duelist", anchor: DeploymentAnchorId.BackBottom, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, tactics: tactics),
        };
        return (allies, enemies);
    }

    private static TacticRule[] MeleeTactics(TargetSelectorType selector)
    {
        return new[]
        {
            new TacticRule(0, TacticConditionType.LowestHpEnemy, 0f, BattleActionType.BasicAttack, selector),
            new TacticRule(1, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self),
        };
    }

    /// <summary>
    /// Liveness gate (GPT Pro Q2/Q7-#3): the progress-gate settle (Stage C) must never introduce a new
    /// movement deadlock — every dense melee must still resolve to a winner within the step budget across a
    /// seed sweep. This is the conditional-convergence guarantee made concrete.
    /// </summary>
    [Test]
    public void DenseMelee_Converges_AcrossSeedSweep()
    {
        for (var seed = 0; seed < 48; seed++)
        {
            var (allies, enemies) = BuildDenseMelee();
            var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: seed);
            var simulator = new BattleSimulator(state, 600);
            while (!simulator.IsFinished)
            {
                simulator.Step();
            }

            Assert.That(simulator.IsFinished, Is.True, $"seed {seed} did not converge — possible movement deadlock from the progress gate");
        }
    }

    [Test]
    public void Measure_DenseMelee_TreadmillIncidence_AndAttribution()
    {
        var (allies, enemies) = BuildDenseMelee();
        var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: 7);
        var simulator = new BattleSimulator(state, 400);

        // Per-unit per-step net position (what presentation samples), and per-unit per-step per-kind path length.
        var positions = new Dictionary<string, List<(int Step, CombatVector2 Pos, bool Alive)>>();
        var kindPathByUnit = new Dictionary<string, Dictionary<BattleMotionKind, float>>();
        var totalNetByUnit = new Dictionary<string, float>(); // sum of |per-step net displacement|
        var firstDeathStep = int.MaxValue;
        var lastStep = 0;
        TeamSide? winner = null;

        while (!simulator.IsFinished)
        {
            var step = simulator.Step();
            lastStep = step.StepIndex;
            winner = step.Winner;

            foreach (var unit in step.Units)
            {
                if (!positions.TryGetValue(unit.Id, out var list))
                {
                    list = new List<(int, CombatVector2, bool)>();
                    positions[unit.Id] = list;
                }

                list.Add((step.StepIndex, unit.Position, unit.IsAlive));
                if (!unit.IsAlive && firstDeathStep == int.MaxValue)
                {
                    firstDeathStep = step.StepIndex;
                }
            }

            if (step.Motions != null)
            {
                foreach (var motion in step.Motions)
                {
                    var id = motion.ActorId.Value;
                    if (!kindPathByUnit.TryGetValue(id, out var byKind))
                    {
                        byKind = new Dictionary<BattleMotionKind, float>();
                        kindPathByUnit[id] = byKind;
                    }

                    var len = motion.From.DistanceTo(motion.To);
                    byKind.TryGetValue(motion.Kind, out var prev);
                    byKind[motion.Kind] = prev + len;
                }
            }
        }

        // ---- Aggregate metrics ------------------------------------------------------------------
        var totalAliveSteps = 0;
        var locomotingSteps = 0;
        var treadmillStepsAll = 0;
        var treadmillStepsBeforeDeath = 0;
        var treadmillStepsAfterDeath = 0;
        var aliveStepsBeforeDeath = 0;
        var aliveStepsAfterDeath = 0;
        var perUnitTreadmill = new Dictionary<string, int>();
        var perUnitLocomoting = new Dictionary<string, int>();

        foreach (var (id, samples) in positions)
        {
            float unitNet = 0f;
            for (var i = 1; i < samples.Count; i++)
            {
                if (!samples[i].Alive || !samples[i - 1].Alive)
                {
                    continue;
                }

                totalAliveSteps++;
                var stepNet = samples[i - 1].Pos.DistanceTo(samples[i].Pos);
                unitNet += stepNet;
                var isLoco = stepNet > LocomotingStepDisplacement;
                if (isLoco)
                {
                    locomotingSteps++;
                    perUnitLocomoting.TryGetValue(id, out var pl);
                    perUnitLocomoting[id] = pl + 1;
                }

                // Window treadmill check (needs i-Window alive too).
                if (i >= Window && samples[i - Window].Alive)
                {
                    float windowPath = 0f;
                    var windowOk = true;
                    for (var w = i - Window + 1; w <= i; w++)
                    {
                        if (!samples[w].Alive || !samples[w - 1].Alive)
                        {
                            windowOk = false;
                            break;
                        }

                        windowPath += samples[w - 1].Pos.DistanceTo(samples[w].Pos);
                    }

                    if (windowOk)
                    {
                        var windowNet = samples[i - Window].Pos.DistanceTo(samples[i].Pos);
                        var efficiency = windowPath > 1e-4f ? windowNet / windowPath : 1f;
                        var treadmilling = windowPath >= WindowMovedFloor && efficiency < WindowEfficiencyCeil;
                        if (treadmilling)
                        {
                            treadmillStepsAll++;
                            perUnitTreadmill.TryGetValue(id, out var pt);
                            perUnitTreadmill[id] = pt + 1;
                        }

                        if (samples[i].Step < firstDeathStep)
                        {
                            aliveStepsBeforeDeath++;
                            if (treadmilling) treadmillStepsBeforeDeath++;
                        }
                        else
                        {
                            aliveStepsAfterDeath++;
                            if (treadmilling) treadmillStepsAfterDeath++;
                        }
                    }
                }
            }

            totalNetByUnit[id] = unitNet;
        }

        // Team path-by-kind totals.
        var kindTotals = new Dictionary<BattleMotionKind, float>();
        foreach (var byKind in kindPathByUnit.Values)
        {
            foreach (var (kind, len) in byKind)
            {
                kindTotals.TryGetValue(kind, out var prev);
                kindTotals[kind] = prev + len;
            }
        }

        var totalPathAllKinds = kindTotals.Values.Sum();
        var totalNetAllUnits = totalNetByUnit.Values.Sum();

        // ---- Report -----------------------------------------------------------------------------
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("=== TREADMILL DIAGNOSTIC (dense 6v6 melee, seed 7) ===");
        sb.AppendLine($"battle length: {lastStep} steps ({(lastStep * StepSeconds).ToString("F1", ci)}s), finished={simulator.IsFinished}, winner={winner}");
        sb.AppendLine($"first death at step: {(firstDeathStep == int.MaxValue ? "(none)" : firstDeathStep.ToString())}");
        sb.AppendLine();
        sb.AppendLine("-- locomotion / treadmill incidence --");
        sb.AppendLine($"alive unit-steps total: {totalAliveSteps}");
        sb.AppendLine($"locomoting steps (per-step net > {LocomotingStepDisplacement.ToString("F3", ci)} => walk plays): {locomotingSteps} ({Pct(locomotingSteps, totalAliveSteps)})");
        sb.AppendLine($"TREADMILL windows (walked >= {WindowMovedFloor.ToString("F2", ci)} in 1s but netted < {WindowEfficiencyCeil:P0} of it): {treadmillStepsAll} ({Pct(treadmillStepsAll, totalAliveSteps)})");
        sb.AppendLine($"  before any death: {treadmillStepsBeforeDeath} / {aliveStepsBeforeDeath} ({Pct(treadmillStepsBeforeDeath, aliveStepsBeforeDeath)})");
        sb.AppendLine($"  after first death (corpses present): {treadmillStepsAfterDeath} / {aliveStepsAfterDeath} ({Pct(treadmillStepsAfterDeath, aliveStepsAfterDeath)})");
        sb.AppendLine();
        sb.AppendLine("-- travel attribution by sim motion kind (total path length) --");
        sb.AppendLine($"total path (all kinds): {totalPathAllKinds.ToString("F2", ci)}");
        sb.AppendLine($"total NET displacement (sum per-step net): {totalNetAllUnits.ToString("F2", ci)}");
        sb.AppendLine($"team travel efficiency (net/path): {(totalPathAllKinds > 1e-4f ? (totalNetAllUnits / totalPathAllKinds) : 1f).ToString("P1", ci)}");
        foreach (var kind in kindTotals.Keys.OrderByDescending(k => kindTotals[k]))
        {
            sb.AppendLine($"  {kind,-18}: {kindTotals[kind].ToString("F2", ci)} ({Pct((int)(kindTotals[kind] * 100), (int)(totalPathAllKinds * 100))})");
        }

        sb.AppendLine();
        sb.AppendLine("-- worst offenders (top treadmill windows) --");
        foreach (var (id, count) in perUnitTreadmill.OrderByDescending(kv => kv.Value).Take(8))
        {
            perUnitLocomoting.TryGetValue(id, out var loco);
            var kinds = kindPathByUnit.TryGetValue(id, out var bk)
                ? string.Join(", ", bk.OrderByDescending(x => x.Value).Select(x => $"{x.Key}={x.Value.ToString("F2", ci)}"))
                : "(none)";
            sb.AppendLine($"  {id,-14}: treadmill={count}, locomoting={loco}, net={totalNetByUnit.GetValueOrDefault(id).ToString("F2", ci)}, pathByKind=[{kinds}]");
        }

        var report = sb.ToString();
        TestContext.WriteLine(report);

        var dir = Path.Combine("Captures", "treadmill-diag");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "report.txt"), report);

        // Loose sanity asserts only — this is a measurement, not a gate (yet).
        Assert.That(totalAliveSteps, Is.GreaterThan(0));
        Assert.That(simulator.IsFinished, Is.True, "battle should resolve within the step budget");
    }

    private static string Pct(int n, int d) => d <= 0 ? "n/a" : ((float)n / d).ToString("P1", CultureInfo.InvariantCulture);

    // ── Robust multi-seed headline ──
    // Each fix stage changes the deterministic sim, so a single seed's battle evolves differently per stage
    // and its treadmill window % is noisy. Averaging over a seed sweep gives a stable headline to compare
    // stages and to anchor the eventual regression gate.

    private readonly record struct HeadlineStats(
        int AliveSteps, int LocomotingSteps, int TreadmillWindows,
        int AliveBeforeDeath, int TreadmillBeforeDeath,
        int AliveAfterDeath, int TreadmillAfterDeath,
        float TotalPath, float TotalNet,
        int MicroRunSteps, int StationarySteps);

    private const float MicroRunCeil = 0.05f; // locomoting but small per-step move = "running a tiny distance"

    private static HeadlineStats MeasureHeadline(int seed, TargetSelectorType selector = TargetSelectorType.LowestHpEnemy)
    {
        var (allies, enemies) = BuildDenseMelee(selector);
        var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: seed);
        var sim = new BattleSimulator(state, 600);
        var positions = new Dictionary<string, List<(int Step, CombatVector2 Pos, bool Alive)>>();
        var kindPath = new Dictionary<BattleMotionKind, float>();
        var firstDeath = int.MaxValue;

        while (!sim.IsFinished)
        {
            var step = sim.Step();
            foreach (var u in step.Units)
            {
                if (!positions.TryGetValue(u.Id, out var l))
                {
                    l = new List<(int, CombatVector2, bool)>();
                    positions[u.Id] = l;
                }

                l.Add((step.StepIndex, u.Position, u.IsAlive));
                if (!u.IsAlive && firstDeath == int.MaxValue)
                {
                    firstDeath = step.StepIndex;
                }
            }

            if (step.Motions != null)
            {
                foreach (var m in step.Motions)
                {
                    kindPath.TryGetValue(m.Kind, out var p);
                    kindPath[m.Kind] = p + m.From.DistanceTo(m.To);
                }
            }
        }

        int alive = 0, loco = 0, tread = 0, aliveBD = 0, treadBD = 0, aliveAD = 0, treadAD = 0, microRun = 0, stationary = 0;
        var net = 0f;
        foreach (var samples in positions.Values)
        {
            for (var i = 1; i < samples.Count; i++)
            {
                if (!samples[i].Alive || !samples[i - 1].Alive)
                {
                    continue;
                }

                alive++;
                var stepNet = samples[i - 1].Pos.DistanceTo(samples[i].Pos);
                net += stepNet;
                if (stepNet > LocomotingStepDisplacement)
                {
                    loco++;
                    if (stepNet <= MicroRunCeil)
                    {
                        microRun++; // locomoting but tiny → the "running a short distance" look
                    }
                }
                else
                {
                    stationary++; // below locomotion threshold → idle/standing
                }

                if (i >= Window && samples[i - Window].Alive)
                {
                    float wp = 0f;
                    var ok = true;
                    for (var w = i - Window + 1; w <= i; w++)
                    {
                        if (!samples[w].Alive || !samples[w - 1].Alive)
                        {
                            ok = false;
                            break;
                        }

                        wp += samples[w - 1].Pos.DistanceTo(samples[w].Pos);
                    }

                    if (ok)
                    {
                        var wn = samples[i - Window].Pos.DistanceTo(samples[i].Pos);
                        var eff = wp > 1e-4f ? wn / wp : 1f;
                        var tm = wp >= WindowMovedFloor && eff < WindowEfficiencyCeil;
                        if (tm)
                        {
                            tread++;
                        }

                        if (samples[i].Step < firstDeath)
                        {
                            aliveBD++;
                            if (tm) treadBD++;
                        }
                        else
                        {
                            aliveAD++;
                            if (tm) treadAD++;
                        }
                    }
                }
            }
        }

        return new HeadlineStats(alive, loco, tread, aliveBD, treadBD, aliveAD, treadAD, kindPath.Values.Sum(), net, microRun, stationary);
    }

    [Test]
    public void Measure_DenseMelee_SeedSweep_RobustHeadline()
    {
        const int seeds = 24;
        int alive = 0, loco = 0, tread = 0, aliveBD = 0, treadBD = 0, aliveAD = 0, treadAD = 0, microRun = 0, stationary = 0;
        float path = 0f, net = 0f;
        for (var s = 0; s < seeds; s++)
        {
            var h = MeasureHeadline(s);
            alive += h.AliveSteps;
            loco += h.LocomotingSteps;
            tread += h.TreadmillWindows;
            aliveBD += h.AliveBeforeDeath;
            treadBD += h.TreadmillBeforeDeath;
            aliveAD += h.AliveAfterDeath;
            treadAD += h.TreadmillAfterDeath;
            path += h.TotalPath;
            net += h.TotalNet;
            microRun += h.MicroRunSteps;
            stationary += h.StationarySteps;
        }

        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"=== TREADMILL ROBUST HEADLINE (dense 6v6, seeds 0..{seeds - 1}) ===");
        sb.AppendLine($"locomoting (move plays):   {Pct(loco, alive)}");
        sb.AppendLine($"stationary (idle/holds):   {Pct(stationary, alive)}");
        sb.AppendLine($"micro-run (tiny move clip): {Pct(microRun, alive)}");
        sb.AppendLine($"TREADMILL windows:         {Pct(tread, alive)}");
        sb.AppendLine($"  before any death:        {Pct(treadBD, aliveBD)}");
        sb.AppendLine($"  after first death:       {Pct(treadAD, aliveAD)}");
        sb.AppendLine($"team travel efficiency (net/path): {(path > 1e-4f ? net / path : 1f).ToString("P1", ci)}");
        var report = sb.ToString();
        TestContext.WriteLine(report);

        var dir = Path.Combine("Captures", "treadmill-diag");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "headline.txt"), report);

        Assert.That(alive, Is.GreaterThan(0));
        // Regression gate for the movement treadmill rewrite (A+B+C+Q5). Baselines: efficiency 53.7% -> 83.7%,
        // treadmill windows 22.2% -> 18.4%, post-death 29.1% -> 20.7%. Thresholds keep margin against seed noise
        // so a future change that re-introduces the jitter/pile-up trips the gate.
        var efficiency = path > 1e-4f ? net / path : 1f;
        Assert.That(efficiency, Is.GreaterThan(0.78f), "team travel efficiency regressed — separation jitter / slot thrash likely back");
        Assert.That((float)tread / alive, Is.LessThan(0.21f), "treadmill window incidence regressed");
        Assert.That((float)treadAD / aliveAD, Is.LessThan(0.25f), "post-death (corpse-phase) treadmill regressed");
    }

    private static (float Treadmill, float PostDeath, float Loco, float Efficiency) SweepTreadmill(TargetSelectorType selector, int seeds)
    {
        int alive = 0, loco = 0, tread = 0, aliveAD = 0, treadAD = 0;
        float path = 0f, net = 0f;
        for (var s = 0; s < seeds; s++)
        {
            var h = MeasureHeadline(s, selector);
            alive += h.AliveSteps;
            loco += h.LocomotingSteps;
            tread += h.TreadmillWindows;
            aliveAD += h.AliveAfterDeath;
            treadAD += h.TreadmillAfterDeath;
            path += h.TotalPath;
            net += h.TotalNet;
        }

        return (
            alive > 0 ? (float)tread / alive : 0f,
            aliveAD > 0 ? (float)treadAD / aliveAD : 0f,
            alive > 0 ? (float)loco / alive : 0f,
            path > 1e-4f ? net / path : 1f);
    }

    /// <summary>
    /// Q5 CONFIRMATION (GPT Pro). After A+B+C the residual treadmill is Approach-dominated and concentrated in
    /// focus-firing duelists. If the driver is really the focus-fire target policy (all units converging on one
    /// far target through the crowd), then merely switching melee targeting to nearest-enemy — with NO other
    /// change — should collapse the treadmill. This isolates the variable and proves Q5 is the finisher before
    /// committing to the (balance-affecting) targeting change.
    /// </summary>
    [Test]
    public void Confirm_NearestTargeting_CollapsesTreadmill_VsFocusFire()
    {
        const int seeds = 24;
        var focus = SweepTreadmill(TargetSelectorType.LowestHpEnemy, seeds);
        var nearest = SweepTreadmill(TargetSelectorType.NearestEnemy, seeds);

        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"=== Q5 CONFIRMATION: focus-fire vs nearest-engage (dense 6v6, seeds 0..{seeds - 1}, A+B+C movement) ===");
        sb.AppendLine($"focus-fire (LowestHpEnemy):    treadmill={focus.Treadmill.ToString("P1", ci)}  postDeath={focus.PostDeath.ToString("P1", ci)}  loco={focus.Loco.ToString("P1", ci)}  eff={focus.Efficiency.ToString("P1", ci)}");
        sb.AppendLine($"nearest-engage (NearestEnemy): treadmill={nearest.Treadmill.ToString("P1", ci)}  postDeath={nearest.PostDeath.ToString("P1", ci)}  loco={nearest.Loco.ToString("P1", ci)}  eff={nearest.Efficiency.ToString("P1", ci)}");
        sb.AppendLine($"treadmill delta: {((nearest.Treadmill - focus.Treadmill) * 100f).ToString("F1", ci)} pts");
        var report = sb.ToString();
        TestContext.WriteLine(report);

        var dir = Path.Combine("Captures", "treadmill-diag");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "q5-confirmation.txt"), report);

        Assert.That(focus.Treadmill, Is.GreaterThan(0f));
        Assert.That(nearest.Treadmill, Is.GreaterThan(0f));
    }

    // ── Endgame melee-chases-ranged treadmill (user-reported) ──
    // The dense-melee headline above is ALL melee, so it NEVER exercised the kite the user actually sees at
    // battle's end ("근거리가 원거리를 못 따라잡고 제자리 달리기"). A ranger back-pedals (BreakContact) every tick while a
    // committed melee chaser sits inside PreferredRangeMin-RetreatBuffer (~4.5m), each moving MoveSpeed*step.
    // At speed PARITY the chaser's net closing is ~0 — it runs in place (walk clip plays, edge distance flat)
    // until it dies to the kite. This isolates the chase, proves the parity treadmill, and quantifies how much
    // faster a committed chaser must be for the chase to RESOLVE. It's the coverage hole behind "metrics good,
    // feel still wrong".
    private readonly record struct ChaseStats(
        float RangerSpeed, float MeleeSpeed, float InitialEdge, float MinEdge, float FinalEdge,
        int Steps, bool Finished, string Winner, bool EverEngaged,
        int AliveSteps, int LocoSteps, int TreadWindows, int StationarySteps, float Path, float RangerPath,
        float RangerHpFracEnd);

    private static ChaseStats MeasureChase(float rangerSpeed, float meleeSpeed, int seed, int budget)
    {
        // Chaser is deliberately tanky with a weak-hitting ranger so we observe many seconds of pure chase
        // dynamics rather than the chaser dying immediately — the question is "can it close", not balance.
        var allies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateUnit("ally_chaser", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter,
                hp: 240f, attack: 12f, defense: 2f, moveSpeed: meleeSpeed, attackRange: 1.2f, attackCooldown: 0.7f, leashDistance: 14f),
        };
        var enemies = new List<BattleUnitLoadout>
        {
            CombatTestFactory.CreateUnit("enemy_ranger", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackCenter,
                hp: 30f, attack: 3f, defense: 1f, moveSpeed: rangerSpeed, attackRange: 5f, attackCooldown: 0.9f, leashDistance: 8f),
        };
        var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: seed);
        var sim = new BattleSimulator(state, budget);

        var samples = new List<(CombatVector2 Pos, bool Alive)>();
        float initialEdge = -1f, minEdge = float.MaxValue, finalEdge = -1f, rangerPath = 0f;
        CombatVector2? rangerPrev = null;
        var steps = 0;
        var everEngaged = false;
        var winner = "(none)";
        var rangerHpFrac = 1f;
        const float chaserRange = 1.2f;

        while (!sim.IsFinished)
        {
            var step = sim.Step();
            steps = step.StepIndex;
            if (step.Winner != null)
            {
                winner = step.Winner.ToString();
            }

            var m = state.Allies.Count > 0 ? state.Allies[0] : null;
            var r = state.Enemies.Count > 0 ? state.Enemies[0] : null;
            if (m == null || r == null)
            {
                break;
            }

            rangerHpFrac = r.HealthRatio; // last value = end-of-battle ranger HP (0 if the chaser killed it)
            samples.Add((m.Position, m.IsAlive));
            if (m.IsAlive && r.IsAlive)
            {
                var edge = MovementResolver.ComputeEdgeDistance(m, r);
                if (initialEdge < 0f)
                {
                    initialEdge = edge;
                }

                if (edge < minEdge)
                {
                    minEdge = edge;
                }

                finalEdge = edge;
                if (edge <= chaserRange + 0.25f)
                {
                    everEngaged = true;
                }

                if (rangerPrev is { } rp)
                {
                    rangerPath += rp.DistanceTo(r.Position);
                }

                rangerPrev = r.Position;
            }
        }

        int alive = 0, loco = 0, tread = 0, stationary = 0;
        var path = 0f;
        for (var i = 1; i < samples.Count; i++)
        {
            if (!samples[i].Alive || !samples[i - 1].Alive)
            {
                continue;
            }

            alive++;
            var stepNet = samples[i - 1].Pos.DistanceTo(samples[i].Pos);
            path += stepNet;
            if (stepNet > LocomotingStepDisplacement)
            {
                loco++;
            }
            else
            {
                stationary++;
            }

            if (i >= Window && samples[i - Window].Alive)
            {
                float wp = 0f;
                var ok = true;
                for (var w = i - Window + 1; w <= i; w++)
                {
                    if (!samples[w].Alive || !samples[w - 1].Alive)
                    {
                        ok = false;
                        break;
                    }

                    wp += samples[w - 1].Pos.DistanceTo(samples[w].Pos);
                }

                if (ok)
                {
                    var wn = samples[i - Window].Pos.DistanceTo(samples[i].Pos);
                    var eff = wp > 1e-4f ? wn / wp : 1f;
                    if (wp >= WindowMovedFloor && eff < WindowEfficiencyCeil)
                    {
                        tread++;
                    }
                }
            }
        }

        return new ChaseStats(rangerSpeed, meleeSpeed, initialEdge, minEdge == float.MaxValue ? -1f : minEdge,
            finalEdge, steps, sim.IsFinished, winner, everEngaged, alive, loco, tread, stationary, path, rangerPath,
            rangerHpFrac);
    }

    private static ChaseStats AverageChase(float rangerSpeed, float meleeSpeed, int seeds, int budget)
    {
        float initEdge = 0f, minEdge = 0f, finalEdge = 0f, path = 0f, rangerPath = 0f, rngHp = 0f;
        int steps = 0, finished = 0, engaged = 0, alive = 0, loco = 0, tread = 0, stationary = 0;
        for (var s = 0; s < seeds; s++)
        {
            var c = MeasureChase(rangerSpeed, meleeSpeed, s, budget);
            initEdge += c.InitialEdge;
            minEdge += c.MinEdge;
            finalEdge += c.FinalEdge;
            path += c.Path;
            rangerPath += c.RangerPath;
            rngHp += c.RangerHpFracEnd;
            steps += c.Steps;
            finished += c.Finished ? 1 : 0;
            engaged += c.EverEngaged ? 1 : 0;
            alive += c.AliveSteps;
            loco += c.LocoSteps;
            tread += c.TreadWindows;
            stationary += c.StationarySteps;
        }

        return new ChaseStats(rangerSpeed, meleeSpeed, initEdge / seeds, minEdge / seeds, finalEdge / seeds,
            steps / seeds, finished == seeds, $"{engaged}/{seeds} engaged", engaged > 0,
            alive, loco, tread, stationary, path, rangerPath, rngHp / seeds);
    }

    /// <summary>
    /// Phase 0 ACCEPTANCE (rewritten from the old buggy-behavior diagnostic). A melee chaser must close to
    /// attack range and connect against a backline ranger, with no engagement-boundary oscillation. On the clean
    /// foundation the ranger stands and shoots (no kite), there is no combat leash, windups are committed, and
    /// range uses one rule — so the chase resolves to a clean engage instead of the old "run in place at ~1.4m"
    /// treadmill. The previous version asserted the BUG (that a faster chaser oscillated MORE); that is now fixed,
    /// so the assertion is inverted into the acceptance below. Still emits the sweep report for inspection.
    /// </summary>
    [Test]
    public void Measure_MeleeChasingRanger_EndgameChase()
    {
        const int seeds = 4;
        const int budget = 400; // 40s — long enough to expose the treadmill, bounded so parity can't hang.
        const float melee = 2.1f; // duelist content-ish chase speed

        var rows = new[]
        {
            AverageChase(rangerSpeed: 1.7f, meleeSpeed: melee, seeds, budget), // slower ranger
            AverageChase(rangerSpeed: 1.9f, meleeSpeed: melee, seeds, budget),
            AverageChase(rangerSpeed: 2.1f, meleeSpeed: melee, seeds, budget), // PARITY (the bug)
            AverageChase(rangerSpeed: 2.3f, meleeSpeed: melee, seeds, budget), // faster ranger
            AverageChase(rangerSpeed: 2.1f, meleeSpeed: 2.7f, seeds, budget),  // faster CHASER vs parity ranger
        };

        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"=== ENDGAME MELEE-CHASES-RANGER (1v1 isolation, seeds 0..{seeds - 1}, budget {budget} steps) ===");
        sb.AppendLine("ranger kites at MoveSpeed*step via BreakContact; chaser approaches at MoveSpeed*step. parity => net0.");
        sb.AppendLine($"{"rangerSpd",-10}{"meleeSpd",-9}{"initEdge",-9}{"minEdge",-9}{"finalEdge",-10}{"engaged",-9}{"steps",-7}{"loco%",-8}{"tread%",-8}{"stat%",-8}{"rngHP%",-8}");
        sb.AppendLine("(rngHP% = ranger end HP: ~100% => the chaser landed NOTHING; low/0 => it actually attacked/killed)");
        foreach (var r in rows)
        {
            sb.AppendLine(
                $"{r.RangerSpeed.ToString("F1", ci),-10}{r.MeleeSpeed.ToString("F1", ci),-9}" +
                $"{r.InitialEdge.ToString("F2", ci),-9}{r.MinEdge.ToString("F2", ci),-9}{r.FinalEdge.ToString("F2", ci),-10}" +
                $"{r.Winner,-9}{r.Steps,-7}" +
                $"{Pct(r.LocoSteps, r.AliveSteps),-8}{Pct(r.TreadWindows, r.AliveSteps),-8}{Pct(r.StationarySteps, r.AliveSteps),-8}" +
                $"{r.RangerHpFracEnd.ToString("P0", ci),-8}");
        }

        var report = sb.ToString();
        TestContext.WriteLine(report);

        var dir = Path.Combine("Captures", "treadmill-diag");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "endgame-chase.txt"), report);

        var parity = rows[2];      // ranger 2.1 vs melee 2.1
        var fasterChaser = rows[4]; // ranger 2.1 vs melee 2.7

        // Phase 0 clean-foundation acceptance. The ranger stands and shoots (no kite), there is no combat leash,
        // windups are committed, and range is one rule — so the chaser closes to attack range and connects
        // regardless of speed, and a faster chaser no longer overshoots and oscillates at the engagement
        // boundary (the old bug this test used to characterize).
        var fasterTread = fasterChaser.AliveSteps > 0 ? (float)fasterChaser.TreadWindows / fasterChaser.AliveSteps : 0f;
        Assert.That(parity.MinEdge, Is.LessThan(1.6f),
            "a parity chaser should close into attack range against a stand-and-shoot ranger (no stall/treadmill)");
        Assert.That(fasterChaser.MinEdge, Is.LessThan(1.6f),
            "a faster chaser should also close cleanly into attack range");
        Assert.That(fasterTread, Is.LessThan(0.25f),
            "a faster chaser closes cleanly now instead of oscillating at the engagement boundary");
    }

    // ── Mixed endgame: melee chasers + kiting ranger + corpses (the user's actual severe case) ──
    // The all-melee headline never has a kiter; the 1v1 isolation has no corpses/crowd. This roster (2 duelist +
    // 1 ranger per side) produces the real endgame the user describes: duelists trade and die, then surviving
    // melee chase the lone kiting ranger across a field strewn with corpses. We split treadmill by class to show
    // WHERE it concentrates (the melee chaser, not the kiter) and report post-death incidence to compare against
    // the all-melee baseline (~20%).
    private static (List<BattleUnitLoadout> Allies, List<BattleUnitLoadout> Enemies) BuildMixedEndgame()
    {
        List<BattleUnitLoadout> Side(string p, string race) => new()
        {
            CombatTestFactory.CreateUnit($"{p}_due_1", race: race, classId: "duelist", anchor: DeploymentAnchorId.FrontTop, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f),
            CombatTestFactory.CreateUnit($"{p}_due_2", race: race, classId: "duelist", anchor: DeploymentAnchorId.FrontBottom, hp: 34f, attack: 9f, defense: 1f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f),
            CombatTestFactory.CreateUnit($"{p}_rng_1", race: race, classId: "ranger", anchor: DeploymentAnchorId.BackCenter, hp: 26f, attack: 6f, defense: 1f, moveSpeed: 1.9f, attackRange: 5f, attackCooldown: 0.9f),
        };

        return (Side("ally", "human"), Side("enemy", "undead"));
    }

    private readonly record struct MixedStats(
        int Alive, int Loco, int Tread, int Stationary, int AliveAD, int TreadAD,
        int DueAlive, int DueTread, int RngAlive, int RngTread, int Steps, bool Finished);

    private static MixedStats MeasureMixed(int seed)
    {
        var (allies, enemies) = BuildMixedEndgame();
        var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: seed);
        var sim = new BattleSimulator(state, 600);
        var positions = new Dictionary<string, List<(int Step, CombatVector2 Pos, bool Alive)>>();
        var firstDeath = int.MaxValue;
        var steps = 0;

        while (!sim.IsFinished)
        {
            var step = sim.Step();
            steps = step.StepIndex;
            foreach (var u in step.Units)
            {
                if (!positions.TryGetValue(u.Id, out var l))
                {
                    l = new List<(int, CombatVector2, bool)>();
                    positions[u.Id] = l;
                }

                l.Add((step.StepIndex, u.Position, u.IsAlive));
                if (!u.IsAlive && firstDeath == int.MaxValue)
                {
                    firstDeath = step.StepIndex;
                }
            }
        }

        int alive = 0, loco = 0, tread = 0, stationary = 0, aliveAD = 0, treadAD = 0;
        int dueAlive = 0, dueTread = 0, rngAlive = 0, rngTread = 0;
        foreach (var (id, samples) in positions)
        {
            var isRanger = id.Contains("_rng_");
            for (var i = 1; i < samples.Count; i++)
            {
                if (!samples[i].Alive || !samples[i - 1].Alive)
                {
                    continue;
                }

                alive++;
                var stepNet = samples[i - 1].Pos.DistanceTo(samples[i].Pos);
                if (stepNet > LocomotingStepDisplacement)
                {
                    loco++;
                }
                else
                {
                    stationary++;
                }

                if (i >= Window && samples[i - Window].Alive)
                {
                    float wp = 0f;
                    var ok = true;
                    for (var w = i - Window + 1; w <= i; w++)
                    {
                        if (!samples[w].Alive || !samples[w - 1].Alive)
                        {
                            ok = false;
                            break;
                        }

                        wp += samples[w - 1].Pos.DistanceTo(samples[w].Pos);
                    }

                    if (ok)
                    {
                        var wn = samples[i - Window].Pos.DistanceTo(samples[i].Pos);
                        var eff = wp > 1e-4f ? wn / wp : 1f;
                        var tm = wp >= WindowMovedFloor && eff < WindowEfficiencyCeil;
                        if (tm)
                        {
                            tread++;
                        }

                        if (samples[i].Step >= firstDeath)
                        {
                            aliveAD++;
                            if (tm)
                            {
                                treadAD++;
                            }
                        }

                        if (isRanger)
                        {
                            rngAlive++;
                            if (tm)
                            {
                                rngTread++;
                            }
                        }
                        else
                        {
                            dueAlive++;
                            if (tm)
                            {
                                dueTread++;
                            }
                        }
                    }
                }
            }
        }

        return new MixedStats(alive, loco, tread, stationary, aliveAD, treadAD,
            dueAlive, dueTread, rngAlive, rngTread, steps, sim.IsFinished);
    }

    /// <summary>
    /// DIAGNOSTIC for the user's severe endgame treadmill. Confirms the treadmill concentrates in the melee
    /// chaser (not the kiter) and spikes in the corpse phase — i.e. the bug is the deliberate-pursuit gap, the
    /// fix is the committed-motion redesign. Loose asserts; the numbers (mixed-endgame.txt) are the output.
    /// </summary>
    [Test]
    public void Measure_MixedEndgame_ChaseTreadmill()
    {
        const int seeds = 24;
        int alive = 0, loco = 0, tread = 0, stationary = 0, aliveAD = 0, treadAD = 0;
        int dueAlive = 0, dueTread = 0, rngAlive = 0, rngTread = 0, steps = 0, finished = 0;
        for (var s = 0; s < seeds; s++)
        {
            var m = MeasureMixed(s);
            alive += m.Alive;
            loco += m.Loco;
            tread += m.Tread;
            stationary += m.Stationary;
            aliveAD += m.AliveAD;
            treadAD += m.TreadAD;
            dueAlive += m.DueAlive;
            dueTread += m.DueTread;
            rngAlive += m.RngAlive;
            rngTread += m.RngTread;
            steps += m.Steps;
            finished += m.Finished ? 1 : 0;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== MIXED ENDGAME (2 duelist + 1 ranger per side, seeds 0..{seeds - 1}) ===");
        sb.AppendLine($"finished: {finished}/{seeds}, avg steps {(seeds > 0 ? steps / seeds : 0)}");
        sb.AppendLine($"locomoting (run plays):       {Pct(loco, alive)}");
        sb.AppendLine($"stationary (idle/holds):      {Pct(stationary, alive)}");
        sb.AppendLine($"TREADMILL windows:            {Pct(tread, alive)}");
        sb.AppendLine($"  post-death (corpses present): {Pct(treadAD, aliveAD)}   (all-melee baseline ~20%)");
        sb.AppendLine($"  duelist (melee chasers):      {Pct(dueTread, dueAlive)}");
        sb.AppendLine($"  ranger  (kiters):             {Pct(rngTread, rngAlive)}");
        var report = sb.ToString();
        TestContext.WriteLine(report);

        var dir = Path.Combine("Captures", "treadmill-diag");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "mixed-endgame.txt"), report);

        Assert.That(alive, Is.GreaterThan(0));
        Assert.That(finished, Is.GreaterThan(0), "mixed endgame battles should resolve at least sometimes");
    }

    /// <summary>
    /// The user's ACTUAL corrected scenario: the ranged unit is STATIONARY (shoots in place — it is NOT kiting),
    /// and the MELEE fails to approach and runs the treadmill. Hypothesis: the melee's leash clamps it to a radius
    /// around its anchor, so a stationary backline ranged beyond that radius is unreachable — the melee stalls /
    /// slides at the leash boundary, and the ranged never moves because the melee never enters its retreat
    /// trigger. Trace a melee approaching a speed-0 ranged at several leash values to see whether the leash is the
    /// wall. Output is Captures/treadmill-diag/stationary-approach.txt.
    /// </summary>
    [Test]
    public void Trace_MeleeApproachStationaryRanged()
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("=== MELEE APPROACH STATIONARY RANGED (speed-0 ranged, seed 7) ===");
        sb.AppendLine("if leash is the wall: distFromAnchor caps at ~leash and edge stalls at (initEdge - leash); ranged HP stays 100%.");

        var reach = new List<(float Leash, float MinEdge, float RangedHp)>();
        foreach (var leash in new[] { 5f, 6f, 8f, 14f })
        {
            var allies = new List<BattleUnitLoadout>
            {
                CombatTestFactory.CreateUnit("ally_melee", classId: "duelist", anchor: DeploymentAnchorId.FrontCenter,
                    hp: 240f, attack: 12f, defense: 2f, moveSpeed: 2.1f, attackRange: 1.2f, attackCooldown: 0.7f, leashDistance: leash),
            };
            var enemies = new List<BattleUnitLoadout>
            {
                CombatTestFactory.CreateUnit("enemy_ranged", race: "undead", classId: "ranger", anchor: DeploymentAnchorId.BackCenter,
                    hp: 30f, attack: 3f, defense: 1f, moveSpeed: 0f, attackRange: 5f, attackCooldown: 0.9f, leashDistance: 8f),
            };
            var state = CombatTestFactory.CreateBattleState(allies, enemies, seed: 7);
            var sim = new BattleSimulator(state, 150);

            float minEdge = float.MaxValue, initEdge = -1f;
            var traceLines = new List<string>();
            int loco = 0, aliveSteps = 0;
            CombatVector2? prevPos = null;
            while (!sim.IsFinished)
            {
                var step = sim.Step();
                var m = state.Allies[0];
                var r = state.Enemies[0];
                if (!m.IsAlive || !r.IsAlive)
                {
                    continue;
                }

                var edge = MovementResolver.ComputeEdgeDistance(m, r);
                if (initEdge < 0f)
                {
                    initEdge = edge;
                }

                if (edge < minEdge)
                {
                    minEdge = edge;
                }

                if (prevPos is { } pp)
                {
                    aliveSteps++;
                    if (pp.DistanceTo(m.Position) > 0.015f)
                    {
                        loco++;
                    }
                }

                prevPos = m.Position;
                if (step.StepIndex < 4 || step.StepIndex % 12 == 0)
                {
                    var distFromAnchor = m.Position.DistanceTo(m.AnchorPosition);
                    traceLines.Add($"  step {step.StepIndex,3}: edge={edge.ToString("F2", ci),5}  distFromAnchor={distFromAnchor.ToString("F2", ci),5}  state={m.ActionState,-14} rngHP={r.HealthRatio.ToString("P0", ci)}");
                }
            }

            sb.AppendLine();
            reach.Add((leash, minEdge, state.Enemies[0].HealthRatio));
            sb.AppendLine($"-- leash {leash.ToString("F0", ci)}: initEdge={initEdge.ToString("F2", ci)} minEdge={minEdge.ToString("F2", ci)} finished={sim.IsFinished} loco={Pct(loco, aliveSteps)} rangedHP_end={state.Enemies[0].HealthRatio.ToString("P0", ci)} --");
            foreach (var t in traceLines.Take(12))
            {
                sb.AppendLine(t);
            }
        }

        var report = sb.ToString();
        TestContext.WriteLine(report);
        var dir = Path.Combine("Captures", "treadmill-diag");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "stationary-approach.txt"), report);

        // Regression gate for the leash-reach fix: a melee must reach a stationary backline target at EVERY leash,
        // including the content-default ~5m (the value that previously pinned it at the spawn-leash boundary and
        // made it "treadmill"). Before the fix, leash 5 stalled at minEdge ~2.5 with the ranged at 100% HP.
        foreach (var (leash, minEdge, rangedHp) in reach)
        {
            Assert.That(minEdge, Is.LessThan(1.5f),
                $"leash {leash}: melee failed to reach strike range of a stationary target (leash-reach regression)");
            Assert.That(rangedHp, Is.EqualTo(0f),
                $"leash {leash}: melee reached but did not kill the stationary target (it should connect once in range)");
        }
    }
}
