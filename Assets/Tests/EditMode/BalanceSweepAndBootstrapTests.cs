using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Editor.Bootstrap;
using SM.Editor.SeedData;

namespace SM.Tests.EditMode;

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
