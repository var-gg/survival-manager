using SM.Editor.SeedData;
using SM.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;

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

            Debug.Log("[QuickBattle] Step 3/7: Write content validation report (non-blocking)");
            ValidateContentDefinitionsForPrototypeEntry("QuickBattle");

            Debug.Log("[QuickBattle] Step 4/7: Repair first playable scenes");
            FirstPlayableSceneInstaller.RepairFirstPlayableScenes();

            Debug.Log("[QuickBattle] Step 5/7: Ensure Quick Battle config");
            var quickBattleConfig = EnsureQuickBattleConfig();
            if (quickBattleConfig != null)
            {
                Selection.activeObject = quickBattleConfig;
                EditorGUIUtility.PingObject(quickBattleConfig);
            }

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

            Debug.Log("[ObserverPlayable] Step 3/6: Write content validation report (non-blocking)");
            ValidateContentDefinitionsForPrototypeEntry("ObserverPlayable");

            Debug.Log("[ObserverPlayable] Step 4/6: Repair first playable scenes");
            FirstPlayableSceneInstaller.RepairFirstPlayableScenes();

            Debug.Log("[ObserverPlayable] Step 5/6: Reset local demo save/profile if present");
            ResetLocalDemoState();

            Debug.Log("[ObserverPlayable] Step 6/6: Open Boot scene");
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            Debug.Log("[ObserverPlayable] Success. 이제 Boot scene에서 Play를 누르면 Session Realm 선택 화면이 열리고, OfflineLocal 선택 후 Town 흐름을 확인할 수 있다.");
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

    private static SM.Unity.Sandbox.CombatSandboxConfig? EnsureQuickBattleConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(QuickBattleConfigAssetPath);
        if (existing != null)
        {
            return existing;
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
        return config;
    }

    private static void ResetLocalDemoState()
    {
        try
        {
            PlayerPrefs.DeleteKey("SM.SaveSlot.default");
            PlayerPrefs.Save();

            foreach (var savePath in EnumerateLocalDemoSavePaths())
            {
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    Debug.Log($"[ObserverPlayable] Local demo save removed: {savePath}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ObserverPlayable] Local demo save reset skipped: {ex.Message}");
        }
    }

    private static void ValidateContentDefinitionsForPrototypeEntry(string flowLabel)
    {
        var report = ContentDefinitionValidator.ValidateAndWriteReport();
        var errorCount = report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Error);
        var warningCount = report.Issues.Count(issue => issue.Severity == ContentValidationSeverity.Warning);

        if (errorCount > 0)
        {
            Debug.LogWarning(
                $"[{flowLabel}] Content validation reported {errorCount} error(s) and {warningCount} warning(s). " +
                $"Prototype entry continues. Strict gating is still available via SM/Validation/Validate Content Definitions. " +
                $"Report: {report.JsonReportPath}");
            return;
        }

        if (warningCount > 0)
        {
            Debug.LogWarning($"[{flowLabel}] Content validation reported {warningCount} warning(s). Report: {report.JsonReportPath}");
            return;
        }

        Debug.Log($"[{flowLabel}] Content validation passed. Report: {report.JsonReportPath}");
    }

    private static string[] EnumerateLocalDemoSavePaths()
    {
        var profileId = System.Environment.GetEnvironmentVariable("SM_PROFILE_ID") ?? "default";
        var configuredSaveDirectory = System.Environment.GetEnvironmentVariable("SM_SAVE_DIR") ?? "Saves";
        var saveDirectory = Path.IsPathRooted(configuredSaveDirectory)
            ? configuredSaveDirectory
            : Path.Combine(Directory.GetCurrentDirectory(), configuredSaveDirectory);
        return new[] { Path.Combine(saveDirectory, $"{profileId}.json") };
    }
}
