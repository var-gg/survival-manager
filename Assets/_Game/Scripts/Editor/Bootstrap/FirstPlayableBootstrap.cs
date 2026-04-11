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
    internal const string CombatSandboxRequestedKey = "SM.CombatSandboxRequested";
    internal const string LegacyQuickBattleRequestedKey = "SM.QuickBattleRequested";
    private const string CombatSandboxInspectorRestoreKey = "SM.CombatSandboxRestoreInspector";
    private const string LegacyQuickBattleInspectorRestoreKey = "SM.QuickBattleRestoreInspector";
    private const string CombatSandboxConfigFolder = "Assets/_Game/Authoring/CombatSandbox";
    internal const string CombatSandboxConfigAssetPath = "Assets/_Game/Authoring/CombatSandbox/combat_sandbox_active.asset";
    private static readonly (string HeroId, SM.Combat.Model.DeploymentAnchorId Anchor)[] DefaultCombatSandboxAllySlots =
    {
        ("hero-1", SM.Combat.Model.DeploymentAnchorId.FrontCenter),
        ("hero-3", SM.Combat.Model.DeploymentAnchorId.FrontBottom),
        ("hero-5", SM.Combat.Model.DeploymentAnchorId.BackTop),
        ("hero-7", SM.Combat.Model.DeploymentAnchorId.BackBottom),
    };

    private static readonly (string ParticipantId, string DisplayName, string CharacterId, SM.Combat.Model.DeploymentAnchorId Anchor)[] DefaultCombatSandboxEnemySlots =
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

            EditorPrefs.SetBool(CombatSandboxInspectorRestoreKey, true);
            Selection.activeObject = quickBattleConfig;
            EditorGUIUtility.PingObject(quickBattleConfig);
            flow.Step("Open Battle scene", () => EditorSceneManager.OpenScene(BattleScenePath, OpenSceneMode.Single));
            EditorPrefs.SetBool(CombatSandboxRequestedKey, true);
            flow.Step("Enter Play Mode", EditorApplication.EnterPlaymode);
        }
        catch (System.Exception ex)
        {
            EditorPrefs.DeleteKey(CombatSandboxRequestedKey);
            EditorPrefs.DeleteKey(LegacyQuickBattleRequestedKey);
            EditorPrefs.DeleteKey(CombatSandboxInspectorRestoreKey);
            EditorPrefs.DeleteKey(LegacyQuickBattleInspectorRestoreKey);
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

    internal static bool TryLoadCombatSandboxConfig(out SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        config = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(CombatSandboxConfigAssetPath);
        if (config != null)
        {
            RepairCombatSandboxConfig(config);
            return true;
        }

        config = EnsureCombatSandboxConfig();
        return config != null;
    }

    internal static SM.Unity.Sandbox.CombatSandboxConfig? EnsureCombatSandboxConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(CombatSandboxConfigAssetPath);
        if (existing != null)
        {
            RepairCombatSandboxConfig(existing);
            return existing;
        }

        if (AssetDatabase.LoadMainAssetAtPath(CombatSandboxConfigAssetPath) != null || File.Exists(CombatSandboxConfigAssetPath))
        {
            Debug.LogWarning(
                $"[CombatSandbox] active handoff를 타입 해석하지 못해 자동 재생성을 중단합니다: {CombatSandboxConfigAssetPath}\n" +
                $"Recovery: {CombatSandboxAuthoringAssetUtility.RecoveryInstructions}");
            return null;
        }

        EnsureFolderPath(CombatSandboxConfigFolder);

        var config = ScriptableObject.CreateInstance<SM.Unity.Sandbox.CombatSandboxConfig>();
        var script = MonoScript.FromScriptableObject(config);
        if (script == null)
        {
            DestroyImmediate(config);
            Debug.LogWarning(
                "[CombatSandbox] CombatSandboxConfig script 등록이 아직 준비되지 않아 active handoff 생성을 중단합니다.\n" +
                $"Recovery: {CombatSandboxAuthoringAssetUtility.RecoveryInstructions}");
            return null;
        }

        ApplyDefaultCombatSandboxConfig(config);
        AssetDatabase.CreateAsset(config, CombatSandboxConfigAssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            CombatSandboxConfigAssetPath,
            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        var reloaded = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(CombatSandboxConfigAssetPath);
        if (reloaded != null)
        {
            RepairCombatSandboxConfig(reloaded);
            Debug.Log($"[CombatSandbox] active handoff 생성: {CombatSandboxConfigAssetPath}");
            return reloaded;
        }

        Debug.LogWarning(
            $"[CombatSandbox] active handoff 생성 후 다시 로드하지 못했습니다: {CombatSandboxConfigAssetPath}\n" +
            $"Recovery: {CombatSandboxAuthoringAssetUtility.RecoveryInstructions}");
        return null;
    }

    private static void RepairCombatSandboxConfig(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        var dirty = false;

        if (string.IsNullOrWhiteSpace(config.Id))
        {
            config.Id = "combat_sandbox_active";
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

        if (config.SceneLayout == null)
        {
            config.SceneLayout = CombatSandboxAuthoringAssetUtility.EnsureDefaultSceneLayoutAsset();
            dirty = true;
        }

        if (config.PreviewSettings == null)
        {
            config.PreviewSettings = CombatSandboxAuthoringAssetUtility.EnsureDefaultPreviewSettingsAsset();
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
            config.AllySlots = BuildDefaultCombatSandboxAllySlots();
            dirty = true;
        }

        if (!HasConfiguredEnemySlots(config))
        {
            config.EnemySlots = BuildDefaultCombatSandboxEnemySlots();
            dirty = true;
        }

        if (!dirty)
        {
            return;
        }

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CombatSandbox] active handoff 보정: {CombatSandboxConfigAssetPath}");
    }

    private static void ApplyDefaultCombatSandboxConfig(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        config.Id = "combat_sandbox_active";
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
        config.SceneLayout = CombatSandboxAuthoringAssetUtility.EnsureDefaultSceneLayoutAsset();
        config.PreviewSettings = CombatSandboxAuthoringAssetUtility.EnsureDefaultPreviewSettingsAsset();
        config.LeftTeam = BuildDefaultDirectLeftTeam();
        config.RightTeam = BuildDefaultDirectRightTeam();
        config.Execution = BuildDefaultExecutionPreset();
        config.Seed = 42;
        config.AllySlots = BuildDefaultCombatSandboxAllySlots();
        config.EnemySlots = BuildDefaultCombatSandboxEnemySlots();
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

        if (!TryLoadCombatSandboxConfig(out quickBattleConfig))
        {
            error =
                $"Combat Sandbox active handoff could not be loaded: {CombatSandboxConfigAssetPath}\n" +
                $"Recovery: {CombatSandboxAuthoringAssetUtility.RecoveryInstructions}";
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
                $"Recovery: {CombatSandboxAuthoringAssetUtility.RecoveryInstructions}\n" +
                "Fallback: Window/SM/Combat Sandbox, SM/Internal/Validation/Validate Canonical Content";
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
            Members = DefaultCombatSandboxAllySlots
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
            Members = DefaultCombatSandboxEnemySlots
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

    private static System.Collections.Generic.List<SM.Unity.Sandbox.CombatSandboxAllySlot> BuildDefaultCombatSandboxAllySlots()
    {
        return DefaultCombatSandboxAllySlots
            .Select(slot => new SM.Unity.Sandbox.CombatSandboxAllySlot
            {
                HeroId = slot.HeroId,
                Anchor = slot.Anchor,
                RoleInstructionIdOverride = string.Empty,
            })
            .ToList();
    }

    private static System.Collections.Generic.List<SM.Unity.Sandbox.CombatSandboxEnemySlot> BuildDefaultCombatSandboxEnemySlots()
    {
        return DefaultCombatSandboxEnemySlots
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

        if (!EditorPrefs.GetBool(CombatSandboxInspectorRestoreKey, false) && !EditorPrefs.GetBool(LegacyQuickBattleInspectorRestoreKey, false))
        {
            return;
        }

        EditorApplication.delayCall += RestoreCombatSandboxInspectorSelection;
    }

    private static void RestoreCombatSandboxInspectorSelection()
    {
        EditorPrefs.DeleteKey(CombatSandboxInspectorRestoreKey);
        EditorPrefs.DeleteKey(LegacyQuickBattleInspectorRestoreKey);

        if (!Application.isPlaying || SceneManager.GetActiveScene().name != SceneNames.Battle)
        {
            return;
        }

        var quickBattleConfig = AssetDatabase.LoadAssetAtPath<SM.Unity.Sandbox.CombatSandboxConfig>(CombatSandboxConfigAssetPath);
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

    private static void EnsureFolderPath(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolderPath(parent);
        }

        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
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
