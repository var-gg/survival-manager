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
    private static readonly (string ParticipantId, string DisplayName, string CharacterId, string RoleInstructionId, SM.Combat.Model.DeploymentAnchorId Anchor)[] DefaultCombatSandboxAllySlots =
    {
        ("ally_warden", "Warden", "warden", "anchor", SM.Combat.Model.DeploymentAnchorId.FrontCenter),
        ("ally_slayer", "Slayer", "slayer", "bruiser", SM.Combat.Model.DeploymentAnchorId.FrontBottom),
        ("ally_hunter", "Hunter", "hunter", "carry", SM.Combat.Model.DeploymentAnchorId.BackTop),
        ("ally_priest", "Priest", "priest", "support", SM.Combat.Model.DeploymentAnchorId.BackBottom),
    };

    private static readonly (string HeroId, SM.Combat.Model.DeploymentAnchorId Anchor)[] DefaultCombatSandboxLegacyAllySlots =
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

    [MenuItem("SM/전투테스트", false, 1)]
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

    [MenuItem("SM/전체테스트", false, 2)]
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

    [MenuItem("SM/극장모드", false, 3)]
    public static void PlayTheaterMode()
    {
        EditorUtility.DisplayDialog("극장모드", "극장모드는 준비 중입니다.", "확인");
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
            Debug.Log($"[CombatSandbox] active handoff 타입 해석 불가 — 삭제 후 재생성합니다: {CombatSandboxConfigAssetPath}");
            AssetDatabase.DeleteAsset(CombatSandboxConfigAssetPath);
            if (File.Exists(CombatSandboxConfigAssetPath))
            {
                File.Delete(CombatSandboxConfigAssetPath);
            }
            var metaPath = CombatSandboxConfigAssetPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        EnsureFolderPath(CombatSandboxConfigFolder);

        var config = ScriptableObject.CreateInstance<SM.Unity.Sandbox.CombatSandboxConfig>();
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
            ConfigureDefaultP09ScenarioMetadata(config);
            dirty = true;
        }
        else if (string.Equals(config.Scenario.ScenarioId, "opening_default_4unit", System.StringComparison.Ordinal)
                 && (config.Scenario.Tags == null || !config.Scenario.Tags.Contains("p09")))
        {
            ConfigureDefaultP09ScenarioMetadata(config);
            dirty = true;
        }

        if (config.LeftTeam.Members.Count == 0 || IsLegacyCurrentProfileLeftTeam(config.LeftTeam))
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
            DisplayName = "P09 Default 4v4",
            Tags = new System.Collections.Generic.List<string> { "starter", "opening", "p09" },
            ExpectedOutcome = "Baseline P09 character-vs-character sandbox lane for daily balance and readability checks.",
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

    private static void ConfigureDefaultP09ScenarioMetadata(SM.Unity.Sandbox.CombatSandboxConfig config)
    {
        config.Scenario.DisplayName = "P09 Default 4v4";
        config.Scenario.Tags = new System.Collections.Generic.List<string> { "starter", "opening", "p09" };
        config.Scenario.ExpectedOutcome = "Baseline P09 character-vs-character sandbox lane for daily balance and readability checks.";
    }

    private static bool TryAutoEnsurePrerequisites(string sceneName, string scenePath, string flowLabel, out string error)
    {
        error = string.Empty;

        if (!File.Exists(scenePath))
        {
            Debug.Log($"[{flowLabel}] {sceneName} 씬이 없어 자동 생성합니다.");
            FirstPlayableSceneInstaller.RepairFirstPlayableScenes();
            if (!File.Exists(scenePath))
            {
                error = $"{sceneName} 씬 자동 생성 실패: {scenePath}";
                return false;
            }
        }

        try
        {
            FirstPlayableContentBootstrap.RequireSampleContentReady(flowLabel);
        }
        catch
        {
            Debug.Log($"[{flowLabel}] 샘플 콘텐츠를 자동 생성합니다.");
            try
            {
                FirstPlayableContentBootstrap.EnsureSampleContent();
                FirstPlayableContentBootstrap.RequireSampleContentReady(flowLabel);
            }
            catch (System.Exception ex)
            {
                error = $"샘플 콘텐츠 자동 생성 실패: {ex.Message}";
                return false;
            }
        }

        if (!FirstPlayableSceneInstaller.TryValidateSavedSceneContract(sceneName, out error))
        {
            Debug.Log($"[{flowLabel}] {sceneName} 씬 구조가 깨져 자동 복구합니다.");
            FirstPlayableSceneInstaller.RepairFirstPlayableScenes();
            if (!FirstPlayableSceneInstaller.TryValidateSavedSceneContract(sceneName, out error))
            {
                error = $"{sceneName} 씬 자동 복구 실패: {error}";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateFullLoopPreflight(out string error)
    {
        return TryAutoEnsurePrerequisites(SceneNames.Boot, BootScenePath, "전체테스트", out error);
    }

    private static bool TryValidateCombatSandboxPreflight(
        out SM.Unity.Sandbox.CombatSandboxConfig quickBattleConfig,
        out string error)
    {
        quickBattleConfig = null!;

        if (!TryAutoEnsurePrerequisites(SceneNames.Battle, BattleScenePath, "전투테스트", out error))
            return false;

        if (!TryLoadCombatSandboxConfig(out quickBattleConfig))
        {
            Debug.Log("[전투테스트] Combat Sandbox 설정 로드 실패 — 스크립트 참조를 복구합니다.");
            CombatSandboxAuthoringAssetUtility.RefreshAuthoringAssets();
            if (!TryLoadCombatSandboxConfig(out quickBattleConfig))
            {
                error =
                    $"Combat Sandbox 설정을 불러올 수 없습니다: {CombatSandboxConfigAssetPath}\n" +
                    "자동 복구에 실패했습니다. Unity를 재시작한 뒤 다시 시도하세요.";
                return false;
            }
        }

        try
        {
            CombatSandboxEditorSession.Shared.BuildCompiledScenario(quickBattleConfig);
        }
        catch (System.Exception ex)
        {
            error = $"시나리오 컴파일 실패: {ex.Message}";
            return false;
        }

        return true;
    }

    private static SM.Unity.Sandbox.CombatSandboxTeamDefinition BuildDefaultDirectLeftTeam()
    {
        return new SM.Unity.Sandbox.CombatSandboxTeamDefinition
        {
            TeamId = "starter.p09_allies",
            DisplayName = "P09 Starter Allies",
            SourceMode = SM.Unity.Sandbox.SandboxLoadoutSourceKind.AuthoredSyntheticTeam,
            TeamPosture = SM.Combat.Model.TeamPostureType.StandardAdvance,
            ProvenanceLabel = "starter.p09_allies",
            Tags = new System.Collections.Generic.List<string> { "starter", "p09", "allies" },
            Members = DefaultCombatSandboxAllySlots
                .Select(slot => new SM.Unity.Sandbox.CombatSandboxTeamMemberDefinition
                {
                    MemberId = slot.ParticipantId,
                    DisplayName = slot.DisplayName,
                    SourceKind = SM.Unity.Sandbox.SandboxUnitSourceKind.Character,
                    CharacterId = slot.CharacterId,
                    Anchor = slot.Anchor,
                    RoleInstructionId = slot.RoleInstructionId,
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

    private static bool IsLegacyCurrentProfileLeftTeam(SM.Unity.Sandbox.CombatSandboxTeamDefinition team)
    {
        if (team.SourceMode == SM.Unity.Sandbox.SandboxLoadoutSourceKind.CurrentLocalProfile)
        {
            return true;
        }

        if (string.Equals(team.TeamId, "starter.current_profile", System.StringComparison.Ordinal)
            || string.Equals(team.ProvenanceLabel, "starter.current_profile", System.StringComparison.Ordinal))
        {
            return true;
        }

        return team.Members.Any(member =>
            member != null
            && (member.SourceKind == SM.Unity.Sandbox.SandboxUnitSourceKind.LocalProfileHero
                || (!string.IsNullOrWhiteSpace(member.MemberId) && member.MemberId.StartsWith("hero-", System.StringComparison.Ordinal))));
    }

    private static System.Collections.Generic.List<SM.Unity.Sandbox.CombatSandboxAllySlot> BuildDefaultCombatSandboxAllySlots()
    {
        return DefaultCombatSandboxLegacyAllySlots
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
