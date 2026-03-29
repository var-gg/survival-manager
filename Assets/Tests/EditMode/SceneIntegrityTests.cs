using System.IO;
using NUnit.Framework;
using SM.Editor.Bootstrap;
using SM.Editor.SeedData;

namespace SM.Tests.EditMode;

public sealed class SceneIntegrityTests
{
    [OneTimeSetUp]
    public void PrepareCanonicalContentAndTemporarySceneBridge()
    {
        FirstPlayableContentBootstrap.EnsureSampleContent();
        FirstPlayableSceneInstaller.RebuildFirstPlayableScenes();
    }

    [Test]
    public void CanonicalContentRoot_Has_Minimum_Core_Assets()
    {
        Assert.That(Directory.Exists(SampleSeedGenerator.ResourcesRoot), Is.True, $"Missing canonical content root: {SampleSeedGenerator.ResourcesRoot}");
        AssertAssetContains($"{SampleSeedGenerator.ResourcesRoot}/Stats/stat_max_health.asset", "SM.Content.Definitions:StatDefinition", "Id: max_health");
        AssertAssetContains($"{SampleSeedGenerator.ResourcesRoot}/Races/race_human.asset", "SM.Content.Definitions:RaceDefinition", "Id: human");
        AssertAssetContains($"{SampleSeedGenerator.ResourcesRoot}/Classes/class_vanguard.asset", "SM.Content.Definitions:ClassDefinition", "Id: vanguard");
        AssertAssetContains($"{SampleSeedGenerator.ResourcesRoot}/Archetypes/archetype_warden.asset", "SM.Content.Definitions:UnitArchetypeDefinition", "Id: warden");
    }

    [TestCase("Assets/_Game/Scenes/Boot.unity")]
    [TestCase("Assets/_Game/Scenes/Town.unity")]
    [TestCase("Assets/_Game/Scenes/Expedition.unity")]
    [TestCase("Assets/_Game/Scenes/Battle.unity")]
    [TestCase("Assets/_Game/Scenes/Reward.unity")]
    public void PlayableSceneAssets_Exist(string scenePath)
    {
        Assert.That(File.Exists(scenePath), Is.True, $"Playable scene asset is missing: {scenePath}");
    }

    [Test]
    public void BootScene_TemporaryBridge_Saves_GameBootstrap_Camera_And_Canvas()
    {
        AssertSceneContains("Boot", "m_Name: GameBootstrap", "SM.Unity::SM.Unity.GameBootstrap", "m_Name: BootCanvas", "m_Name: Main Camera");
    }

    [Test]
    public void TownScene_TemporaryBridge_Saves_Controller_Canvas_And_EventSystem()
    {
        AssertSceneContains("Town", "m_Name: TownScreenController", "SM.Unity::SM.Unity.TownScreenController", "m_Name: TownCanvas", "m_Name: EventSystem");
    }

    [Test]
    public void BattleScene_TemporaryBridge_Saves_Controller_Canvas_And_EventSystem()
    {
        AssertSceneContains("Battle", "m_Name: BattleScreenController", "SM.Unity::SM.Unity.BattleScreenController", "m_Name: BattleCanvas", "m_Name: EventSystem");
    }

    private static void AssertAssetContains(string assetPath, params string[] fragments)
    {
        Assert.That(File.Exists(assetPath), Is.True, $"Canonical sample asset is missing: {assetPath}");
        var text = File.ReadAllText(assetPath);
        foreach (var fragment in fragments)
        {
            Assert.That(text, Does.Contain(fragment), $"Canonical sample asset contract missing '{fragment}' in {assetPath}");
        }
    }

    private static void AssertSceneContains(string sceneName, params string[] fragments)
    {
        var scenePath = $"Assets/_Game/Scenes/{sceneName}.unity";
        Assert.That(File.Exists(scenePath), Is.True, $"Playable scene asset is missing: {scenePath}");

        var text = File.ReadAllText(scenePath);
        foreach (var fragment in fragments)
        {
            Assert.That(text, Does.Contain(fragment), $"Temporary scene bridge until block25 must save '{fragment}' into {scenePath}");
        }
    }
}
