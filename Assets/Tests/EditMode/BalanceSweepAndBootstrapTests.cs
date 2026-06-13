using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Editor.Bootstrap;
using SM.Editor.SeedData;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BalanceSweepAndBootstrapTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(BalanceSweepAndBootstrapTests));
    }

    [Test]
    public void BalanceSweepScenarioFactory_BuildsStableSmokeInputs()
    {
        var first = SM.Editor.Validation.BalanceSweepScenarioFactory.BuildSmokeScenarios();
        var second = SM.Editor.Validation.BalanceSweepScenarioFactory.BuildSmokeScenarios();

        Assert.That(first.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(first.Select(input => input.ScenarioId), Is.EqualTo(second.Select(input => input.ScenarioId)));
        Assert.That(first.All(input => input.PlayerSnapshot.Allies.Count == 4), Is.True);
        Assert.That(first.All(input => input.EnemyLoadout.Count > 0), Is.True);
        Assert.That(first.Select(input => input.PlayerSnapshot.CompileHash), Is.EqualTo(second.Select(input => input.PlayerSnapshot.CompileHash)));
    }

    [Test]
    public void BalanceSweepScenarioFactory_BuildsAllThreatTopologyScenarios()
    {
        var scenarios = SM.Editor.Validation.BalanceSweepScenarioFactory.BuildThreatTopologyScenarios();

        Assert.That(scenarios.Select(input => input.ScenarioId), Is.EqualTo(new[]
        {
            "ArmorFrontlineScenario",
            "ResistanceShellScenario",
            "GuardBulwarkScenario",
            "EvasiveSkirmishScenario",
            "ControlChainScenario",
            "SustainBallScenario",
            "DiveBacklineScenario",
            "SwarmFloodScenario",
        }));
        Assert.That(scenarios.All(input => input.PlayerSnapshot.TeamCounterCoverage != null), Is.True);
        Assert.That(scenarios.All(input => input.EnemyLoadout.Count > 0), Is.True);
    }

    [Test]
    public void BalanceSweepScenarioFactory_ReportsCoverageMatrixAxes()
    {
        var rows = SM.Editor.Validation.BalanceSweepScenarioFactory.BuildCoverageMatrixRows();

        Assert.That(rows.Count(row => row.SourceLane == "balance-sweep-smoke"), Is.EqualTo(2));
        Assert.That(rows.Count(row => row.SourceLane == "balance-sweep-threat-topology"), Is.EqualTo(8));
        Assert.That(rows.Count(row => row.SourceLane.StartsWith("loopd-purekit")), Is.EqualTo(12));
        Assert.That(rows.Select(row => row.ScenarioId), Contains.Item("RunLite_EconomyChoice"));
        Assert.That(rows.Single(row => row.ScenarioId == "SustainBall_vs_BurstSpike").KpiCoverage, Does.Contain("synergy_uplift"));
        Assert.That(rows.Single(row => row.ScenarioId == "RunLite_EconomyChoice").KpiCoverage, Does.Contain("dead_offer_ratio"));
        Assert.That(rows.Single(row => row.ScenarioId == "MixedDraft_4v4").GapDisposition, Does.Contain("ManualLoopD"));
    }

    [Test]
    public void RequireSampleContentReady_DoesNotRewriteCommittedFloorContent()
    {
        const string contentPath = "Assets/Resources/_Game/Content/Definitions/Archetypes/archetype_bulwark.asset";
        var before = File.ReadAllText(contentPath);

        FirstPlayableContentBootstrap.RequireSampleContentReady(nameof(RequireSampleContentReady_DoesNotRewriteCommittedFloorContent));

        var after = File.ReadAllText(contentPath);

        Assert.That(after, Is.EqualTo(before));
        Assert.That(SampleSeedGenerator.TryGetCanonicalSampleContentReadinessIssue(out _), Is.True);
    }
}
