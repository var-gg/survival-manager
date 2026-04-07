using SM.Unity;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;
using UnityEngine.SceneManagement;

namespace SM.Editor.Bootstrap;

[InitializeOnLoad]
public static class FirstPlayableBootstrap
{
    private const string BootScenePath = "Assets/_Game/Scenes/Boot.unity";
    private const string BattleScenePath = "Assets/_Game/Scenes/Battle.unity";
    internal const string QuickBattleRequestedKey = "SM.QuickBattleRequested";
    private const string QuickBattleInspectorRestoreKey = "SM.QuickBattleRestoreInspector";
    private const string QuickBattleConfigFolder = "Assets/Resources/_Game/Content/Definitions/QuickBattle";
    private const string QuickBattleConfigAssetPath = "Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset";
    private static readonly (string HeroId, SM.Combat.Model.DeploymentAnchorId Anchor)[] DefaultQuickBattleAllySlots =
    {
        ("hero-1", SM.Combat.Model.DeploymentAnchorId.FrontCenter),
        ("hero-3", SM.Combat.Model.DeploymentAnchorId.FrontBottom),
        ("hero-5", SM.Combat.Model.DeploymentAnchorId.BackTop),
        ("hero-7", SM.Combat.Model.DeploymentAnchorId.BackBottom),
    };

    private static readonly (string ParticipantId, string DisplayName, string CharacterId, SM.Combat.Model.DeploymentAnchorId Anchor)[] DefaultQuickBattleEnemySlots =
    {
        ("enemy_guardian", "Enemy Guardian", "guardian", SM.Combat.Model.DeploymentAnchorId.FrontTop),
        ("enemy_raider", "Enemy Raider", "raider", SM.Combat.Model.DeploymentAnchorId.FrontBottom),
        ("enemy_hunter", "Enemy Hunter", "hunter", SM.Combat.Model.DeploymentAnchorId.BackTop),
        ("enemy_hexer", "Enemy Hexer", "hexer", SM.Combat.Model.DeploymentAnchorId.BackBottom),
    };

    static FirstPlayableBootstrap()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

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
            using var flow = RuntimeInstrumentation.BeginFlow("QuickBattle");
            flow.Step("Ensure localization foundation", LocalizationFoundationBootstrap.EnsureFoundationAssets);
            flow.Step("Ensure minimum canonical content", () => EnsureCanonicalSampleContentForPrototypeEntry("QuickBattle"));
            flow.Step("Write content validation report (non-blocking)", () => ValidateContentDefinitionsForPrototypeEntry("QuickBattle"));
            flow.Step("Repair first playable scenes", FirstPlayableSceneInstaller.RepairFirstPlayableScenes);
            var quickBattleConfig = flow.Step("Ensure Quick Battle config", EnsureQuickBattleConfig);
            if (quickBattleConfig != null)
            {
                EditorPrefs.SetBool(QuickBattleInspectorRestoreKey, true);
                Selection.activeObject = quickBattleConfig;
                EditorGUIUtility.PingObject(quickBattleConfig);
            }

