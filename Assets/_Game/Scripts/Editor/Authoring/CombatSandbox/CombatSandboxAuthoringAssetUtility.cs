using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Editor.Bootstrap;
using SM.Unity.Sandbox;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Authoring.CombatSandbox;

public static class CombatSandboxAuthoringAssetUtility
{
    public const string AuthoringMenuPath = "SM/Authoring/Combat Sandbox";
    public const string OpenActiveConfigMenuPath = "Window/SM/Combat Sandbox Active Config";
    public const string RecoveryMenuPath = "SM/Internal/Recovery/Refresh Combat Sandbox Authoring Assets";

    private const string RootFolder = "Assets/_Game/Authoring/CombatSandbox";
    private const string BuildOverrideFolder = RootFolder + "/BuildOverrides";
    private const string TeamPresetFolder = RootFolder + "/TeamPresets";
    private const string ExecutionPresetFolder = RootFolder + "/ExecutionPresets";
    private const string ScenarioFolder = RootFolder + "/Scenarios";
    private const string LayoutFolder = RootFolder + "/Layouts";
    private const string PreviewSettingsFolder = RootFolder + "/PreviewSettings";
    private const string DefaultSceneLayoutPath = LayoutFolder + "/combat_sandbox_layout_default.asset";
    private const string DefaultPreviewSettingsPath = PreviewSettingsFolder + "/combat_sandbox_preview_settings_default.asset";
    private static readonly string[] RecoveryImportPaths =
    {
        "Assets/_Game/Scripts/Runtime/Unity/SM.Unity.asmdef",
        "Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxConfig.cs",
        "Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxSceneLayoutAsset.cs",
        "Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxPreviewSettingsAsset.cs",
        "Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxAuthoringModels.cs",
        "Assets/_Game/Scripts/Runtime/Unity/Sandbox/CombatSandboxAssetTypes.cs",
        "Assets/_Game/Scripts/Editor/Authoring/CombatSandbox",
        RootFolder,
        "Assets/Resources/_Game/Content/Definitions/QuickBattle/combat_sandbox_active.asset",
    };

    public static string RecoveryInstructions =>
        $"{RecoveryMenuPath} 실행 후 asset을 다시 선택한다. 계속 `None (Mono Script)`면 Unity를 한 번 재시작한다.";

    [MenuItem(AuthoringMenuPath)]
    [MenuItem(OpenActiveConfigMenuPath)]
    public static void OpenActiveConfig()
    {
        var config = EnsureActiveConfig();
        if (config == null)
        {
            Debug.LogWarning(
                "[CombatSandbox] active config를 열 수 없습니다.\n" +
                $"Recovery: {RecoveryInstructions}");
            return;
        }

        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
        EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        EditorUtility.FocusProjectWindow();
    }

    [MenuItem(RecoveryMenuPath)]
    public static void RefreshAuthoringAssets()
    {
        AssetDatabase.SaveAssets();

        foreach (var path in RecoveryImportPaths)
        {
            ImportAssetIfPresent(path);
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            "[CombatSandbox] sandbox authoring asset/script refresh 완료. " +
            "Inspector가 계속 None (Mono Script)이면 Unity를 재시작한 뒤 asset을 다시 선택하세요.");
    }

    public static CombatSandboxConfig? EnsureActiveConfig()
    {
        var config = FirstPlayableBootstrap.EnsureCombatSandboxConfig();
        if (config != null)
        {
            EnsureSharedAssets(config);
        }

        return config;
    }

