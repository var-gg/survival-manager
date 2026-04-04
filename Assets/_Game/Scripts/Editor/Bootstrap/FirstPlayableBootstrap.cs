using SM.Editor.SeedData;
using SM.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SM.Editor.Bootstrap;

public static class FirstPlayableBootstrap
{
    private const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";
    internal const string QuickBattleRequestedKey = "SM.QuickBattleRequested";
    private const string QuickBattleConfigFolder = "Assets/Resources/_Game/Content/Definitions/QuickBattle";
    private const string QuickBattleConfigAssetPath = "Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset";

    [MenuItem("SM/Quick Battle")]
    public static void QuickBattleOneClick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[QuickBattle] 이미 Play 중입니다.");
            return;
        }

        try
        {
            Debug.Log("[QuickBattle] Step 1/7: Ensure localization foundation");
            LocalizationFoundationBootstrap.EnsureFoundationAssets();

            Debug.Log("[QuickBattle] Step 2/7: Ensure minimum canonical content");
            SampleSeedGenerator.EnsureCanonicalSampleContent();

            Debug.Log("[QuickBattle] Step 3/7: Validate content definitions");
            ContentDefinitionValidator.Validate();

            Debug.Log("[QuickBattle] Step 4/7: Repair first playable scenes");
            FirstPlayableSceneInstaller.RepairFirstPlayableScenes();

            Debug.Log("[QuickBattle] Step 5/7: Ensure Quick Battle config");
            EnsureQuickBattleConfig();

            Debug.Log("[QuickBattle] Step 6/7: Reset local demo save/profile");
            ResetLocalDemoState();

            Debug.Log("[QuickBattle] Step 7/7: Open Boot scene → Enter Play Mode");
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            EditorPrefs.SetBool(QuickBattleRequestedKey, true);
            EditorApplication.EnterPlaymode();
        }
        catch (System.Exception ex)
        {
            EditorPrefs.DeleteKey(QuickBattleRequestedKey);
            Debug.LogError($"[QuickBattle] Failed: {ex.Message}\n{ex}");
            throw;
        }
    }

    [MenuItem("SM/Setup/Prepare Observer Playable")]
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

            Debug.Log("[ObserverPlayable] Step 2/6: Ensure minimum canonical content without rewriting committed authoring");
            SampleSeedGenerator.EnsureCanonicalSampleContent();

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

    private static void EnsureQuickBattleConfig()
    {
        if (AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(QuickBattleConfigAssetPath) != null)
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(QuickBattleConfigFolder))
        {
            var parent = System.IO.Path.GetDirectoryName(QuickBattleConfigFolder)!.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, "QuickBattle");
        }

        var config = ScriptableObject.CreateInstance<SM.Unity.Sandbox.CombatSandboxConfig>();
        config.Id = "quick_battle_default";
        config.DisplayName = "Quick Battle Default";
        config.Seed = 42;
        config.EnemySlots = new System.Collections.Generic.List<SM.Unity.Sandbox.CombatSandboxEnemySlot>
        {
            new() { ParticipantId = "enemy_guardian", DisplayName = "Enemy Guardian", ArchetypeId = "guardian", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontTop },
            new() { ParticipantId = "enemy_raider", DisplayName = "Enemy Raider", ArchetypeId = "raider", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontBottom },
            new() { ParticipantId = "enemy_hunter", DisplayName = "Enemy Hunter", ArchetypeId = "hunter", Anchor = SM.Combat.Model.DeploymentAnchorId.BackTop },
            new() { ParticipantId = "enemy_hexer", DisplayName = "Enemy Hexer", ArchetypeId = "hexer", Anchor = SM.Combat.Model.DeploymentAnchorId.BackBottom },
        };
        AssetDatabase.CreateAsset(config, QuickBattleConfigAssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[QuickBattle] 디폴트 config 생성: {QuickBattleConfigAssetPath}");
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
