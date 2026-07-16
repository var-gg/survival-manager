using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class H100HeadlessMetricsTests
{
    [Test]
    public void ReplayHash_SameSeedAndInput_IsIdentical()
    {
        var first = RunBattle(1701);
        var second = RunBattle(1701);

        Assert.That(ReplayHash.Compute(first.State, first.Result.ActivityTelemetry),
            Is.EqualTo(ReplayHash.Compute(second.State, second.Result.ActivityTelemetry)));
        Assert.That(BattleStateCanonicalHash.Compute(first.State),
            Is.EqualTo(BattleStateCanonicalHash.Compute(second.State)));
    }

    [Test]
    public void Projector_EmitsCoreBattleAndFormationFields()
    {
        var run = RunBattle(1701);
        var record = BattleMetricProjector.Project(
            "run", "campaign", "battle", "group", 0, "scenario", "policy",
            run.State, run.Result, BattleSimulator.DefaultMaxSteps);

        Assert.That(record.StepCount, Is.GreaterThan(0));
        Assert.That(record.DurationSeconds, Is.GreaterThan(0f));
        Assert.That(record.WinnerSide, Is.EqualTo("ally").Or.EqualTo("enemy"));
        Assert.That(record.ReplayHash, Does.Match("^[0-9a-f]{16}$"));
        Assert.That(record.CanonicalStateHash, Does.Match("^[0-9a-f]{16}$"));
        Assert.That(record.BuildFamilyId, Is.Not.Empty);
        Assert.That(record.OpponentFamilyId, Is.Not.Empty);
        Assert.That(record.AllyFormationId, Is.Not.Empty);
        Assert.That(record.EnemyFormationId, Is.Not.Empty);
        Assert.That(record.AllySurvivingHp + record.EnemySurvivingHp, Is.GreaterThan(0f));
    }

    [Test]
    public void Projector_SeparatesForcedTimeoutFromNonTerminatingState()
    {
        var ally = CombatTestFactory.CreateUnit("ally", hp: 1000f, attack: 1f);
        var enemy = CombatTestFactory.CreateUnit("enemy", race: "undead", hp: 1000f, attack: 1f);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: 1702);
        var result = BattleResolver.Run(state, maxTicks: 1);

        var record = BattleMetricProjector.Project(
            "run", "campaign", "battle", "group", 0, "scenario", "policy",
            state, result, maxSteps: 1);

        Assert.That(record.Timeout, Is.True);
        Assert.That(record.NonTerminating, Is.False);
    }

    [Test]
    public void ArtifactWriter_IsByteDeterministic_AndSortsRuleIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm-h100-metrics-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var battle = new BattleMetricRecord
            {
                RunId = "run",
                BattleId = "battle",
                ReplayGroupId = "group",
                SynergyRuleActivationCounts = new[]
                {
                    new MetricCount("zeta", 1),
                    new MetricCount("alpha", 2),
                },
            };
            var campaign = new CampaignMetricRecord
            {
                RunId = "run",
                CampaignId = "campaign",
                Truncated = true,
                TerminalReason = "site-safety-exhausted",
            };
            var report = new GateReport { SpecVersion = "test", OverallPass = false };
            var first = HeadlessMetricArtifactWriter.Write(root, new[] { battle }, new[] { campaign }, report, writeCsv: true);
            var firstJson = File.ReadAllText(first.BattleJsonlPath);
            var firstCsv = File.ReadAllText(first.BattleCsvPath!);
            var firstCampaignCsv = File.ReadAllText(first.CampaignCsvPath!);
            var second = HeadlessMetricArtifactWriter.Write(root, new[] { battle }, new[] { campaign }, report, writeCsv: true);

            Assert.That(File.ReadAllText(second.BattleJsonlPath), Is.EqualTo(firstJson));
            Assert.That(File.ReadAllText(second.BattleCsvPath!), Is.EqualTo(firstCsv));
            Assert.That(File.ReadAllText(second.CampaignCsvPath!), Is.EqualTo(firstCampaignCsv));
            Assert.That(firstJson.IndexOf("alpha", StringComparison.Ordinal),
                Is.LessThan(firstJson.IndexOf("zeta", StringComparison.Ordinal)));
            AssertCsvColumnCountIsStable(firstCsv);
            AssertCsvColumnCountIsStable(firstCampaignCsv);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void ReplayManifestHash_IsIndependentOfInputEnumerationOrder()
    {
        Assert.That(
            ReplayHash.ComputeManifest(new[] { "hash-b", "hash-a", "hash-a" }),
            Is.EqualTo(ReplayHash.ComputeManifest(new[] { "hash-a", "hash-b", "hash-a" })));
    }

    [Test]
    public void GateEvaluator_MissingObservationFailsClosed()
    {
        var spec = new H100GateSpec
        {
            SchemaVersion = "test-schema",
            SpecVersion = "test-v1",
            ThresholdPolicyNote = "test policy",
            TargetBattleSeconds = 35f,
            Gates = new List<H100GateSpec.GateDefinition>
            {
                new()
                {
                    Id = "integrity",
                    NameKo = "무결성",
                    Measurement = "test measurement",
                    Thresholds = new List<H100GateSpec.ThresholdDefinition>
                    {
                        new() { MetricId = "battle_replay_group_count", Operator = "gte", Value = 1 },
                        new() { MetricId = "unresolved_sev_1_2_count", Operator = "eq", Value = 0 },
                    },
                },
            },
        };
        var battles = new[]
        {
            new BattleMetricRecord { ReplayGroupId = "same", ReplayIteration = 0, ReplayHash = "abc" },
            new BattleMetricRecord { ReplayGroupId = "same", ReplayIteration = 1, ReplayHash = "abc" },
        };

        var report = H100GateEvaluator.Generate(spec, battles, Array.Empty<CampaignMetricRecord>());

        Assert.That(report.OverallPass, Is.False);
        var missing = report.Gates.Single().Thresholds.Single(result => result.MetricId == "unresolved_sev_1_2_count");
        Assert.That(missing.Observed, Is.False);
        Assert.That(missing.Pass, Is.False);
    }

    [Test]
    public void CheckedInGateSpec_HasTenAndGates_AndExactTargetTime()
    {
        var spec = H100GateSpec.LoadFromFile(
            Path.Combine("Assets", "_Game", "Scripts", "Runtime", "HeadlessMetrics", "h100-gates-v1.json"));

        Assert.That(spec.Gates, Has.Count.EqualTo(10));
        Assert.That(spec.TargetBattleSeconds, Is.EqualTo(35f));
        Assert.That(spec.Gates.Select(gate => gate.Id), Is.Unique);
        Assert.That(spec.ThresholdPolicyNote, Does.Contain("1회만 조정"));
    }

    private static (BattleState State, BattleResult Result) RunBattle(int seed)
    {
        var ally = CombatTestFactory.CreateUnit(
            "ally",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 2.1f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit(
            "enemy",
            race: "undead",
            anchor: DeploymentAnchorId.FrontCenter,
            hp: 60f,
            moveSpeed: 1.9f,
            attackRange: 1.2f,
            attackWindup: 0.1f,
            attackCooldown: 0.5f);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: seed);
        var result = BattleResolver.Run(state, BattleSimulator.DefaultMaxSteps);
        return (state, result);
    }

    private static void AssertCsvColumnCountIsStable(string csv)
    {
        var lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines, Has.Length.EqualTo(2));
        Assert.That(lines[1].Split(',').Length, Is.EqualTo(lines[0].Split(',').Length));
    }
}
