using SM.Editor.SeedData;
using SM.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableBootstrap
{
    private const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";

    [MenuItem("SM/Bootstrap/Prepare Observer Playable")]
    public static void PrepareObserverPlayableMenu()
    {
        PrepareObserverPlayable();
    }

    public static void PrepareObserverPlayable()
    {
        try
        {
            Debug.Log("[ObserverPlayable] Step 1/6: Ensure localization foundation");
            LocalizationFoundationBootstrap.EnsureFoundationAssets();

            Debug.Log("[ObserverPlayable] Step 2/6: Ensure sample content");
            SampleSeedGenerator.Generate();

            Debug.Log("[ObserverPlayable] Step 3/6: Validate content definitions");
            ContentDefinitionValidator.Validate();

            Debug.Log("[ObserverPlayable] Step 4/6: Repair first playable scenes");
            FirstPlayableSceneInstaller.RepairFirstPlayableScenes();

            Debug.Log("[ObserverPlayable] Step 5/6: Reset local demo save/profile if present");
            ResetLocalDemoState();

            Debug.Log("[ObserverPlayable] Step 6/6: Open Boot scene");
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            Debug.Log("[ObserverPlayable] Success. 이제 Boot scene에서 Play를 누르면 Boot -> Town 흐름을 확인할 수 있다.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ObserverPlayable] Failed: {ex.Message}\n{ex}");
            throw;
        }
    }

    public static void PrepareFirstPlayable()
    {
        PrepareObserverPlayable();
    }

    private static void ResetLocalDemoState()
    {
        try
        {
            PlayerPrefs.DeleteKey("SM.SaveSlot.default");
            PlayerPrefs.Save();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ObserverPlayable] Local demo save reset skipped: {ex.Message}");
        }
    }
}
