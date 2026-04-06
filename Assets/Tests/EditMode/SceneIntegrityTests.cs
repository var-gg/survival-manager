using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.Editor.Bootstrap;
using SM.Editor.SeedData;
using SM.Content.Definitions;
using SM.Unity;
using SM.Unity.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;

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
    [TestCase("Assets/_Game/UI/Screens/Town/TownScreen.uxml")]
    [TestCase("Assets/_Game/UI/Screens/Town/TownScreen.uss")]
    [TestCase("Assets/_Game/UI/Screens/Expedition/ExpeditionScreen.uxml")]
    [TestCase("Assets/_Game/UI/Screens/Expedition/ExpeditionScreen.uss")]
    [TestCase("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml")]
    [TestCase("Assets/_Game/UI/Screens/Battle/BattleScreen.uss")]
    [TestCase("Assets/_Game/UI/Screens/Reward/RewardScreen.uxml")]
    [TestCase("Assets/_Game/UI/Screens/Reward/RewardScreen.uss")]
    public void PlayableSceneAndScreenAssets_Exist(string assetPath)
    {
        Assert.That(File.Exists(assetPath), Is.True, $"Playable asset is missing: {assetPath}");
    }

    [Test]
    public void BootScene_Saves_GameBootstrap_Camera_And_Canvas()
    {
        var scene = OpenScene("Boot");
        AssertComponent<GameBootstrap>(scene, "GameBootstrap");
        AssertComponent<Canvas>(scene, "BootCanvas");
        AssertComponent<Camera>(scene, "Main Camera");
    }

    [Test]
    public void TownScene_Saves_RuntimePanelHost_And_Controller()
    {
        var scene = OpenScene("Town");
        AssertComponent<RuntimePanelHost>(scene, "TownRuntimePanelHost");
        AssertComponent<UIDocument>(scene, "TownRuntimePanelHost");
        AssertComponent<TownScreenController>(scene, "TownScreenController");
        AssertInputSystemEventSystem(scene);
        AssertSceneDoesNotContainComponent<StandaloneInputModule>(scene);
    }

    [Test]
    public void ExpeditionScene_Saves_RuntimePanelHost_And_Controller()
    {
        var scene = OpenScene("Expedition");
        AssertComponent<RuntimePanelHost>(scene, "ExpeditionRuntimePanelHost");
        AssertComponent<UIDocument>(scene, "ExpeditionRuntimePanelHost");
        AssertComponent<ExpeditionScreenController>(scene, "ExpeditionScreenController");
        AssertInputSystemEventSystem(scene);
        AssertSceneDoesNotContainComponent<StandaloneInputModule>(scene);
    }

    [TestCase("Town", "TownRuntimePanelHost")]
    [TestCase("Expedition", "ExpeditionRuntimePanelHost")]
    public void EditModeSceneBinding_DoesNotCreate_GameSessionRoot(string sceneName, string hostObjectName)
    {
        var scene = OpenScene(sceneName);

        Assert.That(FindGameObject(scene, "GameSessionRoot"), Is.Null, $"Unexpected GameSessionRoot already exists in {scene.path}");

        FirstPlayableRuntimeSceneBinder.EnsureSceneBindings(scene);

        AssertComponent<RuntimePanelHost>(scene, hostObjectName);
        Assert.That(FindGameObject(scene, "GameSessionRoot"), Is.Null, $"Edit-mode scene binding should not create GameSessionRoot in {scene.path}");
    }

    [Test]
    public void BattleScene_Saves_RuntimePanelHost_Presentation_And_ActorOverlay()
    {
        var scene = OpenScene("Battle");
        AssertComponent<RuntimePanelHost>(scene, "BattleRuntimePanelHost");
        AssertComponent<UIDocument>(scene, "BattleRuntimePanelHost");
        AssertComponent<BattleScreenController>(scene, "BattleScreenController");
        AssertComponent<BattlePresentationController>(scene, "BattlePresentationRoot");
        AssertComponent<BattleCameraController>(scene, "BattleCameraRoot");
        AssertComponent<Canvas>(scene, "ActorOverlayCanvas");
        AssertGameObject(scene, "BattleStageRoot");
        AssertGameObject(scene, "ActorOverlayRoot");
        AssertInputSystemEventSystem(scene);
        AssertSceneDoesNotContainComponent<StandaloneInputModule>(scene);
    }

    [Test]
    public void RewardScene_Saves_RuntimePanelHost_And_Controller()
    {
        var scene = OpenScene("Reward");
        AssertComponent<RuntimePanelHost>(scene, "RewardRuntimePanelHost");
        AssertComponent<UIDocument>(scene, "RewardRuntimePanelHost");
        AssertComponent<RewardScreenController>(scene, "RewardScreenController");
        AssertInputSystemEventSystem(scene);
        AssertSceneDoesNotContainComponent<StandaloneInputModule>(scene);
    }

    [Test]
    public void TownScreenUxml_Declares_QuickBattle_And_Deployment_Controls()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Town/TownScreen.uxml");
        Assert.That(uxml, Does.Contain("QuickBattleButton"));
        Assert.That(uxml, Does.Contain("DeployButton_FrontTop"));
        Assert.That(uxml, Does.Contain("TeamPostureButton"));
    }

    [Test]
    public void BattleScreenUxml_Declares_Settings_And_Scrubber_Controls()
    {
        var uxml = File.ReadAllText("Assets/_Game/UI/Screens/Battle/BattleScreen.uxml");
        Assert.That(uxml, Does.Contain("SettingsButton"));
        Assert.That(uxml, Does.Contain("SettingsPanel"));
        Assert.That(uxml, Does.Contain("ProgressFill"));
        Assert.That(uxml, Does.Contain("PauseButton"));
    }

    [Test]
    public void RuntimePanelTheme_DoesNotUse_Unsupported_Gap_Property()
    {
        var uss = File.ReadAllText("Assets/_Game/UI/Foundation/Styles/RuntimePanelTheme.uss");
        Assert.That(uss, Does.Not.Contain("gap:"), "Runtime UITK theme should avoid 'gap' because Unity runtime on this project emits Unknown style property warnings.");
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

    private static Scene OpenScene(string sceneName)
    {
        var path = $"Assets/_Game/Scenes/{sceneName}.unity";
        Assert.That(File.Exists(path), Is.True, $"Playable scene asset is missing: {path}");
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    private static GameObject AssertGameObject(Scene scene, string objectName)
    {
        var go = FindGameObject(scene, objectName);
        Assert.That(go, Is.Not.Null, $"Missing '{objectName}' in {scene.path}");
        return go!;
    }

    private static T AssertComponent<T>(Scene scene, string objectName) where T : Component
    {
        var go = AssertGameObject(scene, objectName);
        var component = go.GetComponent<T>();
        Assert.That(component, Is.Not.Null, $"Missing component '{typeof(T).Name}' on '{objectName}' in {scene.path}");
        return component!;
    }

    private static void AssertInputSystemEventSystem(Scene scene)
    {
        var eventSystem = AssertComponent<EventSystem>(scene, "EventSystem");
        var inputModule = eventSystem.GetComponent("InputSystemUIInputModule");
        Assert.That(inputModule, Is.Not.Null, $"Missing component 'InputSystemUIInputModule' on 'EventSystem' in {scene.path}");
    }

    private static void AssertSceneDoesNotContainComponent<T>(Scene scene) where T : Component
    {
        var found = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .FirstOrDefault();
        Assert.That(found, Is.Null, $"Scene should not contain '{typeof(T).Name}' in {scene.path}");
    }

    private static GameObject? FindGameObject(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }
}
