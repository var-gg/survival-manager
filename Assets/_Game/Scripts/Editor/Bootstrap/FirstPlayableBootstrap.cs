using SM.Unity;
using SM.Editor.SeedData;
using SM.Editor.Validation;
using SM.Editor.Authoring.CombatSandbox;
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
    internal const string QuickBattleConfigAssetPath = "Assets/Resources/_Game/Content/Definitions/QuickBattle/quick_battle_default.asset";
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

    [MenuItem("SM/Play/Combat Sandbox")]
    public static void PlayCombatSandbox()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CombatSandbox] 이미 Play 중입니다.");
            return;
        }

        try
        {
            using var flow = RuntimeInstrumentation.BeginFlow("CombatSandbox");
            if (!TryValidateCombatSandboxPreflight(out var quickBattleConfig, out var error))
            {
                throw new System.InvalidOperationException(error);
            }

            EditorPrefs.SetBool(QuickBattleInspectorRestoreKey, true);
            Selection.activeObject = quickBattleConfig;
            EditorGUIUtility.PingObject(quickBattleConfig);
            flow.Step("Open Battle scene", () => EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single));
            EditorPrefs.SetBool(QuickBattleRequestedKey, true);
            flow.Step("Enter Play Mode", EditorApplication.EnterPlaymode);
        }
        catch (System.Exception ex)
        {
            EditorPrefs.DeleteKey(QuickBattleRequestedKey);
            EditorPrefs.DeleteKey(QuickBattleInspectorRestoreKey);
            Debug.LogError($"[CombatSandbox] Failed: {ex.Message}\n{ex}");
            throw;
        }
    }

    [MenuItem("SM/Play/Full Loop")]
    public static void PrepareObserverPlayableMenu()
    {
        PrepareObserverPlayable();
    }

    public static void PrepareObserverPlayable()
    {
        try
        {
            using var flow = RuntimeInstrumentation.BeginFlow("FullLoop");
            if (!TryValidateFullLoopPreflight(out var error))
            {
                throw new System.InvalidOperationException(error);
            }

            flow.Step("Open Boot scene", () => EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single));
            Debug.Log("[FullLoop] Success. 이제 Boot scene에서 Play를 누르고 Start Local Run으로 Town 흐름을 확인할 수 있다.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[FullLoop] Failed: {ex.Message}\n{ex}");
            throw;
        }
    }

    public static void PrepareFirstPlayable()
    {
        PrepareObserverPlayable();
    }

    public static void EnsureLocalizationFoundation()
    {
        LocalizationFoundationBootstrap.EnsureFoundationAssets();
    }

    public static void RepairFirstPlayableScenes()
    {
        FirstPlayableSceneInstaller.RepairFirstPlayableScenes();
    }

    [MenuItem("SM/Internal/Validation/Validate Canonical Content")]
    public static void ValidateCanonicalContent()
    {
        ValidateContentDefinitionsForPrototypeEntry("Recovery");
    }

    internal static bool TryLoadQuickBattleConfig(out SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        config = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(QuickBattleConfigAssetPath);
        return config != null;
    }

    internal static SM.Unity.Sandbox.CombatSandboxConfig? EnsureQuickBattleConfig()
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
        Debug.Log($"[CombatSandbox] active handoff 생성: {QuickBattleConfigAssetPath}");
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
            config.DisplayName = "Combat Sandbox Active";
            dirty = true;
        }

        if (!config.UseScenarioAuthoring)
        {
            config.UseScenarioAuthoring = true;
            dirty = true;
        }

        if (config.DefaultLaneKind == SM.Unity.Sandbox.CombatSandboxLaneKind.None)
        {
            config.DefaultLaneKind = SM.Unity.Sandbox.CombatSandboxLaneKind.DirectCombatSandbox;
            dirty = true;
        }

        if (config.Seed == 0)
        {
            config.Seed = 42;
            dirty = true;
        }

        if (string.IsNullOrWhiteSpace(config.Scenario.ScenarioId))
        {
            config.Scenario.ScenarioId = "opening_default_4unit";
            config.Scenario.DisplayName = "Opening Default 4 Unit";
            config.Scenario.Tags = new System.Collections.Generic.List<string> { "starter", "opening" };
            config.Scenario.ExpectedOutcome = "Baseline direct combat sandbox lane for daily balance and readability checks.";
            dirty = true;
        }

        if (config.LeftTeam.Members.Count == 0)
        {
            config.LeftTeam = BuildDefaultDirectLeftTeam();
            dirty = true;
        }

        if (config.RightTeam.Members.Count == 0)
        {
            config.RightTeam = BuildDefaultDirectRightTeam();
            dirty = true;
        }

        if (config.Execution.BatchCount <= 0 || config.Execution.Seed == 0)
        {
            config.Execution = BuildDefaultExecutionPreset();
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
        Debug.Log($"[CombatSandbox] active handoff 보정: {QuickBattleConfigAssetPath}");
    }

    private static void ApplyDefaultQuickBattleConfig(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        config.Id = "quick_battle_default";
        config.DisplayName = "Combat Sandbox Active";
        config.UseScenarioAuthoring = true;
        config.DefaultLaneKind = SM.Unity.Sandbox.CombatSandboxLaneKind.DirectCombatSandbox;
        config.Scenario = new SM.Unity.Sandbox.CombatSandboxScenarioMetadata
        {
            ScenarioId = "opening_default_4unit",
            DisplayName = "Opening Default 4 Unit",
            Tags = new System.Collections.Generic.List<string> { "starter", "opening" },
            ExpectedOutcome = "Baseline direct combat sandbox lane for daily balance and readability checks.",
        };
        config.LeftTeam = BuildDefaultDirectLeftTeam();
        config.RightTeam = BuildDefaultDirectRightTeam();
        config.Execution = BuildDefaultExecutionPreset();
        config.Seed = 42;
        config.AllySlots = BuildDefaultQuickBattleAllySlots();
        config.EnemySlots = BuildDefaultQuickBattleEnemySlots();
    }

    private static bool TryValidateFullLoopPreflight(out string error)
    {
        error = string.Empty;
        if (!File.Exists(BootScenePath))
        {
            error = $"Boot scene is missing: {BootScenePath}";
            return false;
        }

        try
        {
            FirstPlayableContentBootstrap.RequireSampleContentReady(nameof(PrepareObserverPlayable));
        }
        catch (System.Exception ex)
        {
            error = $"{ex.Message}\nRecovery: SM/Internal/Content/Ensure Sample Content";
            return false;
        }

        if (!FirstPlayableSceneInstaller.TryValidateSavedSceneContract(SceneNames.Boot, out error))
        {
            error += "\nRecovery: SM/Internal/Recovery/Repair First Playable Scenes";
            return false;
        }

        return true;
    }

    private static bool TryValidateCombatSandboxPreflight(
        out SM.Unity.Sandbox.CombatSandboxConfig quickBattleConfig,
        out string error)
    {
        quickBattleConfig = null!;
        error = string.Empty;

        if (!File.Exists(BattleScenePath))
        {
            error = $"Battle scene is missing: {BattleScenePath}";
            return false;
        }

        if (!TryLoadQuickBattleConfig(out quickBattleConfig))
        {
            error =
                $"Combat Sandbox active handoff is missing: {QuickBattleConfigAssetPath}\n" +
                "Recovery: Window/SM/Combat Sandbox or SM/Internal/Content/Ensure Sample Content";
            return false;
        }

        try
        {
            FirstPlayableContentBootstrap.RequireSampleContentReady(nameof(PlayCombatSandbox));
        }
        catch (System.Exception ex)
        {
            error = $"{ex.Message}\nRecovery: SM/Internal/Content/Ensure Sample Content";
            return false;
        }

        if (!FirstPlayableSceneInstaller.TryValidateSavedSceneContract(SceneNames.Battle, out error))
        {
            error += "\nRecovery: SM/Internal/Recovery/Repair First Playable Scenes";
            return false;
        }

        try
        {
            CombatSandboxEditorSession.Shared.BuildCompiledScenario(quickBattleConfig);
        }
        catch (System.Exception ex)
        {
            error =
                $"Combat Sandbox preflight compile failed.\n{ex.Message}\n" +
                "Recovery: Window/SM/Combat Sandbox, SM/Internal/Validation/Validate Canonical Content";
            return false;
        }

        return true;
    }

    private static SM.Unity.Sandbox.CombatSandboxTeamDefinition BuildDefaultDirectLeftTeam()
    {
        return new SM.Unity.Sandbox.CombatSandboxTeamDefinition
        {
            TeamId = "starter.current_profile",
            DisplayName = "Current Local Profile",
            SourceMode = SM.Unity.Sandbox.SandboxLoadoutSourceKind.CurrentLocalProfile,
            TeamPosture = SM.Combat.Model.TeamPostureType.StandardAdvance,
            ProvenanceLabel = "starter.current_profile",
            Tags = new System.Collections.Generic.List<string> { "starter", "profile" },
            Members = DefaultQuickBattleAllySlots
                .Select(slot => new SM.Unity.Sandbox.CombatSandboxTeamMemberDefinition
                {
                    MemberId = slot.HeroId,
                    HeroId = slot.HeroId,
                    SourceKind = SM.Unity.Sandbox.SandboxUnitSourceKind.LocalProfileHero,
                    Anchor = slot.Anchor,
                })
                .ToList(),
        };
    }

    private static SM.Unity.Sandbox.CombatSandboxTeamDefinition BuildDefaultDirectRightTeam()
    {
        return new SM.Unity.Sandbox.CombatSandboxTeamDefinition
        {
            TeamId = "starter.observer_smoke",
            DisplayName = "Observer Smoke",
            SourceMode = SM.Unity.Sandbox.SandboxLoadoutSourceKind.AuthoredSyntheticTeam,
            TeamPosture = SM.Combat.Model.TeamPostureType.StandardAdvance,
            ProvenanceLabel = "starter.observer_smoke",
            Tags = new System.Collections.Generic.List<string> { "starter", "observer_smoke" },
            Members = DefaultQuickBattleEnemySlots
                .Select(slot => new SM.Unity.Sandbox.CombatSandboxTeamMemberDefinition
                {
                    MemberId = slot.ParticipantId,
                    DisplayName = slot.DisplayName,
                    SourceKind = SM.Unity.Sandbox.SandboxUnitSourceKind.Character,
                    CharacterId = slot.CharacterId,
                    Anchor = slot.Anchor,
                })
                .ToList(),
        };
    }

    private static SM.Unity.Sandbox.CombatSandboxExecutionSettings BuildDefaultExecutionPreset()
    {
        return new SM.Unity.Sandbox.CombatSandboxExecutionSettings
        {
            PresetId = "starter.fixed_seed",
            DisplayName = "Fixed Seed",
            SeedMode = SM.Unity.Sandbox.SandboxSeedMode.Fixed,
            Seed = 42,
            BatchCount = 1,
            RecordReplay = true,
        };
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
            foreach (var savePath in EnumerateLocalDemoSavePaths())
            {
                if (File.Exists(savePath))
                {
                    File.Delete(savePath);
                    Debug.Log($"[CombatSandbox] Quick Battle smoke save removed: {savePath}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[CombatSandbox] Local demo save reset skipped: {ex.Message}");
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
                $"Prototype entry continues. Strict gating is still available via SM/Internal/Validation/Validate Content Definitions. " +
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
                $"Strict gating is still available via SM/Internal/Validation/Validate Content Definitions. {ex.Message}");
        }
    }

    private static bool IsNonBlockingContentValidationFailure(System.Exception ex)
    {
        return ex.Message.Contains("SM content validation failed", System.StringComparison.Ordinal);
    }

    private static string[] EnumerateLocalDemoSavePaths()
    {
        var profileId = System.Environment.GetEnvironmentVariable("SM_PROFILE_ID") ?? "default";
        var smokeProfileId = $"{profileId}.smoke";
        var configuredSaveDirectory = System.Environment.GetEnvironmentVariable("SM_SAVE_DIR") ?? "Saves";
        var saveDirectory = Path.IsPathRooted(configuredSaveDirectory)
            ? configuredSaveDirectory
            : Path.Combine(Directory.GetCurrentDirectory(), configuredSaveDirectory);
        return new[]
        {
            Path.Combine(saveDirectory, $"{smokeProfileId}.json"),
            Path.Combine(saveDirectory, $"{smokeProfileId}.manifest.json"),
            Path.Combine(saveDirectory, $"{smokeProfileId}.bak.json"),
            Path.Combine(saveDirectory, $"{smokeProfileId}.bak.manifest.json"),
            Path.Combine(saveDirectory, $"{smokeProfileId}.tmp.json"),
            Path.Combine(saveDirectory, $"{smokeProfileId}.tmp.manifest.json"),
        };
    }
}