            flow.Step("Reset local demo save/profile", ResetLocalDemoState);
            flow.Step("Open Battle scene", () => EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single));
            if (quickBattleConfig != null)
            {
                Selection.activeObject = quickBattleConfig;
                EditorGUIUtility.PingObject(quickBattleConfig);
            }

            EditorPrefs.SetBool(QuickBattleRequestedKey, true);
            flow.Step("Enter Play Mode", EditorApplication.EnterPlaymode);
        }
        catch (System.Exception ex)
        {
            EditorPrefs.DeleteKey(QuickBattleRequestedKey);
            EditorPrefs.DeleteKey(QuickBattleInspectorRestoreKey);
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
            using var flow = RuntimeInstrumentation.BeginFlow("PrepareObserverPlayable");
            flow.Step("Ensure localization foundation", LocalizationFoundationBootstrap.EnsureFoundationAssets);
            flow.Step("Ensure minimum canonical content without rewriting committed authoring", () => EnsureCanonicalSampleContentForPrototypeEntry("ObserverPlayable"));
            flow.Step("Write content validation report (non-blocking)", () => ValidateContentDefinitionsForPrototypeEntry("ObserverPlayable"));
            flow.Step("Repair first playable scenes", FirstPlayableSceneInstaller.RepairFirstPlayableScenes);
            flow.Step("Reset local demo save/profile if present", ResetLocalDemoState);
            flow.Step("Open Boot scene", () => EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single));

            Debug.Log("[ObserverPlayable] Success. 이제 Boot scene에서 Play를 누르고 Start Local Run으로 Town 흐름을 확인할 수 있다.");
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
            RepairQuickBattleConfig(existing);
            return existing;
        }

        if (!AssetDatabase.IsValidFolder(QuickBattleConfigFolder))
        {
            var parent = System.IO.Path.GetDirectoryName(QuickBattleConfigFolder)!.Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, "QuickBattle");
        }

        var config = ScriptableObject.CreateInstance<SM.Unity.Sandbox.CombatSandboxConfig>();
        ApplyDefaultQuickBattleConfig(config);
        AssetDatabase.CreateAsset(config, QuickBattleConfigAssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[QuickBattle] 디폴트 config 생성: {QuickBattleConfigAssetPath}");
        return config;
    }

    private static void RepairQuickBattleConfig(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        var dirty = false;

        if (string.IsNullOrWhiteSpace(config.Id))
        {
            config.Id = "quick_battle_default";
            dirty = true;
        }

        if (string.IsNullOrWhiteSpace(config.DisplayName))
        {
            config.DisplayName = "Quick Battle Default";
            dirty = true;
        }

        if (config.Seed == 0)
        {
            config.Seed = 42;
            dirty = true;
        }

        if (!HasConfiguredAllySlots(config))
        {
            config.AllySlots = BuildDefaultQuickBattleAllySlots();
            dirty = true;
        }

        if (!HasConfiguredEnemySlots(config))
        {
            config.EnemySlots = BuildDefaultQuickBattleEnemySlots();
            dirty = true;
        }

        if (!dirty)
        {
            return;
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[QuickBattle] 디폴트 config 보정: {QuickBattleConfigAssetPath}");
    }

    private static void ApplyDefaultQuickBattleConfig(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        config.Id = "quick_battle_default";
        config.DisplayName = "Quick Battle Default";
        config.Seed = 42;
        config.AllySlots = BuildDefaultQuickBattleAllySlots();
        config.EnemySlots = BuildDefaultQuickBattleEnemySlots();
    }

    private static bool HasConfiguredAllySlots(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        return config.AllySlots != null
               && config.AllySlots.Any(slot => slot != null && !string.IsNullOrWhiteSpace(slot.HeroId));
    }

    private static bool HasConfiguredEnemySlots(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        return config.EnemySlots != null
               && config.EnemySlots.Any(slot => slot != null && (!string.IsNullOrWhiteSpace(slot.CharacterId) || !string.IsNullOrWhiteSpace(slot.ArchetypeIdOverride)));
    }

    private static System.Collections.Generic.List<SM.Unity.Sandbox.CombatSandboxAllySlot> BuildDefaultQuickBattleAllySlots()
    {
        return DefaultQuickBattleAllySlots
            .Select(slot => new SM.Unity.Sandbox.CombatSandboxAllySlot
            {
                HeroId = slot.HeroId,
                Anchor = slot.Anchor,
                RoleInstructionIdOverride = string.Empty,
            })
            .ToList();
    }

    private static System.Collections.Generic.List<SM.Unity.Sandbox.CombatSandboxEnemySlot> BuildDefaultQuickBattleEnemySlots()
    {
        return DefaultQuickBattleEnemySlots
            .Select(slot => new SM.Unity.Sandbox.CombatSandboxEnemySlot
            {
                ParticipantId = slot.ParticipantId,
                DisplayName = slot.DisplayName,
                CharacterId = slot.CharacterId,
                ArchetypeIdOverride = string.Empty,
                Anchor = slot.Anchor,
                RoleInstructionId = string.Empty,
            })
            .ToList();
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

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
        {
            return;
        }

        if (!EditorPrefs.GetBool(QuickBattleInspectorRestoreKey, false))
        {
            return;
        }

        EditorApplication.delayCall += RestoreQuickBattleInspectorSelection;
    }

    private static void RestoreQuickBattleInspectorSelection()
    {
        EditorPrefs.DeleteKey(QuickBattleInspectorRestoreKey);

        if (!Application.isPlaying || SceneManager.GetActiveScene().name != SceneNames.Battle)
        {
            return;
        }

        var quickBattleConfig = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(QuickBattleConfigAssetPath);
        if (quickBattleConfig != null)
        {
            Selection.activeObject = quickBattleConfig;
            EditorGUIUtility.PingObject(quickBattleConfig);
            return;
        }

        var battleController = Object.FindFirstObjectByType<BattleScreenController>();
        if (battleController != null)
        {
            Selection.activeGameObject = battleController.gameObject;
            EditorGUIUtility.PingObject(battleController.gameObject);
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

    private static void EnsureCanonicalSampleContentForPrototypeEntry(string flowLabel)
    {
        try
        {
            SampleSeedGenerator.EnsureCanonicalSampleContent();
        }
        catch (System.Exception ex) when (IsNonBlockingContentValidationFailure(ex))
        {
            Debug.LogWarning(
                $"[{flowLabel}] Canonical sample content ensure reported validation errors but prototype entry continues. " +
                $"Strict gating is still available via SM/Validation/Validate Content Definitions. {ex.Message}");
        }
    }

    private static bool IsNonBlockingContentValidationFailure(System.Exception ex)
    {
        return ex.Message.Contains("SM content validation failed", System.StringComparison.Ordinal);
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
