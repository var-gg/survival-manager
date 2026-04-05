using System.IO;
using NUnit.Framework;
using SM.Editor.Bootstrap;
using SM.Editor.SeedData;
using SM.Content.Definitions;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

public sealed class SceneIntegrityTests
{
    [OneTimeSetUp]
    public void PrepareCanonicalContentAndObserverPlayableScenes()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(SceneIntegrityTests));
        FirstPlayableSceneInstaller.RebuildFirstPlayableScenes();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    [Test]
    public void CanonicalContentRoot_Has_Minimum_Core_Assets()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(CanonicalContentRoot_Has_Minimum_Core_Assets));
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Assert.That(Directory.Exists(SampleSeedGenerator.ResourcesRoot), Is.True, $"Missing canonical content root: {SampleSeedGenerator.ResourcesRoot}");
        AssertCoreDefinition<StatDefinition>($"{SampleSeedGenerator.ResourcesRoot}/Stats/stat_max_health.asset", definition => definition.Id, "max_health");
        AssertCoreDefinition<RaceDefinition>($"{SampleSeedGenerator.ResourcesRoot}/Races/race_human.asset", definition => definition.Id, "human");
        AssertCoreDefinition<ClassDefinition>($"{SampleSeedGenerator.ResourcesRoot}/Classes/class_vanguard.asset", definition => definition.Id, "vanguard");
        AssertCoreDefinition<UnitArchetypeDefinition>($"{SampleSeedGenerator.ResourcesRoot}/Archetypes/archetype_warden.asset", definition => definition.Id, "warden");
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
    public void BootScene_Saves_GameBootstrap_Camera_And_Canvas()
    {
        AssertSceneContains("Boot", "m_Name: GameBootstrap", "SM.Unity::SM.Unity.GameBootstrap", "m_Name: BootCanvas", "m_Name: Main Camera");
    }

    [Test]
    public void TownScene_Saves_Controller_QuickBattle_And_RecruitCards()
    {
        AssertSceneContains(
            "Town",
            "m_Name: TownScreenController",
            "SM.Unity::SM.Unity.TownScreenController",
            "m_Name: TownCanvas",
            "m_Name: EventSystem",
            "Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule",
            "m_Name: RecruitCardsRoot",
            "m_Name: QuickBattleButton");
        AssertSceneDoesNotContain("Town", "StandaloneInputModule", "UiRaycastDebugger");
    }

    [Test]
    public void BattleScene_Saves_Replay_Presentation_And_Controls()
    {
        AssertSceneContains(
            "Battle",
            "m_Name: BattleScreenController",
            "SM.Unity::SM.Unity.BattleScreenController",
            "m_Name: BattlePresentationRoot",
            "SM.Unity::SM.Unity.BattlePresentationController",
            "m_Name: BattleStageRoot",
            "m_Name: ActorOverlayRoot",
            "m_Name: PauseButton",
            "m_Name: SettingsButton",
            "m_Name: SettingsPanel",
            "m_Name: ProgressFill",
            "m_Name: BattleCanvas",
            "m_Name: EventSystem",
            "Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        AssertSceneDoesNotContain("Battle", "StandaloneInputModule", "UiRaycastDebugger");
    }

    [Test]
    public void RewardScene_Saves_Controller_And_RewardCards()
    {
        AssertSceneContains(
            "Reward",
            "m_Name: RewardScreenController",
            "SM.Unity::SM.Unity.RewardScreenController",
            "m_Name: RewardCardsRoot",
            "m_Name: RewardCanvas",
            "m_Name: EventSystem",
            "Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        AssertSceneDoesNotContain("Reward", "StandaloneInputModule", "UiRaycastDebugger");
    }

    [Test]
    public void ExpeditionScene_Saves_Node_Selection_Buttons()
    {
        AssertSceneContains(
            "Expedition",
            "m_Name: ExpeditionScreenController",
            "SM.Unity::SM.Unity.ExpeditionScreenController",
            "m_Name: NodeTrackRoot",
            "m_Name: NodeBox1",
            "m_Name: SelectButton",
            "m_Name: NextBattleButton",
            "Unity.InputSystem::UnityEngine.InputSystem.UI.InputSystemUIInputModule");
        AssertSceneDoesNotContain("Expedition", "StandaloneInputModule", "UiRaycastDebugger");
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

    private static void AssertCoreDefinition<T>(string assetPath, System.Func<T, string> selector, string expectedId) where T : UnityEngine.Object
    {
        Assert.That(File.Exists(assetPath), Is.True, $"Canonical sample asset is missing: {assetPath}");
        var text = File.ReadAllText(assetPath);
        Assert.That(text, Does.Contain($"Id: {expectedId}"), $"Canonical sample asset contract missing '{expectedId}' in {assetPath}");
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var asset = Resources.Load<T>(ToResourcesLoadPath(assetPath)) ?? AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
        {
            Assert.That(selector(asset), Is.EqualTo(expectedId), $"Canonical sample asset contract missing '{expectedId}' in {assetPath}");
        }
    }

    private static string ToResourcesLoadPath(string assetPath)
    {
        const string resourcesPrefix = "Assets/Resources/";
        Assert.That(assetPath.StartsWith(resourcesPrefix), Is.True, $"Canonical sample asset is not under Resources: {assetPath}");
        var relativePath = assetPath.Substring(resourcesPrefix.Length);
        return Path.ChangeExtension(relativePath, null)!.Replace('\\', '/');
    }

    private static void AssertSceneContains(string sceneName, params string[] fragments)
    {
        var scenePath = $"Assets/_Game/Scenes/{sceneName}.unity";
        Assert.That(File.Exists(scenePath), Is.True, $"Playable scene asset is missing: {scenePath}");

        var text = File.ReadAllText(scenePath);
        foreach (var fragment in fragments)
        {
            Assert.That(text, Does.Contain(fragment), $"Observer playable scene contract missing '{fragment}' in {scenePath}");
        }
    }

    private static void AssertSceneDoesNotContain(string sceneName, params string[] fragments)
    {
        var scenePath = $"Assets/_Game/Scenes/{sceneName}.unity";
        Assert.That(File.Exists(scenePath), Is.True, $"Playable scene asset is missing: {scenePath}");

        var text = File.ReadAllText(scenePath);
        foreach (var fragment in fragments)
        {
            Assert.That(text, Does.Not.Contain(fragment), $"Observer playable scene contract should not contain '{fragment}' in {scenePath}");
        }
    }
}