    public static void EnsureStarterLibrary()
    {
        EnsureFolder("Assets/_Game", "Authoring");
        EnsureFolder("Assets/_Game/Authoring", "CombatSandbox");
        EnsureFolder(RootFolder, "BuildOverrides");
        EnsureFolder(RootFolder, "TeamPresets");
        EnsureFolder(RootFolder, "ExecutionPresets");
        EnsureFolder(RootFolder, "Scenarios");
        EnsureFolder(RootFolder, "Layouts");
        EnsureFolder(RootFolder, "PreviewSettings");
        var defaultLayout = EnsureDefaultSceneLayoutAsset();
        var defaultPreviewSettings = EnsureDefaultPreviewSettingsAsset();

        var glassCannon = GetOrCreateAsset<UnitBuildOverrideAsset>(
            BuildOverrideFolder + "/unit_build_override_glass_cannon.asset",
            asset =>
            {
                asset.Data.OverrideId = "glass_cannon";
                asset.Data.DisplayName = "Glass Cannon";
                asset.Data.Tags = new List<string> { "endgame", "burst" };
                asset.Data.PassiveBoardId = "board_ranger";
                asset.Data.PassiveNodeIds = new List<string> { "passive_ranger_small_01", "passive_ranger_notable_01" };
                asset.Data.TemporaryAugmentIds = new List<string> { "augment_silver_hunt" };
                asset.Data.PermanentAugmentIds = new List<string> { "augment_perm_legacy_fang" };
                asset.Data.EquippedItems = new List<CombatSandboxItemOverrideData>
                {
                    new() { ItemId = "item_marksman_bow", AffixIds = new List<string> { "affix_farshot" } },
                    new() { ItemId = "item_marksman_armor", AffixIds = new List<string> { "affix_watchful" } },
                    new() { ItemId = "item_marksman_trinket", AffixIds = new List<string> { "affix_quick" } },
                };
                asset.Data.Notes = "High-pressure ranged carry baseline.";
            });

        var fortress = GetOrCreateAsset<UnitBuildOverrideAsset>(
            BuildOverrideFolder + "/unit_build_override_fortress.asset",
            asset =>
            {
                asset.Data.OverrideId = "fortress";
                asset.Data.DisplayName = "Fortress";
                asset.Data.Tags = new List<string> { "endgame", "tank" };
                asset.Data.PassiveBoardId = "board_vanguard";
                asset.Data.PassiveNodeIds = new List<string> { "passive_vanguard_small_01", "passive_vanguard_notable_01" };
                asset.Data.TemporaryAugmentIds = new List<string> { "augment_silver_guard" };
                asset.Data.PermanentAugmentIds = new List<string> { "augment_perm_legacy_oath" };
                asset.Data.EquippedItems = new List<CombatSandboxItemOverrideData>
                {
                    new() { ItemId = "item_guardian_shield", AffixIds = new List<string> { "affix_guarded" } },
                    new() { ItemId = "item_warden_armor", AffixIds = new List<string> { "affix_bracing" } },
                    new() { ItemId = "item_warden_trinket", AffixIds = new List<string> { "affix_hallowed" } },
                };
                asset.Data.Notes = "Frontline stability and anti-burst regression anchor.";
            });

        _ = GetOrCreateAsset<TeamLoadoutPresetAsset>(
            TeamPresetFolder + "/team_loadout_current_profile.asset",
            asset =>
            {
                asset.PresetId = "current_profile";
                asset.DisplayName = "Current Local Profile";
                asset.IsFavorite = true;
                asset.Source.SourceMode = SandboxLoadoutSourceKind.CurrentLocalProfile;
                asset.Source.TeamPosture = SM.Combat.Model.TeamPostureType.StandardAdvance;
                asset.Source.Tags = new List<string> { "starter", "profile" };
                asset.Source.Members = new List<CombatSandboxPresetMemberSpec>
                {
                    new() { MemberId = "hero-1", HeroId = "hero-1", SourceKind = SandboxUnitSourceKind.LocalProfileHero, Anchor = SM.Combat.Model.DeploymentAnchorId.FrontCenter },
                    new() { MemberId = "hero-3", HeroId = "hero-3", SourceKind = SandboxUnitSourceKind.LocalProfileHero, Anchor = SM.Combat.Model.DeploymentAnchorId.FrontBottom },
                    new() { MemberId = "hero-5", HeroId = "hero-5", SourceKind = SandboxUnitSourceKind.LocalProfileHero, Anchor = SM.Combat.Model.DeploymentAnchorId.BackTop },
                    new() { MemberId = "hero-7", HeroId = "hero-7", SourceKind = SandboxUnitSourceKind.LocalProfileHero, Anchor = SM.Combat.Model.DeploymentAnchorId.BackBottom },
                };
                asset.Source.Notes = "Uses the current local profile and deployment as the sandbox left side.";
            });

        var p09StarterAllies = GetOrCreateAsset<TeamLoadoutPresetAsset>(
            TeamPresetFolder + "/team_loadout_p09_starter_allies.asset",
            asset =>
            {
                asset.PresetId = "p09_starter_allies";
                asset.DisplayName = "P09 Starter Allies";
                asset.IsFavorite = true;
                asset.Source.SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam;
                asset.Source.TeamPosture = SM.Combat.Model.TeamPostureType.StandardAdvance;
                asset.Source.Tags = new List<string> { "starter", "p09", "allies" };
                asset.Source.Members = new List<CombatSandboxPresetMemberSpec>
                {
                    new() { MemberId = "ally_dawn_priest", DisplayName = "단린 (丹麟) / Dawn Priest", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "priest", CharacterId = "hero_dawn_priest", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontCenter, RoleInstructionId = "support" },
                    new() { MemberId = "ally_pack_raider", DisplayName = "이빨바람 / Pack Raider", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "raider", CharacterId = "hero_pack_raider", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontBottom, RoleInstructionId = "bruiser" },
                    new() { MemberId = "ally_echo_savant", DisplayName = "공한 (空閑) / Echo Savant", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "marksman", CharacterId = "hero_echo_savant", Anchor = SM.Combat.Model.DeploymentAnchorId.BackTop, RoleInstructionId = "carry" },
                    new() { MemberId = "ally_grave_hexer", DisplayName = "묵향 (墨香) / Grave Hexer", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "hexer", CharacterId = "hero_grave_hexer", Anchor = SM.Combat.Model.DeploymentAnchorId.BackBottom, RoleInstructionId = "support" },
                };
                asset.Source.Notes = "Uses authored P09 character IDs so appearance presets are visible in the combat sandbox.";
            });

        var observerSmoke = GetOrCreateAsset<TeamLoadoutPresetAsset>(
            TeamPresetFolder + "/team_loadout_observer_smoke.asset",
            asset =>
            {
                asset.PresetId = "observer_smoke";
                asset.DisplayName = "Observer Smoke";
                asset.Source.SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam;
                asset.Source.TeamPosture = SM.Combat.Model.TeamPostureType.StandardAdvance;
                asset.Source.Tags = new List<string> { "starter", "observer_smoke" };
                asset.Source.Members = new List<CombatSandboxPresetMemberSpec>
                {
                    new() { MemberId = "enemy_grey_fang", DisplayName = "회조 (灰爪) / Grey Fang", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "reaver", CharacterId = "npc_grey_fang", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontTop },
                    new() { MemberId = "enemy_silent_moon", DisplayName = "침월 (沉月) / Silent Moon", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "hexer", CharacterId = "npc_silent_moon", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontBottom },
                    new() { MemberId = "enemy_lyra_sternfeld", DisplayName = "선영 (宣英) / Lyra Sternfeld", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "priest", CharacterId = "npc_lyra_sternfeld", Anchor = SM.Combat.Model.DeploymentAnchorId.BackTop },
                    new() { MemberId = "enemy_black_vellum", DisplayName = "흑지 (黑紙) / Black Vellum", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "shaman", CharacterId = "npc_black_vellum", Anchor = SM.Combat.Model.DeploymentAnchorId.BackBottom },
                };
                asset.Source.Notes = "Baseline enemy lane shared by the starter scenarios.";
            });

        var endgameGlassCannon = GetOrCreateAsset<TeamLoadoutPresetAsset>(
            TeamPresetFolder + "/team_loadout_endgame_glass_cannon.asset",
            asset =>
            {
                asset.PresetId = "endgame_glass_cannon";
                asset.DisplayName = "Endgame Glass Cannon";
                asset.IsFavorite = true;
                asset.Source.SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam;
                asset.Source.TeamPosture = SM.Combat.Model.TeamPostureType.CollapseWeakSide;
                asset.Source.TeamTacticId = "team_tactic_collapse_weak_side";
                asset.Source.Tags = new List<string> { "endgame", "burst" };
                asset.Source.Members = new List<CombatSandboxPresetMemberSpec>
                {
                    new() { MemberId = "glass.anchor", DisplayName = "Bulwark", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "bulwark", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontCenter, RoleInstructionId = "anchor", BuildOverride = fortress },
                    new() { MemberId = "glass.raider", DisplayName = "Raider", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "raider", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontBottom, RoleInstructionId = "bruiser" },
                    new() { MemberId = "glass.carry", DisplayName = "Marksman", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "marksman", Anchor = SM.Combat.Model.DeploymentAnchorId.BackTop, RoleInstructionId = "carry", BuildOverride = glassCannon },
                    new() { MemberId = "glass.support", DisplayName = "Priest", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "priest", Anchor = SM.Combat.Model.DeploymentAnchorId.BackBottom, RoleInstructionId = "support" },
                };
                asset.Source.Notes = "Burst-heavy authored team for readability and anti-burst checks.";
            });

        var endgameFortress = GetOrCreateAsset<TeamLoadoutPresetAsset>(
            TeamPresetFolder + "/team_loadout_endgame_fortress.asset",
            asset =>
            {
                asset.PresetId = "endgame_fortress";
                asset.DisplayName = "Endgame Fortress";
                asset.Source.SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam;
                asset.Source.TeamPosture = SM.Combat.Model.TeamPostureType.HoldLine;
                asset.Source.TeamTacticId = "team_tactic_hold_line";
                asset.Source.Tags = new List<string> { "endgame", "tank" };
                asset.Source.Members = new List<CombatSandboxPresetMemberSpec>
                {
                    new() { MemberId = "fortress.warden", DisplayName = "Warden", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "warden", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontCenter, RoleInstructionId = "anchor", BuildOverride = fortress },
                    new() { MemberId = "fortress.bulwark", DisplayName = "Bulwark", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "bulwark", Anchor = SM.Combat.Model.DeploymentAnchorId.FrontTop, RoleInstructionId = "anchor" },
                    new() { MemberId = "fortress.shaman", DisplayName = "Shaman", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "shaman", Anchor = SM.Combat.Model.DeploymentAnchorId.BackCenter, RoleInstructionId = "support" },
                    new() { MemberId = "fortress.priest", DisplayName = "Priest", SourceKind = SandboxUnitSourceKind.Archetype, ArchetypeId = "priest", Anchor = SM.Combat.Model.DeploymentAnchorId.BackBottom, RoleInstructionId = "support" },
                };
                asset.Source.Notes = "Sustain shell preset for fortress and anti-burst validation.";
            });

        var fixedSeed = GetOrCreateAsset<CombatSandboxExecutionPreset>(
            ExecutionPresetFolder + "/combat_sandbox_execution_fixed_seed.asset",
            asset =>
            {
                asset.Settings.PresetId = "fixed_seed";
                asset.Settings.DisplayName = "Fixed Seed";
                asset.Settings.SeedMode = SandboxSeedMode.Fixed;
                asset.Settings.Seed = 42;
                asset.Settings.BatchCount = 1;
                asset.Settings.RecordReplay = true;
            });

        var regressionBatch = GetOrCreateAsset<CombatSandboxExecutionPreset>(
            ExecutionPresetFolder + "/combat_sandbox_execution_regression_batch.asset",
            asset =>
            {
                asset.Settings.PresetId = "regression_batch";
                asset.Settings.DisplayName = "Regression Batch";
                asset.Settings.SeedMode = SandboxSeedMode.Batch;
                asset.Settings.Seed = 101;
                asset.Settings.BatchCount = 8;
                asset.Settings.RunSideSwap = true;
                asset.Settings.RecordReplay = true;
                asset.Settings.StopOnReadabilityViolation = true;
            });

        CreateScenario("opening_default_4unit", "P09 Default 4v4", p09StarterAllies, observerSmoke, fixedSeed, true, "starter", "opening", "p09");
        CreateScenario("endgame_glass_cannon", "Endgame Glass Cannon", endgameGlassCannon, observerSmoke, regressionBatch, true, "endgame", "burst");
        CreateScenario("endgame_fortress", "Endgame Fortress", endgameFortress, observerSmoke, fixedSeed, false, "endgame", "tank");
        CreateScenario("anti_burst_regression", "Anti Burst Regression", endgameFortress, endgameGlassCannon, regressionBatch, true, "regression", "counter", "anti_burst");
        var activeConfig = FirstPlayableBootstrap.EnsureCombatSandboxConfig();
        if (activeConfig != null)
        {
            if (activeConfig.SceneLayout == null)
            {
                activeConfig.SceneLayout = defaultLayout;
            }

            if (activeConfig.PreviewSettings == null)
            {
                activeConfig.PreviewSettings = defaultPreviewSettings;
            }

            EditorUtility.SetDirty(activeConfig);
        }

        AssetDatabase.SaveAssets();
    }

