using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.HeadlessCensus;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BuildSpaceCensusFastTests
{
    private BuildSpaceCensus _census = null!;

    [OneTimeSetUp]
    public void BuildCanonicalCensus()
    {
        _census = BuildSpaceEnumerator.Generate(CanonicalGrid());
        CanonicalBuildSpaceContract.RequireExpected(_census);
    }

    [Test]
    public void CanonicalGrid_EnumeratesAllExpectedStructuralCounts()
    {
        var summary = _census.Summary;
        Assert.That(summary.TotalCombinations, Is.EqualTo(495));
        Assert.That(summary.FormationPlacementsPerCombination, Is.EqualTo(360));
        Assert.That(_census.EnumerateStates().Count(), Is.EqualTo(178200));
        Assert.That(summary.RaceTier2BuildCount, Is.EqualTo(495));
        Assert.That(summary.ClassTier2BuildCount, Is.EqualTo(414));
        Assert.That(summary.ClassTier3BuildCount, Is.EqualTo(36));
        Assert.That(summary.RaceTier4BuildCount, Is.EqualTo(3));
        Assert.That(summary.UpperDoctrineBuildCount, Is.EqualTo(39));
        Assert.That(summary.ExactThreeRaceBuildCount, Is.EqualTo(96));
        Assert.That(summary.RaceTwoPlusTwoBuildCount, Is.EqualTo(108));
        Assert.That(summary.ClassTwoPlusTwoBuildCount, Is.EqualTo(54));
        Assert.That(summary.RoleCompleteBuildCount, Is.EqualTo(81));
        Assert.That(summary.Flags.Select(flag => flag.Id), Is.EquivalentTo(new[]
        {
            "race-tier2-automatic",
            "race-three-dead-zone",
            "upper-doctrine-rarity-asymmetry",
        }));
    }

    [Test]
    public void SynergySignature_ReusesCanonicalDoctrineRuleIds()
    {
        var raceDoctrineCounts = _census.Combinations
            .Where(build => build.Synergy.RaceTier4Count == 1)
            .SelectMany(build => build.Synergy.DoctrineRuleIds)
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var classDoctrineCounts = _census.Combinations
            .Where(build => build.Synergy.ClassTier3Count == 1)
            .SelectMany(build => build.Synergy.DoctrineRuleIds)
            .GroupBy(id => id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.That(raceDoctrineCounts.Keys, Is.EquivalentTo(new[]
        {
            TeamRuleSet.PhalanxRuleId,
            TeamRuleSet.BloodrushRuleId,
            TeamRuleSet.DeathTollRuleId,
        }));
        Assert.That(raceDoctrineCounts.Values, Is.All.EqualTo(1));
        Assert.That(classDoctrineCounts.Keys, Is.EquivalentTo(new[]
        {
            TeamRuleSet.BulwarkRuleId,
            TeamRuleSet.ExecuteRuleId,
            TeamRuleSet.KillzoneRuleId,
            TeamRuleSet.ResonanceRuleId,
        }));
        Assert.That(classDoctrineCounts.Values, Is.All.EqualTo(9));
    }

    [Test]
    public void FormationMedoids_AreAutomaticDeterministicAndCoverAllPlacements()
    {
        var repeated = BuildSpaceEnumerator.Generate(CanonicalGrid());
        var firstSignatures = _census.Medoids.Select(medoid => medoid.Placement.Signature).ToArray();
        var repeatedSignatures = repeated.Medoids.Select(medoid => medoid.Placement.Signature).ToArray();

        Assert.That(firstSignatures, Has.Length.EqualTo(8));
        Assert.That(firstSignatures, Is.Unique);
        Assert.That(repeatedSignatures, Is.EqualTo(firstSignatures));
        Assert.That(repeated.Medoids.Select(medoid => medoid.ClusterSize),
            Is.EqualTo(_census.Medoids.Select(medoid => medoid.ClusterSize)));
        Assert.That(_census.Medoids.Sum(medoid => medoid.ClusterSize), Is.EqualTo(360));
    }

    [Test]
    public void ArtifactWriter_EmitsStableSortedCensusFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm-h100-census-fast", Guid.NewGuid().ToString("N"));
        var firstDirectory = Path.Combine(root, "first");
        var secondDirectory = Path.Combine(root, "second");
        try
        {
            var first = BuildSpaceArtifactWriter.Write(firstDirectory, _census);
            var second = BuildSpaceArtifactWriter.Write(secondDirectory, _census);

            Assert.That(File.ReadAllBytes(first.BuildSpaceCsvPath), Is.EqualTo(File.ReadAllBytes(second.BuildSpaceCsvPath)));
            Assert.That(File.ReadAllBytes(first.FormationSpaceCsvPath), Is.EqualTo(File.ReadAllBytes(second.FormationSpaceCsvPath)));
            Assert.That(File.ReadAllBytes(first.FormationMedoidsCsvPath), Is.EqualTo(File.ReadAllBytes(second.FormationMedoidsCsvPath)));
            Assert.That(File.ReadAllBytes(first.CensusReportPath), Is.EqualTo(File.ReadAllBytes(second.CensusReportPath)));
            Assert.That(File.ReadLines(first.BuildSpaceCsvPath).Count(), Is.EqualTo(496));
            Assert.That(File.ReadLines(first.FormationSpaceCsvPath).Count(), Is.EqualTo(361));
            Assert.That(File.ReadLines(first.FormationMedoidsCsvPath).Count(), Is.EqualTo(9));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static IReadOnlyList<BuildArchetype> CanonicalGrid()
    {
        return new[]
        {
            Archetype("warden", "human", "vanguard", BuildRole.Tank, DeploymentAnchorId.FrontCenter),
            Archetype("guardian", "undead", "vanguard", BuildRole.Tank, DeploymentAnchorId.FrontTop),
            Archetype("slayer", "human", "duelist", BuildRole.Damage, DeploymentAnchorId.FrontBottom),
            Archetype("raider", "beastkin", "duelist", BuildRole.Damage, DeploymentAnchorId.FrontTop),
            Archetype("hunter", "human", "ranger", BuildRole.Ranged, DeploymentAnchorId.BackTop),
            Archetype("scout", "beastkin", "ranger", BuildRole.Ranged, DeploymentAnchorId.BackBottom),
            Archetype("priest", "human", "mystic", BuildRole.Healer, DeploymentAnchorId.BackCenter),
            Archetype("hexer", "undead", "mystic", BuildRole.Healer, DeploymentAnchorId.BackCenter),
            Archetype("bulwark", "beastkin", "vanguard", BuildRole.Tank, DeploymentAnchorId.FrontBottom),
            Archetype("reaver", "undead", "duelist", BuildRole.Damage, DeploymentAnchorId.FrontCenter),
            Archetype("marksman", "undead", "ranger", BuildRole.Ranged, DeploymentAnchorId.BackCenter),
            Archetype("shaman", "beastkin", "mystic", BuildRole.Healer, DeploymentAnchorId.BackTop),
        };
    }

    private static BuildArchetype Archetype(
        string id,
        string race,
        string @class,
        BuildRole role,
        DeploymentAnchorId anchor)
        => new(id, race, @class, role, anchor);
}