    public static IReadOnlyList<CombatSandboxScenarioAsset> FindScenarioAssets()
    {
        EnsureStarterLibrary();
        return AssetDatabase.FindAssets("t:CombatSandboxScenarioAsset", new[] { ScenarioFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CombatSandboxScenarioAsset>)
            .Where(asset => asset != null)
            .OrderBy(asset => asset.DisplayName, StringComparer.Ordinal)
            .ToList();
    }

    public static bool TryPushScenarioToActiveConfig(
        CombatSandboxScenarioAsset scenario,
        CombatSandboxConfig activeConfig,
        out string message)
    {
        message = string.Empty;
        if (scenario == null || activeConfig == null)
        {
            message = "Scenario or active config is missing.";
            return false;
        }

        var leftTeamBuilt = TryBuildTeamDefinition(scenario.LeftTeam, out var leftTeam, out var leftWarning);
        var rightTeamBuilt = TryBuildTeamDefinition(scenario.RightTeam, out var rightTeam, out var rightWarning);
        if (!leftTeamBuilt || !rightTeamBuilt)
        {
            message = string.Join("\n", new[] { leftWarning, rightWarning }.Where(text => !string.IsNullOrWhiteSpace(text)));
            return false;
        }

        activeConfig.UseScenarioAuthoring = true;
        activeConfig.DefaultLaneKind = CombatSandboxLaneKind.DirectCombatSandbox;
        activeConfig.Id = string.IsNullOrWhiteSpace(activeConfig.Id) ? "combat_sandbox_active" : activeConfig.Id;
        activeConfig.DisplayName = string.IsNullOrWhiteSpace(activeConfig.DisplayName) ? "Combat Sandbox Active" : activeConfig.DisplayName;
        activeConfig.Scenario = new CombatSandboxScenarioMetadata
        {
            ScenarioId = scenario.ScenarioId,
            DisplayName = scenario.DisplayName,
            Tags = scenario.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            Notes = scenario.Notes,
            ExpectedOutcome = scenario.ExpectedOutcome,
        };
        activeConfig.LeftTeam = leftTeam;
        activeConfig.RightTeam = rightTeam;
        activeConfig.Execution = scenario.ExecutionPreset != null
            ? new CombatSandboxExecutionSettings
            {
                PresetId = scenario.ExecutionPreset.Settings.PresetId,
                DisplayName = scenario.ExecutionPreset.Settings.DisplayName,
                SeedMode = scenario.ExecutionPreset.Settings.SeedMode,
                Seed = scenario.ExecutionPreset.Settings.Seed,
                BatchCount = scenario.ExecutionPreset.Settings.BatchCount,
                RunSideSwap = scenario.ExecutionPreset.Settings.RunSideSwap,
                RecordReplay = scenario.ExecutionPreset.Settings.RecordReplay,
                StopOnMismatch = scenario.ExecutionPreset.Settings.StopOnMismatch,
                StopOnReadabilityViolation = scenario.ExecutionPreset.Settings.StopOnReadabilityViolation,
                Notes = scenario.ExecutionPreset.Settings.Notes,
            }
            : new CombatSandboxExecutionSettings { PresetId = "runtime.active", DisplayName = "Runtime Active", Seed = 42, BatchCount = 1 };
        EnsureSharedAssets(activeConfig);
        activeConfig.Seed = activeConfig.Execution.Seed;
        activeConfig.BatchCount = activeConfig.Execution.BatchCount;
        activeConfig.AllySlots = BuildLegacyAllyMirror(leftTeam);
        activeConfig.EnemySlots = BuildLegacyEnemyMirror(rightTeam);

        EditorUtility.SetDirty(activeConfig);
        AssetDatabase.SaveAssetIfDirty(activeConfig);
        message = string.Join("\n", new[] { leftWarning, rightWarning }.Where(text => !string.IsNullOrWhiteSpace(text)));
        return true;
    }

    public static CombatSandboxScenarioAsset? DuplicateScenario(CombatSandboxScenarioAsset scenario)
    {
        if (scenario == null)
        {
            return null;
        }

        EnsureStarterLibrary();
        var sourcePath = AssetDatabase.GetAssetPath(scenario);
        var destinationPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(ScenarioFolder, $"{scenario.ScenarioId}_copy.asset").Replace('\\', '/'));
        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            return null;
        }

        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<CombatSandboxScenarioAsset>(destinationPath);
    }

    public static CombatSandboxConfig CreateTransientConfigCopy(CombatSandboxConfig source)
    {
        var copy = ScriptableObject.CreateInstance<CombatSandboxConfig>();
        copy.hideFlags = HideFlags.HideAndDontSave;
        EditorUtility.CopySerialized(source, copy);
        EnsureSharedAssets(copy);
        return copy;
    }

    public static bool IsActiveConfigAsset(CombatSandboxConfig? config)
    {
        if (config == null)
        {
            return false;
        }

        var assetPath = AssetDatabase.GetAssetPath(config);
        return string.Equals(assetPath, FirstPlayableBootstrap.CombatSandboxConfigAssetPath, StringComparison.Ordinal);
    }

    public static bool TryPushConfigToActiveConfig(CombatSandboxConfig source, out string message)
    {
        message = string.Empty;
        if (source == null)
        {
            message = "Source config is missing.";
            return false;
        }

        var activeConfig = EnsureActiveConfig();
        if (activeConfig == null)
        {
            message = "Active Combat Sandbox config could not be created.";
            return false;
        }

        EditorUtility.CopySerialized(source, activeConfig);
        EnsureSharedAssets(activeConfig);
        EditorUtility.SetDirty(activeConfig);
        AssetDatabase.SaveAssetIfDirty(activeConfig);
        return true;
    }

    public static CombatSandboxSceneLayoutAsset EnsureDefaultSceneLayoutAsset()
    {
        return GetOrCreateAsset<CombatSandboxSceneLayoutAsset>(
            DefaultSceneLayoutPath,
            asset =>
            {
                asset.LayoutId = "combat_sandbox_layout_default";
                asset.DisplayName = "Combat Sandbox Default Layout";
                asset.SpawnOffsetX = 1.25f;
                asset.AllyAnchors = CombatSandboxSceneLayoutAsset.CreateDefaultAnchors(isEnemy: false);
                asset.EnemyAnchors = CombatSandboxSceneLayoutAsset.CreateDefaultAnchors(isEnemy: true);
            });
    }

    public static CombatSandboxPreviewSettingsAsset EnsureDefaultPreviewSettingsAsset()
    {
        return GetOrCreateAsset<CombatSandboxPreviewSettingsAsset>(
            DefaultPreviewSettingsPath,
            asset =>
            {
                asset.SettingsId = "combat_sandbox_preview_default";
                asset.DisplayName = "Combat Sandbox Default Preview";
                asset.RangePreviewRadius = 2f;
                asset.NavigationPreviewRadius = 0.5f;
                asset.SeparationPreviewRadius = 0.75f;
                asset.PreferredRangeMinPreview = 1f;
                asset.PreferredRangeMaxPreview = 3f;
                asset.EngagementSlotRadiusPreview = 1.25f;
                asset.EngagementSlotCountPreview = 4;
                asset.HeadAnchorHeightPreview = 2f;
                asset.FrontlineGuardRadiusPreview = 2.5f;
                asset.ClusterRadiusPreview = 2.5f;
            });
    }

    private static bool TryBuildTeamDefinition(TeamLoadoutPresetAsset preset, out CombatSandboxTeamDefinition definition, out string warning)
    {
        warning = string.Empty;
        definition = new CombatSandboxTeamDefinition();
        if (preset == null)
        {
            warning = "Missing team preset asset.";
            return false;
        }

        definition = new CombatSandboxTeamDefinition
        {
            TeamId = string.IsNullOrWhiteSpace(preset.PresetId) ? preset.name : preset.PresetId,
            DisplayName = preset.DisplayName,
            SourceMode = preset.Source.SourceMode,
            TeamPosture = preset.Source.TeamPosture,
            TeamTacticId = preset.Source.TeamTacticId,
            Tags = preset.Source.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            Members = new List<CombatSandboxTeamMemberDefinition>(),
            TeamTemporaryAugmentIds = preset.Source.TeamTemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            TeamPermanentAugmentIds = preset.Source.TeamPermanentAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            RemoteDeck = new CombatSandboxRemoteDeckReference
            {
                UserId = preset.Source.RemoteDeck?.UserId ?? string.Empty,
                DeckId = preset.Source.RemoteDeck?.DeckId ?? string.Empty,
                Revision = preset.Source.RemoteDeck?.Revision ?? string.Empty,
            },
            ProvenanceLabel = AssetDatabase.GetAssetPath(preset),
            Notes = preset.Source.Notes,
        };

        if (preset.Source.SourceMode == SandboxLoadoutSourceKind.SavedSnapshotAsset && preset.Source.SnapshotAsset != null)
        {
            definition = CloneSnapshotDefinition(preset.Source.SnapshotAsset.Snapshot, definition, SandboxLoadoutSourceKind.SavedSnapshotAsset, preset.DisplayName);
            return true;
        }

        if (preset.Source.SourceMode == SandboxLoadoutSourceKind.SnapshotJson)
        {
            var json = ResolveSnapshotJson(preset.Source);
            if (string.IsNullOrWhiteSpace(json))
            {
                warning = $"SnapshotJson preset '{preset.DisplayName}' is empty.";
                return false;
            }

            var parsed = JsonUtility.FromJson<CombatSandboxTeamDefinition>(json);
            if (parsed == null)
            {
                warning = $"SnapshotJson preset '{preset.DisplayName}' could not be parsed.";
                return false;
            }

            definition = CloneSnapshotDefinition(parsed, definition, SandboxLoadoutSourceKind.SnapshotJson, preset.DisplayName);
            return true;
        }

        if (preset.Source.SourceMode == SandboxLoadoutSourceKind.RemoteDeckRef)
        {
            if (preset.Source.SnapshotAsset != null)
            {
                definition = CloneSnapshotDefinition(preset.Source.SnapshotAsset.Snapshot, definition, SandboxLoadoutSourceKind.RemoteDeckRef, preset.DisplayName);
                return true;
            }

            var json = ResolveSnapshotJson(preset.Source);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var parsed = JsonUtility.FromJson<CombatSandboxTeamDefinition>(json);
                if (parsed != null)
                {
                    definition = CloneSnapshotDefinition(parsed, definition, SandboxLoadoutSourceKind.RemoteDeckRef, preset.DisplayName);
                    return true;
                }
            }

            warning = $"Remote deck preset '{preset.DisplayName}' is stub-only and has no cached snapshot.";
            return true;
        }

        definition.Members = preset.Source.Members?
            .Select(member => new CombatSandboxTeamMemberDefinition
            {
                MemberId = member.MemberId,
                SourceKind = member.SourceKind,
                HeroId = member.HeroId,
                DisplayName = member.DisplayName,
                ArchetypeId = member.ArchetypeId,
                CharacterId = member.CharacterId,
                Anchor = member.Anchor,
                RoleInstructionId = member.RoleInstructionId,
                BuildOverride = member.BuildOverride != null
                    ? CloneBuildOverride(member.BuildOverride.Data)
                    : new CombatSandboxBuildOverrideData(),
                Notes = member.Notes,
            })
            .ToList() ?? new List<CombatSandboxTeamMemberDefinition>();
        return true;
    }

    private static CombatSandboxTeamDefinition CloneSnapshotDefinition(
        CombatSandboxTeamDefinition source,
        CombatSandboxTeamDefinition fallback,
        SandboxLoadoutSourceKind sourceMode,
        string displayName)
    {
        var cloned = new CombatSandboxTeamDefinition
        {
            TeamId = string.IsNullOrWhiteSpace(source.TeamId) ? fallback.TeamId : source.TeamId,
            DisplayName = string.IsNullOrWhiteSpace(source.DisplayName) ? displayName : source.DisplayName,
            SourceMode = sourceMode,
            TeamPosture = source.TeamPosture,
            TeamTacticId = source.TeamTacticId,
            Tags = source.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            Members = source.Members?.Select(CloneMember).ToList() ?? new List<CombatSandboxTeamMemberDefinition>(),
            TeamTemporaryAugmentIds = source.TeamTemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            TeamPermanentAugmentIds = source.TeamPermanentAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            RemoteDeck = fallback.RemoteDeck,
            ProvenanceLabel = fallback.ProvenanceLabel,
            Notes = source.Notes,
        };
        return cloned;
    }

    private static string ResolveSnapshotJson(CombatSandboxPresetSourceDefinition source)
    {
        if (source.SnapshotJsonAsset != null && !string.IsNullOrWhiteSpace(source.SnapshotJsonAsset.text))
        {
            return source.SnapshotJsonAsset.text;
        }

        return source.SnapshotJson ?? string.Empty;
    }

    private static List<CombatSandboxAllySlot> BuildLegacyAllyMirror(CombatSandboxTeamDefinition team)
    {
        return team.Members
            .Where(member => !string.IsNullOrWhiteSpace(member.HeroId))
            .Select(member => new CombatSandboxAllySlot
            {
                HeroId = member.HeroId,
                Anchor = member.Anchor,
                RoleInstructionIdOverride = !string.IsNullOrWhiteSpace(member.RoleInstructionId)
                    ? member.RoleInstructionId
                    : member.BuildOverride?.RoleInstructionIdOverride ?? string.Empty,
            })
            .ToList();
    }

    private static List<CombatSandboxEnemySlot> BuildLegacyEnemyMirror(CombatSandboxTeamDefinition team)
    {
        return team.Members
            .Select(member => new CombatSandboxEnemySlot
            {
                ParticipantId = member.MemberId,
                DisplayName = member.DisplayName,
                CharacterId = member.CharacterId,
                ArchetypeIdOverride = member.ArchetypeId,
                Anchor = member.Anchor,
                PositiveTraitId = member.BuildOverride?.PositiveTraitId ?? string.Empty,
                NegativeTraitId = member.BuildOverride?.NegativeTraitId ?? string.Empty,
                RoleInstructionId = !string.IsNullOrWhiteSpace(member.RoleInstructionId)
                    ? member.RoleInstructionId
                    : member.BuildOverride?.RoleInstructionIdOverride ?? string.Empty,
                TemporaryAugmentIds = member.BuildOverride?.TemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
            })
            .ToList();
    }

    private static CombatSandboxTeamMemberDefinition CloneMember(CombatSandboxTeamMemberDefinition source)
    {
        return new CombatSandboxTeamMemberDefinition
        {
            MemberId = source.MemberId,
            SourceKind = source.SourceKind,
            HeroId = source.HeroId,
            DisplayName = source.DisplayName,
            ArchetypeId = source.ArchetypeId,
            CharacterId = source.CharacterId,
            Anchor = source.Anchor,
            RoleInstructionId = source.RoleInstructionId,
            BuildOverride = CloneBuildOverride(source.BuildOverride),
            Notes = source.Notes,
        };
    }

    private static CombatSandboxBuildOverrideData CloneBuildOverride(CombatSandboxBuildOverrideData source)
    {
        return source == null
            ? new CombatSandboxBuildOverrideData()
            : new CombatSandboxBuildOverrideData
            {
                OverrideId = source.OverrideId,
                DisplayName = source.DisplayName,
                Tags = source.Tags?.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                EquippedItems = source.EquippedItems?.Select(item => new CombatSandboxItemOverrideData
                {
                    ItemId = item.ItemId,
                    AffixIds = item.AffixIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                }).ToList() ?? new List<CombatSandboxItemOverrideData>(),
                PassiveBoardId = source.PassiveBoardId,
                PassiveNodeIds = source.PassiveNodeIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                TemporaryAugmentIds = source.TemporaryAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                PermanentAugmentIds = source.PermanentAugmentIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList() ?? new List<string>(),
                FlexActiveSkillId = source.FlexActiveSkillId,
                FlexPassiveSkillId = source.FlexPassiveSkillId,
                PositiveTraitId = source.PositiveTraitId,
                NegativeTraitId = source.NegativeTraitId,
                RoleInstructionIdOverride = source.RoleInstructionIdOverride,
                RetrainCount = source.RetrainCount,
                Notes = source.Notes,
            };
    }

    private static void CreateScenario(
        string scenarioId,
        string displayName,
        TeamLoadoutPresetAsset leftTeam,
        TeamLoadoutPresetAsset rightTeam,
        CombatSandboxExecutionPreset execution,
        bool favorite,
        params string[] tags)
    {
        var path = $"{ScenarioFolder}/combat_sandbox_scenario_{scenarioId}.asset";
        var scenario = GetOrCreateAsset<CombatSandboxScenarioAsset>(path, _ => { });
        scenario.ScenarioId = scenarioId;
        scenario.DisplayName = displayName;
        scenario.IsFavorite = favorite;
        scenario.LeftTeam = leftTeam;
        scenario.RightTeam = rightTeam;
        scenario.ExecutionPreset = execution;
        scenario.Tags = tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        scenario.ExpectedOutcome = favorite
            ? "Primary starter scenario."
            : "Regression / matchup authoring baseline.";
        scenario.Notes = "Generated by CombatSandboxAuthoringAssetUtility.";
        EditorUtility.SetDirty(scenario);
    }

    private static void EnsureSharedAssets(CombatSandboxConfig config)
    {
        if (config.SceneLayout == null)
        {
            config.SceneLayout = EnsureDefaultSceneLayoutAsset();
        }

        if (config.PreviewSettings == null)
        {
            config.PreviewSettings = EnsureDefaultPreviewSettingsAsset();
        }
    }

    private static T GetOrCreateAsset<T>(string assetPath, Action<T> initialize) where T : ScriptableObject
    {
        EnsureFolderPath(Path.GetDirectoryName(assetPath)!.Replace('\\', '/'));

        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || File.Exists(assetPath))
        {
            Debug.Log($"[CombatSandbox] {typeof(T).Name} 타입 해석 불가 — 삭제 후 재생성합니다: {assetPath}");
            AssetDatabase.DeleteAsset(assetPath);
            if (File.Exists(assetPath))
            {
                File.Delete(assetPath);
            }
            var metaPath = assetPath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
        }

        var asset = ScriptableObject.CreateInstance<T>();
        initialize(asset);
        AssetDatabase.CreateAsset(asset, assetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var reloaded = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (reloaded != null)
        {
            return reloaded;
        }

        throw BuildUnresolvedAssetException<T>(
            assetPath,
            "새로 만든 asset을 다시 로드하지 못해 broken asset 자동 교체를 중단했다.");
    }

    private static InvalidOperationException BuildUnresolvedAssetException<T>(string assetPath, string reason) where T : ScriptableObject
    {
        return new InvalidOperationException(
            $"[CombatSandbox] {typeof(T).Name} asset load failed: {assetPath}\n" +
            $"{reason}\n" +
            $"Recovery: {RecoveryInstructions}");
    }

    private static void ImportAssetIfPresent(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        if (!AssetDatabase.IsValidFolder(assetPath)
            && AssetDatabase.LoadMainAssetAtPath(assetPath) == null
            && !File.Exists(assetPath))
        {
            return;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static void EnsureFolder(string parentFolder, string childFolder)
    {
        if (AssetDatabase.IsValidFolder($"{parentFolder}/{childFolder}"))
        {
            return;
        }

        AssetDatabase.CreateFolder(parentFolder, childFolder);
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
}
