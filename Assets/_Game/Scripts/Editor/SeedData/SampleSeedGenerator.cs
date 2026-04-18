using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Combat.Model;
using SM.Editor.Bootstrap;
using SM.Editor.Validation;
using SM.Content;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core;
using SM.Core.Contracts;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SM.Editor.SeedData;

public static class SampleSeedGenerator
{
    public const string ResourcesRoot = "Assets/Resources/_Game/Content/Definitions";
    public const string LegacyRoot = "Assets/_Game/Content/Definitions";
    private const string StoryEventsDir = ResourcesRoot + "/StoryEvents";
    private const string DialogueSequencesDir = ResourcesRoot + "/DialogueSequences";
    private const string ChapterBeatsDir = ResourcesRoot + "/ChapterBeats";
    private const string HeroLoreDir = ResourcesRoot + "/HeroLore";
    private const string StoryTableName = ContentLocalizationTables.Story;

    [MenuItem("SM/Internal/Content/Generate Sample Content")]
    public static void Generate()
    {
        EnsureFolders();
        LocalizationFoundationBootstrap.EnsureFoundationAssets();

        var stats = CreateStats();
        var races = CreateRaces();
        var classes = CreateClasses();
        var footprintProfiles = CreateFootprintProfiles();
        var behaviorProfiles = CreateBehaviorProfiles();
        var mobilityProfiles = CreateMobilityProfiles();
        var skills = CreateSkills();
        CreateStableTags();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var stableTags = LoadDefinitionsById<StableTagDefinition>($"{ResourcesRoot}/StableTags");
        CreateSupportModifierSkills(stableTags);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var traitPools = CreateTraitPools();
        var archetypes = CreateArchetypes(races, classes, traitPools, skills, footprintProfiles, behaviorProfiles, mobilityProfiles);
        var skillCatalog = LoadDefinitionsById<SkillDefinitionAsset>($"{ResourcesRoot}/Skills");
        PatchLaunchFloorArchetypes(races, classes, traitPools, skillCatalog, stableTags, footprintProfiles, behaviorProfiles, mobilityProfiles);
        var patchedArchetypes = LoadDefinitionsById<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes");
        var roleInstructions = LoadDefinitionsById<RoleInstructionDefinition>($"{ResourcesRoot}/RoleInstructions");
        EnsureRoleGlossaryLocalization(roleInstructions);
        var characters = CreateCharacters(patchedArchetypes, roleInstructions);
        CreateAugments();
        CreateItems();
        CreateAffixes();
        CreateSafeTargetPassiveNodes(stableTags);
        PatchLaunchFloorItemsAndSkills(stableTags);
        CreateStatusCatalog();
        CreateTraitTokens();
        CreateRewardSourcesAndDropTables();
        CreateCampaignEncounterCatalog();
        var rewardTables = CreateRewardTables();
        CreateExpedition(rewardTables);
        RepairResidualAuthoring(stableTags);
        ApplyLoopCGovernance(stableTags);
        SeedNarrativeContent();

        AssetDatabase.SaveAssets();
        ReimportCanonicalAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        PatchSafeTargetSpecialistFallbackYaml();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        var fixedScriptReferences = CanonicalContentScriptReferenceRepair.RepairResourcesRoot();
        if (fixedScriptReferences > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        Debug.Log($"SM sample content generated under Resources. Root={ResourcesRoot}, Stats={stats.Count}, Races={races.Count}, Classes={classes.Count}, Skills={skills.Count}, Archetypes={patchedArchetypes.Count}, Characters={characters.Count}");
    }

    [MenuItem("SM/Internal/Content/Migrate Legacy Sample Content")]
    public static void MigrateLegacySampleContent()
    {
        EnsureFolders();

        if (HasCanonicalMinimumContent() && !HasLegacyContent())
        {
            Debug.Log($"SM canonical sample content already present. Root={ResourcesRoot}");
            return;
        }

        if (TryMigrateLegacySampleContent())
        {
            return;
        }

        Debug.LogWarning($"SM legacy sample content migration could not fully restore the canonical root. Regenerating under {ResourcesRoot}.");
        Generate();
    }

    [MenuItem("SM/Internal/Content/Migrate Legacy Sample Content", true)]
    public static bool ValidateMigrateLegacySampleContent()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public static void EnsureCanonicalSampleContent()
    {
        EnsureFolders();
        LocalizationFoundationBootstrap.EnsureFoundationAssets();

        if (TryGetCanonicalSampleContentReadinessIssue(out var issue))
        {
            var fixedScriptReferences = CanonicalContentScriptReferenceRepair.RepairResourcesRoot();
            if (fixedScriptReferences > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }

            Debug.Log($"SM canonical sample content already preflight-ready. Root={ResourcesRoot}");
            return;
        }

        Debug.Log($"SM canonical sample content is not preflight-ready. {issue} Regenerating under {ResourcesRoot}.");
        Generate();
    }

    public static void RequireCanonicalSampleContentReady(string consumer)
    {
        if (TryGetCanonicalSampleContentReadinessIssue(out var issue))
        {
            return;
        }

        var consumerLabel = string.IsNullOrWhiteSpace(consumer) ? "current caller" : consumer;
        throw new InvalidOperationException(
            $"SM canonical sample content is not preflight-ready for {consumerLabel}. {issue} " +
            "Run 'pwsh -File tools/unity-bridge.ps1 seed-content' or Unity menu 'SM/Internal/Content/Generate Sample Content' before running this path.");
    }

    public static bool TryGetCanonicalSampleContentReadinessIssue(out string issue)
    {
        var minimumFailures = GetCanonicalMinimumContentFailures();
        if (minimumFailures.Count > 0)
        {
            var failureList = string.Join(", ", minimumFailures.Take(4));
            issue = HasCanonicalAssetFootprintInDatabase() || HasCanonicalAssetFootprintOnDisk()
                ? $"Canonical root contains unreadable or invalid baseline assets under {ResourcesRoot}: {failureList}."
                : $"Canonical root is missing required baseline assets under {ResourcesRoot}: {failureList}.";
            return false;
        }

        if (HasCanonicalAuthoringDrift())
        {
            issue = "Canonical root contains stale or drifted authored assets.";
            return false;
        }

        issue = string.Empty;
        return true;
    }

    public static bool HasCanonicalMinimumContent()
    {
        return GetCanonicalMinimumContentFailures().Count == 0;
    }

    public static IReadOnlyList<string> GetCanonicalMinimumContentFailures()
    {
        var failures = new List<string>();

        CheckMinimum(failures, "Stats/stat_max_health.asset", HasCanonicalAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_max_health.asset", stat => stat.Id, "max_health"));
        CheckMinimum(failures, "Races/race_human.asset", HasCanonicalAsset<RaceDefinition>($"{ResourcesRoot}/Races/race_human.asset", race => race.Id, "human"));
        CheckMinimum(failures, "Classes/class_vanguard.asset", HasCanonicalAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_vanguard.asset", @class => @class.Id, "vanguard"));
        CheckMinimum(failures, "Characters/character_warden.asset", HasCanonicalAsset<CharacterDefinition>($"{ResourcesRoot}/Characters/character_warden.asset", character => character.Id, "warden"));
        CheckMinimum(failures, "FootprintProfiles/footprint_vanguard.asset", HasCanonicalAsset<FootprintProfileDefinition>($"{ResourcesRoot}/FootprintProfiles/footprint_vanguard.asset", profile => profile.EngagementSlotCount > 0, fallbackPath => HasAssetNumericFieldAboveZero(fallbackPath, "EngagementSlotCount")));
        CheckMinimum(failures, "BehaviorProfiles/behavior_vanguard.asset", HasCanonicalAsset<BehaviorProfileDefinition>($"{ResourcesRoot}/BehaviorProfiles/behavior_vanguard.asset", profile => profile.ReevaluationInterval > 0f, fallbackPath => HasAssetNumericFieldAboveZero(fallbackPath, "ReevaluationInterval")));
        CheckMinimum(failures, "MobilityProfiles/mobility_ranger.asset", HasCanonicalAsset<MobilityProfileDefinition>($"{ResourcesRoot}/MobilityProfiles/mobility_ranger.asset", profile => profile.Distance > 0f, fallbackPath => HasAssetNumericFieldAboveZero(fallbackPath, "Distance")));
        CheckMinimum(failures, "Archetypes/archetype_warden.asset", HasCanonicalAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_warden.asset", archetype => archetype.Id, "warden"));
        CheckMinimum(failures, "Skills/skill_warden_utility.asset", HasCanonicalAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_warden_utility.asset", skill => skill.Id, "skill_warden_utility"));
        CheckMinimum(failures, "Augments/augment_silver_guard.asset", HasCanonicalAsset<AugmentDefinition>($"{ResourcesRoot}/Augments/augment_silver_guard.asset", augment => augment.Id, "augment_silver_guard"));
        CheckMinimum(failures, "Items/item_warden_armor.asset", HasCanonicalAsset<ItemBaseDefinition>($"{ResourcesRoot}/Items/item_warden_armor.asset", item => item.Id, "item_warden_armor"));
        CheckMinimum(failures, "Affixes/affix_guarded.asset", HasCanonicalAsset<AffixDefinition>($"{ResourcesRoot}/Affixes/affix_guarded.asset", affix => affix.Id, "affix_guarded"));
        CheckMinimum(failures, "CampaignChapters/chapter_ashen_frontier.asset", HasCanonicalAsset<CampaignChapterDefinition>($"{ResourcesRoot}/CampaignChapters/chapter_ashen_frontier.asset", chapter => chapter.Id, "chapter_ashen_frontier"));
        CheckMinimum(failures, "ExpeditionSites/site_ashen_gate.asset", HasCanonicalAsset<ExpeditionSiteDefinition>($"{ResourcesRoot}/ExpeditionSites/site_ashen_gate.asset", site => site.Id, "site_ashen_gate"));
        CheckMinimum(failures, "StatusFamilies/status_family_guarded.asset", HasCanonicalAsset<StatusFamilyDefinition>($"{ResourcesRoot}/StatusFamilies/status_family_guarded.asset", status => status.Id, "guarded"));
        CheckMinimum(failures, "RewardSources/reward_source_skirmish.asset", HasCanonicalAsset<RewardSourceDefinition>($"{ResourcesRoot}/RewardSources/reward_source_skirmish.asset", reward => reward.Id, "reward_source_skirmish"));
        CheckMinimum(failures, "TraitTokens/trait_reroll_token.asset", HasCanonicalAsset<TraitTokenDefinition>($"{ResourcesRoot}/TraitTokens/trait_reroll_token.asset", token => token.Id, "trait_reroll_token"));
        CheckMinimum(failures, "StoryEvents/story_event_site_intro_ashen_gate.asset", HasCanonicalAsset<StoryEventDefinition>($"{StoryEventsDir}/story_event_site_intro_ashen_gate.asset", definition => definition.Id, "story_event_site_intro_ashen_gate"));
        CheckMinimum(failures, "DialogueSequences/dialogue_scene_ashen_gate_intro.asset", HasCanonicalAsset<DialogueSequenceDefinition>($"{DialogueSequencesDir}/dialogue_scene_ashen_gate_intro.asset", definition => definition.Id, "dialogue_scene_ashen_gate_intro"));
        CheckMinimum(failures, "ChapterBeats/beat.chapter_ashen_gate.site_ashen_gate.node_1.asset", HasCanonicalAsset<ChapterBeatDefinition>($"{ChapterBeatsDir}/beat.chapter_ashen_gate.site_ashen_gate.node_1.asset", definition => definition.Id, "beat.chapter_ashen_gate.site_ashen_gate.node_1"));
        CheckMinimum(failures, "HeroLore/hero_lore_dawn_priest.asset", HasCanonicalAsset<HeroLoreDefinition>($"{HeroLoreDir}/hero_lore_dawn_priest.asset", definition => definition.Id, "hero_lore_dawn_priest"));

        return failures;
    }

    private static void CheckMinimum(List<string> failures, string label, bool passed)
    {
        if (!passed)
        {
            failures.Add(label);
        }
    }

    private static void EnsureFolders()
    {
        var folders = new[]
        {
            "Assets/Resources",
            "Assets/Resources/_Game",
            "Assets/Resources/_Game/Content",
            ResourcesRoot,
            $"{ResourcesRoot}/Stats",
            $"{ResourcesRoot}/Races",
            $"{ResourcesRoot}/Classes",
            $"{ResourcesRoot}/Characters",
            $"{ResourcesRoot}/FootprintProfiles",
            $"{ResourcesRoot}/BehaviorProfiles",
            $"{ResourcesRoot}/MobilityProfiles",
            $"{ResourcesRoot}/Traits",
            $"{ResourcesRoot}/Skills",
            $"{ResourcesRoot}/Archetypes",
            $"{ResourcesRoot}/PassiveBoards",
            $"{ResourcesRoot}/PassiveNodes",
            $"{ResourcesRoot}/Augments",
            $"{ResourcesRoot}/Items",
            $"{ResourcesRoot}/Affixes",
            $"{ResourcesRoot}/StableTags",
            $"{ResourcesRoot}/CampaignChapters",
            $"{ResourcesRoot}/ExpeditionSites",
            $"{ResourcesRoot}/Encounters",
            $"{ResourcesRoot}/EnemySquads",
            $"{ResourcesRoot}/BossOverlays",
            $"{ResourcesRoot}/StatusFamilies",
            $"{ResourcesRoot}/CleanseProfiles",
            $"{ResourcesRoot}/ControlDiminishingRules",
            $"{ResourcesRoot}/RewardSources",
            $"{ResourcesRoot}/DropTables",
            $"{ResourcesRoot}/LootBundles",
            $"{ResourcesRoot}/TraitTokens",
            $"{ResourcesRoot}/Rewards",
            $"{ResourcesRoot}/Expeditions",
            StoryEventsDir,
            DialogueSequencesDir,
            ChapterBeatsDir,
            HeroLoreDir,
        };

        foreach (var folder in folders)
        {
            EnsureFolder(folder);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
        var name = Path.GetFileName(folder);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private static bool TryMigrateLegacySampleContent()
    {
        if (!HasLegacyContent())
        {
            Debug.Log($"SM legacy sample content root not found. Expected legacy root: {LegacyRoot}");
            return false;
        }

        var migrated = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var sourcePath in EnumerateLegacyAssetPaths())
        {
            var targetPath = GetCanonicalPath(sourcePath);
            EnsureFolder(Path.GetDirectoryName(targetPath)!.Replace('\\', '/'));

            if (AssetDatabase.LoadMainAssetAtPath(targetPath) != null || File.Exists(targetPath))
            {
                skipped++;
                Debug.LogWarning($"SM legacy sample content migration skipped existing canonical asset: {targetPath}");
                continue;
            }

            var moveError = AssetDatabase.MoveAsset(sourcePath, targetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                failed++;
                Debug.LogWarning($"SM legacy sample content migration failed: {sourcePath} -> {targetPath}. {moveError}");
                continue;
            }

            migrated++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (HasCanonicalMinimumContent())
        {
            Debug.Log($"SM legacy sample content migration finished. Root={ResourcesRoot}, Migrated={migrated}, Skipped={skipped}, Failed={failed}");
            return true;
        }

        Debug.LogWarning($"SM legacy sample content migration left canonical root incomplete. Root={ResourcesRoot}, Migrated={migrated}, Skipped={skipped}, Failed={failed}");
        return false;
    }

    private static bool HasLegacyContent()
    {
        return Directory.Exists(LegacyRoot) && EnumerateLegacyAssetPaths().Any();
    }

    private static IEnumerable<string> EnumerateLegacyAssetPaths()
    {
        if (!Directory.Exists(LegacyRoot))
        {
            return Enumerable.Empty<string>();
        }

        return Directory
            .EnumerateFiles(LegacyRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".meta"))
            .Select(ToUnityPath)
            .ToArray();
    }

    private static string GetCanonicalPath(string sourcePath)
    {
        var relative = sourcePath.Substring(LegacyRoot.Length).TrimStart('/');
        return $"{ResourcesRoot}/{relative}";
    }

    private static string ToUnityPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        var projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
        return normalized.StartsWith(projectRoot + "/")
            ? normalized.Substring(projectRoot.Length + 1)
            : normalized;
    }

    private static bool HasAssetText(string path, string fragment)
    {
        foreach (var candidatePath in EnumerateProjectCandidatePaths(path))
        {
            if (File.Exists(candidatePath) && File.ReadAllText(candidatePath).Contains(fragment))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSerializedIdText(string path, string expectedId)
    {
        return HasAssetText(path, $"Id: {expectedId}")
               || HasAssetText(path, $"_id: {expectedId}");
    }

    private static bool HasAssetNumericFieldAboveZero(string path, string fieldName)
    {
        foreach (var candidatePath in EnumerateProjectCandidatePaths(path))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            foreach (var line in File.ReadLines(candidatePath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith(fieldName + ":", StringComparison.Ordinal))
                {
                    continue;
                }

                var valueText = trimmed.Substring(fieldName.Length + 1).Trim();
                if (float.TryParse(valueText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var floatValue))
                {
                    return floatValue > 0f;
                }

                if (int.TryParse(valueText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var intValue))
                {
                    return intValue > 0;
                }

                return false;
            }
        }

        return false;
    }

    private static bool HasCanonicalAsset<T>(string path, System.Func<T, string> selector, string expectedId) where T : UnityEngine.Object
    {
        var asset = TryLoadCanonicalAsset<T>(path);
        if (asset != null)
        {
            return string.Equals(selector(asset), expectedId, System.StringComparison.Ordinal);
        }

        return HasSerializedIdText(path, expectedId);
    }

    private static bool HasCanonicalAsset<T>(string path, System.Func<T, bool> predicate) where T : UnityEngine.Object
    {
        var asset = TryLoadCanonicalAsset<T>(path);
        return asset != null && predicate(asset);
    }

    private static bool HasCanonicalAsset<T>(string path, System.Func<T, bool> predicate, System.Func<string, bool> fallbackPredicate) where T : UnityEngine.Object
    {
        var asset = TryLoadCanonicalAsset<T>(path);
        if (asset != null)
        {
            return predicate(asset);
        }

        return fallbackPredicate(path);
    }

    private static string ToResourcesLoadPath(string assetPath)
    {
        const string resourcesPrefix = "Assets/Resources/";
        if (!assetPath.StartsWith(resourcesPrefix, System.StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var relativePath = assetPath.Substring(resourcesPrefix.Length);
        return Path.ChangeExtension(relativePath, null)?.Replace('\\', '/')
               ?? string.Empty;
    }

    private static IEnumerable<string> EnumerateProjectCandidatePaths(string assetPath)
    {
        if (Path.IsPathRooted(assetPath))
        {
            yield return assetPath;
            yield break;
        }

        var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(projectRoot))
        {
            yield return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        var currentDirectory = Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            yield return Path.GetFullPath(Path.Combine(currentDirectory, assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        yield return Path.GetFullPath(assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool HasCanonicalAssetFootprintOnDisk()
    {
        foreach (var root in EnumerateProjectRootCandidates())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            if (HasNamedAsset(root, "stat_max_health.asset")
                && HasNamedAsset(root, "race_human.asset")
                && HasNamedAsset(root, "class_vanguard.asset")
                && HasNamedAsset(root, "footprint_vanguard.asset"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCanonicalAssetFootprintInDatabase()
    {
        return HasAssetNamedInDatabase("stat_max_health")
               && HasAssetNamedInDatabase("race_human")
               && HasAssetNamedInDatabase("class_vanguard")
               && HasAssetNamedInDatabase("footprint_vanguard");
    }

    private static IEnumerable<string> EnumerateProjectRootCandidates()
    {
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        var appRoot = Path.GetDirectoryName(Application.dataPath);
        if (!string.IsNullOrWhiteSpace(appRoot))
        {
            var full = Path.GetFullPath(appRoot);
            if (seen.Add(full))
            {
                yield return full;
            }
        }

        if (!string.IsNullOrWhiteSpace(Environment.CurrentDirectory))
        {
            var full = Path.GetFullPath(Environment.CurrentDirectory);
            if (seen.Add(full))
            {
                yield return full;
            }
        }
    }

    private static bool HasNamedAsset(string root, string fileName)
    {
        return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Any();
    }

    private static bool HasAssetNamedInDatabase(string assetName)
    {
        foreach (var guid in AssetDatabase.FindAssets(assetName))
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileNameWithoutExtension(assetPath), assetName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static T? TryLoadCanonicalAsset<T>(string path) where T : UnityEngine.Object
    {
        var resourcePath = ToResourcesLoadPath(path);
        var asset = string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<T>(resourcePath);
        asset ??= AssetDatabase.LoadAssetAtPath<T>(path);
        asset ??= AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
        if (asset != null)
        {
            return asset;
        }

        var expectedPath = path.Replace('\\', '/');
        var fileName = Path.GetFileNameWithoutExtension(path);
        foreach (var guid in AssetDatabase.FindAssets(fileName))
        {
            var candidatePath = AssetDatabase.GUIDToAssetPath(guid)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(candidatePath))
            {
                continue;
            }

            if (!string.Equals(candidatePath, expectedPath, System.StringComparison.OrdinalIgnoreCase)
                && !candidatePath.EndsWith($"/{Path.GetFileName(path)}", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            asset = AssetDatabase.LoadAssetAtPath<T>(candidatePath);
            asset ??= AssetDatabase.LoadAssetAtPath(candidatePath, typeof(T)) as T;
            if (asset != null)
            {
                return asset;
            }
        }

        return null;
    }

    private static void ReimportCanonicalAssets()
    {
        var assetPaths = Directory
            .EnumerateFiles(ResourcesRoot, "*.asset", SearchOption.AllDirectories)
            .Select(ToUnityPath)
            .ToList();
        if (assetPaths.Count == 0)
        {
            return;
        }

        foreach (var assetPath in assetPaths)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }
    }

    private static List<StatDefinition> CreateStats()
    {
        var stats = new (string id, string enName, string koName)[]
        {
            ("max_health", "Max Health", "최대 체력"),
            ("armor", "Armor", "방어력"),
            ("resist", "Resist", "저항력"),
            ("phys_power", "Physical Power", "물리 위력"),
            ("mag_power", "Magical Power", "마법 위력"),
            ("heal_power", "Heal Power", "회복력"),
            ("attack_speed", "Attack Speed", "공격 속도"),
            ("move_speed", "Move Speed", "이동 속도"),
            ("attack_range", "Attack Range", "공격 사거리"),
            ("mana_max", "Mana Max", "최대 마나"),
            ("mana_gain_on_attack", "Mana Gain On Attack", "공격 시 마나 획득"),
            ("mana_gain_on_hit", "Mana Gain On Hit", "피격 시 마나 획득"),
            ("cooldown_recovery", "Cooldown Recovery", "재사용 대기시간 회복"),
            ("crit_chance", "Critical Chance", "치명타 확률"),
            ("crit_multiplier", "Critical Multiplier", "치명타 배율"),
            ("phys_pen", "Physical Penetration", "물리 관통"),
            ("mag_pen", "Magical Penetration", "마법 관통"),
            ("lifesteal", "Lifesteal", "생명력 흡수"),
            ("omnivamp", "Omnivamp", "전능 흡혈"),
            ("tenacity", "Tenacity", "강인함"),
            ("aggro_radius", "Aggro Radius", "어그로 반경"),
            ("preferred_distance", "Preferred Distance", "선호 거리"),
            ("protect_radius", "Protect Radius", "보호 반경"),
            ("attack_windup", "Attack Windup", "공격 선딜"),
            ("cast_windup", "Cast Windup", "시전 선딜"),
            ("attack_cooldown", "Attack Cooldown", "공격 쿨다운"),
            ("leash_distance", "Leash Distance", "복귀 거리"),
            ("target_switch_delay", "Target Switch Delay", "타깃 전환 지연"),
            ("projectile_speed", "Projectile Speed", "투사체 속도"),
            ("collision_radius", "Collision Radius", "충돌 반경"),
        };

        return stats.Select(stat => CreateAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_{stat.id}.asset", a =>
        {
            a.Id = stat.id;
            a.NameKey = ContentLocalizationTables.BuildStatNameKey(stat.id);
            a.DescriptionKey = ContentLocalizationTables.BuildStatDescriptionKey(stat.id);
            UpsertStringEntry(ContentLocalizationTables.Stats, a.NameKey, stat.koName, stat.enName);
            UpsertStringEntry(ContentLocalizationTables.Stats, a.DescriptionKey, stat.koName, stat.enName);
        })).ToList();
    }

    private static Dictionary<string, RaceDefinition> CreateRaces()
    {
        return new[]
        {
            ("human", "Human", "인간", "Flexible baseline race.", "기본이 탄탄한 균형 종족."),
            ("beastkin", "Beastkin", "수인", "Aggressive feral pressure.", "공세적인 야수형 압박 종족."),
            ("undead", "Undead", "언데드", "Attrition-oriented deathless fighters.", "소모전에 강한 불사 전사 종족."),
        }.ToDictionary(tuple => tuple.Item1, tuple =>
        {
            var path = $"{ResourcesRoot}/Races/race_{tuple.Item1}.asset";
            var asset = CreateAsset<RaceDefinition>(path, a =>
            {
                a.Id = tuple.Item1;
                a.NameKey = ContentLocalizationTables.BuildRaceNameKey(tuple.Item1);
                a.DescriptionKey = ContentLocalizationTables.BuildRaceDescriptionKey(tuple.Item1);
                UpsertStringEntry(ContentLocalizationTables.Races, a.NameKey, tuple.Item3, tuple.Item2);
                UpsertStringEntry(ContentLocalizationTables.Races, a.DescriptionKey, tuple.Item5, tuple.Item4);
            });
            PatchSerializedLocalizedIdentity(asset, asset.Id, asset.NameKey, asset.DescriptionKey);
            return asset;
        });
    }

    private static Dictionary<string, ClassDefinition> CreateClasses()
    {
        return new[]
        {
            ("vanguard", "Vanguard", "선봉", "Frontline tank and anchor.", "전열을 버티는 탱커 역할."),
            ("duelist", "Duelist", "결투가", "Melee pressure and finishing.", "근접 압박과 마무리 역할."),
            ("ranger", "Ranger", "사수", "Ranged carry and pick-off.", "원거리 딜링과 픽오프 역할."),
            ("mystic", "Mystic", "비술사", "Healing and support control.", "회복과 지원 제어 역할."),
        }.ToDictionary(tuple => tuple.Item1, tuple =>
        {
            var path = $"{ResourcesRoot}/Classes/class_{tuple.Item1}.asset";
            var asset = CreateAsset<ClassDefinition>(path, a =>
            {
                a.Id = tuple.Item1;
                a.NameKey = ContentLocalizationTables.BuildClassNameKey(tuple.Item1);
                a.DescriptionKey = ContentLocalizationTables.BuildClassDescriptionKey(tuple.Item1);
                UpsertStringEntry(ContentLocalizationTables.Classes, a.NameKey, tuple.Item3, tuple.Item2);
                UpsertStringEntry(ContentLocalizationTables.Classes, a.DescriptionKey, tuple.Item5, tuple.Item4);
            });
            PatchSerializedLocalizedIdentity(asset, asset.Id, asset.NameKey, asset.DescriptionKey);
            return asset;
        });
    }

    private static Dictionary<string, FootprintProfileDefinition> CreateFootprintProfiles()
    {
        return new[]
        {
            ("vanguard", 0.58f, 0.82f, 1.25f, 0.9f, 1.25f, 5, 1.3f, BodySizeCategoryValue.Large, 2.15f),
            ("duelist", 0.45f, 0.68f, 1.3f, 0.95f, 1.45f, 6, 1.3f, BodySizeCategoryValue.Medium, 2.0f),
            ("ranger", 0.34f, 0.74f, 3.2f, 2.55f, 3.35f, 3, 2.2f, BodySizeCategoryValue.Small, 1.92f),
            ("mystic", 0.34f, 0.78f, 2.8f, 2.35f, 3.15f, 3, 2.0f, BodySizeCategoryValue.Small, 1.96f),
        }.ToDictionary(tuple => tuple.Item1, tuple => CreateAsset<FootprintProfileDefinition>($"{ResourcesRoot}/FootprintProfiles/footprint_{tuple.Item1}.asset", asset =>
        {
            asset.NavigationRadius = tuple.Item2;
            asset.SeparationRadius = tuple.Item3;
            asset.CombatReach = tuple.Item4;
            asset.PreferredRangeMin = tuple.Item5;
            asset.PreferredRangeMax = tuple.Item6;
            asset.EngagementSlotCount = tuple.Item7;
            asset.EngagementSlotRadius = tuple.Item8;
            asset.BodySizeCategory = tuple.Item9;
            asset.HeadAnchorHeight = tuple.Item10;
        }));
    }

    private static Dictionary<string, BehaviorProfileDefinition> CreateBehaviorProfiles()
    {
        return new[]
        {
            ("vanguard", 0.42f, 0.16f, 0.04f, 0.05f, 0.34f, 0.82f, 0.02f, 0.28f, 0.38f, 0.88f, 1.0f),
            ("duelist", 0.30f, 0.22f, 0.22f, 0.24f, 0.72f, 0.58f, 0.08f, 0.12f, 0.18f, 0.62f, 1.15f),
            ("ranger", 0.24f, 0.28f, 0.72f, 0.84f, 0.58f, 0.74f, 0.12f, 0.04f, 0.12f, 0.34f, 1.5f),
            ("mystic", 0.28f, 0.30f, 0.68f, 0.78f, 0.50f, 0.84f, 0.06f, 0.06f, 0.18f, 0.45f, 1.35f),
        }.ToDictionary(tuple => tuple.Item1, tuple => CreateAsset<BehaviorProfileDefinition>($"{ResourcesRoot}/BehaviorProfiles/behavior_{tuple.Item1}.asset", asset =>
        {
            asset.ReevaluationInterval = tuple.Item2;
            asset.RangeHysteresis = tuple.Item3;
            asset.RetreatBias = tuple.Item4;
            asset.MaintainRangeBias = tuple.Item5;
            asset.Opportunism = tuple.Item6;
            asset.Discipline = tuple.Item7;
            asset.DodgeChance = tuple.Item8;
            asset.BlockChance = tuple.Item9;
            asset.BlockMitigation = tuple.Item10;
            asset.Stability = tuple.Item11;
            asset.BlockCooldownSeconds = tuple.Item12;
        }));
    }

    private static Dictionary<string, MobilityProfileDefinition> CreateMobilityProfiles()
    {
        return new[]
        {
            ("vanguard", MobilityStyleValue.Dash, MobilityPurposeValue.Engage, 0.9f, 5.0f, 0f, 0.18f, 1.4f, 2.8f, 0f),
            ("duelist", MobilityStyleValue.Dash, MobilityPurposeValue.Engage, 1.15f, 4.2f, 0f, 0.16f, 1.3f, 3.0f, 0.2f),
            ("ranger", MobilityStyleValue.Roll, MobilityPurposeValue.MaintainRange, 1.45f, 3.4f, 0f, 0.22f, 0f, 1.45f, 0.68f),
            ("mystic", MobilityStyleValue.Blink, MobilityPurposeValue.Disengage, 1.85f, 4.4f, 0f, 0.30f, 0f, 1.35f, 0.35f),
        }.ToDictionary(tuple => tuple.Item1, tuple => CreateAsset<MobilityProfileDefinition>($"{ResourcesRoot}/MobilityProfiles/mobility_{tuple.Item1}.asset", asset =>
        {
            asset.Style = tuple.Item2;
            asset.Purpose = tuple.Item3;
            asset.Distance = tuple.Item4;
            asset.Cooldown = tuple.Item5;
            asset.CastTime = tuple.Item6;
            asset.Recovery = tuple.Item7;
            asset.TriggerMinDistance = tuple.Item8;
            asset.TriggerMaxDistance = tuple.Item9;
            asset.LateralBias = tuple.Item10;
        }));
    }

    private static Dictionary<string, SkillDefinitionAsset> CreateSkills()
    {
        return new[]
        {
            ("skill_power_strike", "Power Strike", "강타", "Heavy melee strike.", "강한 근접 일격.", SkillKindValue.Strike, 3f, 1f),
            ("skill_precision_shot", "Precision Shot", "정밀 사격", "Focused ranged shot.", "집중된 원거리 사격.", SkillKindValue.Strike, 2f, 2f),
            ("skill_minor_heal", "Minor Heal", "소회복", "Small targeted heal.", "단일 대상 소회복.", SkillKindValue.Heal, 4f, 2f),
        }.ToDictionary(tuple => tuple.Item1, tuple => CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{tuple.Item1}.asset", a =>
        {
            a.Id = tuple.Item1;
            a.NameKey = ContentLocalizationTables.BuildSkillNameKey(tuple.Item1);
            a.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(tuple.Item1);
            a.Kind = tuple.Item6;
            a.SlotKind = tuple.Item6 == SkillKindValue.Heal ? SkillSlotKindValue.UtilityActive : SkillSlotKindValue.CoreActive;
            a.DamageType = tuple.Item6 == SkillKindValue.Heal ? DamageTypeValue.Healing : DamageTypeValue.Physical;
            a.Delivery = tuple.Item1 == "skill_precision_shot" ? SkillDeliveryValue.Projectile : SkillDeliveryValue.Melee;
            a.TargetRule = tuple.Item6 == SkillKindValue.Heal ? SkillTargetRuleValue.LowestHpAlly : SkillTargetRuleValue.MostExposedEnemy;
            a.Power = tuple.Item7;
            a.Range = tuple.Item8;
            a.PowerFlat = tuple.Item7;
            a.PhysCoeff = tuple.Item6 == SkillKindValue.Heal ? 0f : 1f;
            a.MagCoeff = tuple.Item6 == SkillKindValue.Heal ? 0.8f : 0f;
            a.HealCoeff = tuple.Item6 == SkillKindValue.Heal ? 1f : 0f;
            a.HealthCoeff = 0f;
            a.CanCrit = tuple.Item1 == "skill_precision_shot";
            UpsertStringEntry(ContentLocalizationTables.Skills, a.NameKey, tuple.Item3, tuple.Item2);
            UpsertStringEntry(ContentLocalizationTables.Skills, a.DescriptionKey, tuple.Item5, tuple.Item4);
        }));
    }

    private static Dictionary<string, TraitPoolDefinition> CreateTraitPools()
    {
        var result = new Dictionary<string, TraitPoolDefinition>();
        foreach (var archetypeId in new[] { "warden", "guardian", "bulwark", "slayer", "raider", "reaver", "hunter", "scout", "marksman", "priest", "hexer", "shaman", "rift_stalker", "bastion_penitent", "pale_executor", "mirror_cantor" })
        {
            var path = $"{ResourcesRoot}/Traits/traitpool_{archetypeId}.asset";
            var asset = CreateAsset<TraitPoolDefinition>(path, a =>
            {
                a.Id = $"traitpool_{archetypeId}";
                a.ArchetypeId = archetypeId;
                a.PositiveTraits = new List<TraitEntry>
                {
                    MakeTrait(archetypeId, $"{archetypeId}_positive_brave", "Brave", "용맹", "phys_power", 2f),
                    MakeTrait(archetypeId, $"{archetypeId}_positive_sturdy", "Sturdy", "견고", "armor", 1f),
                    MakeTrait(archetypeId, $"{archetypeId}_positive_swift", "Swift", "민첩", "attack_speed", 1f),
                };
                a.NegativeTraits = new List<TraitEntry>
                {
                    MakeTrait(archetypeId, $"{archetypeId}_negative_frail", "Frail", "허약", "max_health", -3f),
                    MakeTrait(archetypeId, $"{archetypeId}_negative_clumsy", "Clumsy", "서툼", "phys_power", -1f),
                    MakeTrait(archetypeId, $"{archetypeId}_negative_slow", "Slow", "둔함", "attack_speed", -1f),
                };
            });
            PatchSerializedTraitPool(asset, asset.Id, asset.ArchetypeId, asset.PositiveTraits, asset.NegativeTraits);
            result[archetypeId] = asset;
        }
        return result;
    }

    private static Dictionary<string, UnitArchetypeDefinition> CreateArchetypes(
        Dictionary<string, RaceDefinition> races,
        Dictionary<string, ClassDefinition> classes,
        Dictionary<string, TraitPoolDefinition> traitPools,
        Dictionary<string, SkillDefinitionAsset> skills,
        IReadOnlyDictionary<string, FootprintProfileDefinition> footprintProfiles,
        IReadOnlyDictionary<string, BehaviorProfileDefinition> behaviorProfiles,
        IReadOnlyDictionary<string, MobilityProfileDefinition> mobilityProfiles)
    {
        var result = new Dictionary<string, UnitArchetypeDefinition>();
        result["warden"] = CreateArchetype("warden", "Warden", races["human"], classes["vanguard"], traitPools["warden"], skills["skill_power_strike"], footprintProfiles["vanguard"], behaviorProfiles["vanguard"], mobilityProfiles["vanguard"], 24, 5, 3, 3, 0, DeploymentAnchorValue.FrontCenter, TeamPostureTypeValue.HoldLine);
        result["guardian"] = CreateArchetype("guardian", "Guardian", races["undead"], classes["vanguard"], traitPools["guardian"], skills["skill_power_strike"], footprintProfiles["vanguard"], behaviorProfiles["vanguard"], mobilityProfiles["vanguard"], 26, 4, 4, 2, 0, DeploymentAnchorValue.FrontTop, TeamPostureTypeValue.ProtectCarry);
        result["slayer"] = CreateArchetype("slayer", "Slayer", races["human"], classes["duelist"], traitPools["slayer"], skills["skill_power_strike"], footprintProfiles["duelist"], behaviorProfiles["duelist"], mobilityProfiles["duelist"], 20, 7, 2, 4, 0, DeploymentAnchorValue.FrontBottom, TeamPostureTypeValue.StandardAdvance);
        result["raider"] = CreateArchetype("raider", "Raider", races["beastkin"], classes["duelist"], traitPools["raider"], skills["skill_power_strike"], footprintProfiles["duelist"], behaviorProfiles["duelist"], mobilityProfiles["duelist"], 19, 8, 1, 5, 0, DeploymentAnchorValue.FrontTop, TeamPostureTypeValue.CollapseWeakSide);
        result["hunter"] = CreateArchetype("hunter", "Hunter", races["human"], classes["ranger"], traitPools["hunter"], skills["skill_precision_shot"], footprintProfiles["ranger"], behaviorProfiles["ranger"], mobilityProfiles["ranger"], 18, 6, 2, 5, 0, DeploymentAnchorValue.BackTop, TeamPostureTypeValue.StandardAdvance);
        result["scout"] = CreateArchetype("scout", "Scout", races["beastkin"], classes["ranger"], traitPools["scout"], skills["skill_precision_shot"], footprintProfiles["ranger"], behaviorProfiles["ranger"], mobilityProfiles["ranger"], 17, 5, 2, 6, 0, DeploymentAnchorValue.BackBottom, TeamPostureTypeValue.CollapseWeakSide);
        result["priest"] = CreateArchetype("priest", "Priest", races["human"], classes["mystic"], traitPools["priest"], skills["skill_minor_heal"], footprintProfiles["mystic"], behaviorProfiles["mystic"], mobilityProfiles["mystic"], 18, 3, 2, 4, 5, DeploymentAnchorValue.BackCenter, TeamPostureTypeValue.ProtectCarry);
        result["hexer"] = CreateArchetype("hexer", "Hexer", races["undead"], classes["mystic"], traitPools["hexer"], skills["skill_minor_heal"], footprintProfiles["mystic"], behaviorProfiles["mystic"], mobilityProfiles["mystic"], 17, 4, 2, 4, 4, DeploymentAnchorValue.BackCenter, TeamPostureTypeValue.AllInBackline);
        result["rift_stalker"] = CreateArchetype("rift_stalker", "Rift Stalker", races["beastkin"], classes["ranger"], traitPools["rift_stalker"], skills["skill_precision_shot"], footprintProfiles["ranger"], behaviorProfiles["ranger"], mobilityProfiles["ranger"], 18, 6, 2, 6, 0, DeploymentAnchorValue.BackBottom, TeamPostureTypeValue.CollapseWeakSide, ArchetypeScopeValue.Specialist);
        result["bastion_penitent"] = CreateArchetype("bastion_penitent", "Bastion Penitent", races["human"], classes["vanguard"], traitPools["bastion_penitent"], skills["skill_power_strike"], footprintProfiles["vanguard"], behaviorProfiles["vanguard"], mobilityProfiles["vanguard"], 27, 4, 5, 2, 0, DeploymentAnchorValue.FrontCenter, TeamPostureTypeValue.ProtectCarry, ArchetypeScopeValue.Specialist);
        result["pale_executor"] = CreateArchetype("pale_executor", "Pale Executor", races["undead"], classes["duelist"], traitPools["pale_executor"], skills["skill_power_strike"], footprintProfiles["duelist"], behaviorProfiles["duelist"], mobilityProfiles["duelist"], 20, 8, 2, 4, 0, DeploymentAnchorValue.FrontBottom, TeamPostureTypeValue.CollapseWeakSide, ArchetypeScopeValue.Specialist);
        result["mirror_cantor"] = CreateArchetype("mirror_cantor", "Mirror Cantor", races["human"], classes["mystic"], traitPools["mirror_cantor"], skills["skill_minor_heal"], footprintProfiles["mystic"], behaviorProfiles["mystic"], mobilityProfiles["mystic"], 18, 3, 2, 4, 5, DeploymentAnchorValue.BackCenter, TeamPostureTypeValue.ProtectCarry, ArchetypeScopeValue.Specialist);
        return result;
    }

    private static UnitArchetypeDefinition CreateArchetype(string id, string name, RaceDefinition race, ClassDefinition @class, TraitPoolDefinition pool, SkillDefinitionAsset skill, FootprintProfileDefinition footprintProfile, BehaviorProfileDefinition behaviorProfile, MobilityProfileDefinition mobilityProfile, float hp, float atk, float def, float spd, float heal, DeploymentAnchorValue defaultAnchor, TeamPostureTypeValue preferredPosture, ArchetypeScopeValue scopeKind = ArchetypeScopeValue.Core)
    {
        return CreateAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{id}.asset", a =>
        {
            a.Id = id;
            a.NameKey = ContentLocalizationTables.BuildArchetypeNameKey(id);
            a.ScopeKind = scopeKind;
            a.Race = race;
            a.Class = @class;
            a.TraitPool = pool;
            a.Skills = new List<SkillDefinitionAsset> { skill };
            a.DefaultAnchor = defaultAnchor;
            a.PreferredTeamPosture = preferredPosture;
            a.RoleFamilyTag = @class.Id switch
            {
                "duelist" => "striker",
                _ => @class.Id
            };
            a.FootprintProfile = footprintProfile;
            a.BehaviorProfile = behaviorProfile;
            a.MobilityProfile = mobilityProfile;
            a.PrimaryWeaponFamilyTag = @class.Id switch
            {
                "vanguard" => "shield",
                "duelist" => "blade",
                "ranger" => "bow",
                "mystic" => "focus",
                _ => "general"
            };
            a.BaseMaxHealth = hp;
            a.BaseArmor = def;
            a.BasePhysPower = atk;
            a.BaseAttackSpeed = spd;
            a.BaseAttack = atk;
            a.BaseDefense = def;
            a.BaseSpeed = spd;
            a.BaseHealPower = heal;
            a.BaseMoveSpeed = @class.Id switch
            {
                "vanguard" => 1.65f,
                "duelist" => 2.05f,
                "ranger" => 1.85f,
                "mystic" => 1.7f,
                _ => 1.75f
            };
            a.BaseAttackRange = @class.Id switch
            {
                "ranger" => 3.2f,
                "mystic" => 2.8f,
                _ => 1.3f
            };
            a.BaseAggroRadius = @class.Id == "ranger" ? 8f : 7.2f;
            a.BaseAttackWindup = @class.Id == "ranger" ? 0.18f : 0.22f;
            a.BaseAttackCooldown = @class.Id == "duelist" ? 0.85f : 1.0f;
            a.BaseLeashDistance = @class.Id switch
            {
                "ranger" => 4.6f,
                "mystic" => 4.8f,
                _ => 5.2f
            };
            a.BaseTargetSwitchDelay = @class.Id == "ranger" ? 0.30f : 0.40f;
            a.TacticPreset = BuildTacticPreset(@class.Id, skill);
            UpsertStringEntry(ContentLocalizationTables.Archetypes, a.NameKey, ResolveArchetypeKoName(id), name);
        });
    }

    private static Dictionary<string, CharacterDefinition> CreateCharacters(
        IReadOnlyDictionary<string, UnitArchetypeDefinition> archetypes,
        IReadOnlyDictionary<string, RoleInstructionDefinition> roleInstructions)
    {
        var result = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);

        foreach (var archetypeId in new[] { "warden", "guardian", "bulwark", "slayer", "raider", "reaver", "hunter", "scout", "marksman", "priest", "hexer", "shaman", "rift_stalker", "bastion_penitent", "pale_executor", "mirror_cantor" })
        {
            if (!archetypes.TryGetValue(archetypeId, out var archetype))
            {
                continue;
            }

            var defaultRoleInstructionId = ResolveDefaultRoleInstructionId(archetype.Class != null ? archetype.Class.Id : string.Empty, (DeploymentAnchorId)(int)archetype.DefaultAnchor);
            roleInstructions.TryGetValue(defaultRoleInstructionId, out var roleInstruction);

            var asset = CreateAsset<CharacterDefinition>($"{ResourcesRoot}/Characters/character_{archetypeId}.asset", character =>
            {
                character.Id = archetypeId;
                character.NameKey = ContentLocalizationTables.BuildCharacterNameKey(archetypeId);
                character.DescriptionKey = ContentLocalizationTables.BuildCharacterDescriptionKey(archetypeId);
                character.Race = archetype.Race;
                character.Class = archetype.Class;
                character.DefaultArchetype = archetype;
                character.DefaultRoleInstruction = roleInstruction;
                UpsertStringEntry(ContentLocalizationTables.Characters, character.NameKey, ResolveCharacterKoName(archetypeId), ResolveCharacterEnName(archetypeId));
                UpsertStringEntry(ContentLocalizationTables.Characters, character.DescriptionKey, ResolveCharacterKoDescription(archetypeId), ResolveCharacterEnDescription(archetypeId));
            });

            result[archetypeId] = asset;
        }

        return result;
    }

    private static void EnsureRoleGlossaryLocalization(IReadOnlyDictionary<string, RoleInstructionDefinition> roleInstructions)
    {
        var entries = new (string Id, string Ko, string En)[]
        {
            ("anchor", "전열 고정", "Anchor"),
            ("bruiser", "난전", "Bruiser"),
            ("carry", "화력", "Carry"),
            ("support", "지원", "Support"),
            ("frontline", "전열", "Frontline"),
            ("backline", "후열", "Backline"),
            ("striker", "타격", "Striker"),
        };

        foreach (var entry in entries)
        {
            var key = ContentLocalizationTables.BuildRoleNameKey(entry.Id);
            UpsertStringEntry(ContentLocalizationTables.Roles, key, entry.Ko, entry.En);
        }

        foreach (var roleInstruction in roleInstructions.Values)
        {
            roleInstruction.NameKey = ContentLocalizationTables.BuildRoleNameKey(roleInstruction.Id);
            UpsertStringEntry(
                ContentLocalizationTables.Roles,
                roleInstruction.NameKey,
                RoleGlossary.GetLocalizedRoleTagFallback(roleInstruction.RoleTag, "ko"),
                RoleGlossary.GetLocalizedRoleTagFallback(roleInstruction.RoleTag, "en"));
            EditorUtility.SetDirty(roleInstruction);
        }
    }

    private static string ResolveDefaultRoleInstructionId(string classId, DeploymentAnchorId anchor)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => anchor.IsFrontRow() ? "frontline" : "backline",
        };
    }

    private static void PatchLaunchFloorArchetypes(
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        IReadOnlyDictionary<string, StableTagDefinition> tags,
        IReadOnlyDictionary<string, FootprintProfileDefinition> footprintProfiles,
        IReadOnlyDictionary<string, BehaviorProfileDefinition> behaviorProfiles,
        IReadOnlyDictionary<string, MobilityProfileDefinition> mobilityProfiles)
    {
        var definitions = new[]
        {
            new { Id = "warden", ScopeKind = ArchetypeScopeValue.Core, RaceId = "human", ClassId = "vanguard", TraitPoolId = "warden", DefaultSkillIds = new[] { "skill_guardian_core", "skill_warden_utility", "skill_vanguard_passive_1", "skill_vanguard_support_1" } },
            new { Id = "guardian", ScopeKind = ArchetypeScopeValue.Core, RaceId = "undead", ClassId = "vanguard", TraitPoolId = "guardian", DefaultSkillIds = new[] { "skill_guardian_core", "skill_guardian_utility", "skill_vanguard_passive_2", "skill_vanguard_support_2" } },
            new { Id = "bulwark", ScopeKind = ArchetypeScopeValue.Core, RaceId = "beastkin", ClassId = "vanguard", TraitPoolId = "bulwark", DefaultSkillIds = new[] { "skill_bulwark_core", "skill_guardian_core", "skill_warden_utility", "support_purifying" } },
            new { Id = "slayer", ScopeKind = ArchetypeScopeValue.Core, RaceId = "human", ClassId = "duelist", TraitPoolId = "slayer", DefaultSkillIds = new[] { "skill_slayer_core", "skill_slayer_utility", "skill_duelist_passive_1", "skill_duelist_support_1" } },
            new { Id = "raider", ScopeKind = ArchetypeScopeValue.Core, RaceId = "beastkin", ClassId = "duelist", TraitPoolId = "raider", DefaultSkillIds = new[] { "skill_raider_core", "skill_raider_utility", "skill_duelist_passive_2", "skill_duelist_support_2" } },
            new { Id = "reaver", ScopeKind = ArchetypeScopeValue.Core, RaceId = "undead", ClassId = "duelist", TraitPoolId = "reaver", DefaultSkillIds = new[] { "skill_reaver_core", "skill_raider_core", "skill_reaver_utility", "support_swift" } },
            new { Id = "hunter", ScopeKind = ArchetypeScopeValue.Core, RaceId = "human", ClassId = "ranger", TraitPoolId = "hunter", DefaultSkillIds = new[] { "skill_precision_shot", "skill_hunter_utility", "skill_ranger_passive_1", "skill_ranger_support_1" } },
            new { Id = "scout", ScopeKind = ArchetypeScopeValue.Core, RaceId = "beastkin", ClassId = "ranger", TraitPoolId = "scout", DefaultSkillIds = new[] { "skill_scout_core", "skill_scout_utility", "skill_ranger_passive_2", "skill_ranger_support_2" } },
            new { Id = "marksman", ScopeKind = ArchetypeScopeValue.Core, RaceId = "undead", ClassId = "ranger", TraitPoolId = "marksman", DefaultSkillIds = new[] { "skill_marksman_core", "skill_hunter_utility", "skill_marksman_utility", "support_longshot" } },
            new { Id = "priest", ScopeKind = ArchetypeScopeValue.Core, RaceId = "human", ClassId = "mystic", TraitPoolId = "priest", DefaultSkillIds = new[] { "skill_priest_core", "skill_minor_heal", "skill_mystic_passive_1", "skill_mystic_support_1" } },
            new { Id = "hexer", ScopeKind = ArchetypeScopeValue.Core, RaceId = "undead", ClassId = "mystic", TraitPoolId = "hexer", DefaultSkillIds = new[] { "skill_hexer_core", "skill_hexer_utility", "skill_mystic_passive_2", "skill_mystic_support_2" } },
            new { Id = "shaman", ScopeKind = ArchetypeScopeValue.Core, RaceId = "beastkin", ClassId = "mystic", TraitPoolId = "shaman", DefaultSkillIds = new[] { "skill_shaman_core", "skill_priest_core", "skill_shaman_utility", "support_siphon" } },
            new { Id = "rift_stalker", ScopeKind = ArchetypeScopeValue.Specialist, RaceId = "beastkin", ClassId = "ranger", TraitPoolId = "rift_stalker", DefaultSkillIds = new[] { "skill_scout_core", "skill_scout_utility", "skill_ranger_passive_2", "support_swift" } },
            new { Id = "bastion_penitent", ScopeKind = ArchetypeScopeValue.Specialist, RaceId = "human", ClassId = "vanguard", TraitPoolId = "bastion_penitent", DefaultSkillIds = new[] { "skill_bulwark_core", "skill_warden_utility", "skill_vanguard_passive_2", "support_anchored" } },
            new { Id = "pale_executor", ScopeKind = ArchetypeScopeValue.Specialist, RaceId = "undead", ClassId = "duelist", TraitPoolId = "pale_executor", DefaultSkillIds = new[] { "skill_reaver_core", "skill_raider_utility", "skill_duelist_passive_2", "support_executioner" } },
            new { Id = "mirror_cantor", ScopeKind = ArchetypeScopeValue.Specialist, RaceId = "human", ClassId = "mystic", TraitPoolId = "mirror_cantor", DefaultSkillIds = new[] { "skill_priest_core", "skill_hexer_utility", "skill_mystic_passive_1", "support_purifying" } },
        };

        foreach (var definition in definitions)
        {
            var path = $"{ResourcesRoot}/Archetypes/archetype_{definition.Id}.asset";
            var asset = LoadDefinition<UnitArchetypeDefinition>(path);
            if (asset == null)
            {
                continue;
            }

            asset.ScopeKind = definition.ScopeKind;
            asset.Race = races[definition.RaceId];
            asset.Class = classes[definition.ClassId];
            asset.TraitPool = traitPools[definition.TraitPoolId];
            asset.RoleFamilyTag = definition.ClassId switch
            {
                "duelist" => "striker",
                _ => definition.ClassId
            };
            asset.FootprintProfile = footprintProfiles[definition.ClassId];
            asset.BehaviorProfile = behaviorProfiles[definition.ClassId];
            asset.MobilityProfile = mobilityProfiles[definition.ClassId];
            asset.PrimaryWeaponFamilyTag = definition.ClassId switch
            {
                "vanguard" => "shield",
                "duelist" => "blade",
                "ranger" => "bow",
                "mystic" => "focus",
                _ => asset.PrimaryWeaponFamilyTag
            };

            if (string.IsNullOrWhiteSpace(asset.RoleTag) || string.Equals(asset.RoleTag, "auto", StringComparison.Ordinal))
            {
                asset.RoleTag = definition.ClassId switch
                {
                    "vanguard" => "anchor",
                    "duelist" => "bruiser",
                    "ranger" => "carry",
                    "mystic" => "support",
                    _ => "auto"
                };
            }

            if (asset.Skills == null
                || asset.Skills.Count != 4
                || asset.Skills.Any(skill => skill == null))
            {
                var resolvedSkills = definition.DefaultSkillIds
                    .Where(skills.ContainsKey)
                    .Select(skillId => skills[skillId])
                    .ToList();
                if (resolvedSkills.Count == 4)
                {
                    asset.Skills = resolvedSkills;
                }
                else
                {
                    Debug.LogWarning($"SM launch-floor archetype patch skipped incomplete skill catalog for {definition.Id}. Resolved={resolvedSkills.Count}/4");
                }
            }

            if (asset.TacticPreset == null || asset.TacticPreset.Count == 0)
            {
                var tacticSkill = asset.Skills.FirstOrDefault(skill => skill != null);
                if (tacticSkill != null)
                {
                    asset.TacticPreset = BuildTacticPreset(definition.ClassId, tacticSkill);
                }
            }

            ApplyLoopBRecruitmentMetadata(asset, definition.Id, skills, tags);
            ApplyLoopCArchetypeGovernance(asset);
            if (definition.ScopeKind == ArchetypeScopeValue.Specialist)
            {
                EditorUtility.SetDirty(asset);
                PatchArchetypeSkillFallbackYaml(path, definition.ClassId, definition.DefaultSkillIds);
                continue;
            }

            EditorUtility.SetDirty(asset);
        }
    }

    private static List<TacticPresetEntry> BuildTacticPreset(string classId, SkillDefinitionAsset skill)
    {
        if (classId == "mystic")
        {
            return new List<TacticPresetEntry>
            {
                new() { Priority = 0, ConditionType = TacticConditionTypeValue.AllyHpBelow, Threshold = 0.6f, ActionType = BattleActionTypeValue.ActiveSkill, TargetSelector = TargetSelectorTypeValue.LowestHpAlly, Skill = skill },
                new() { Priority = 1, ConditionType = TacticConditionTypeValue.EnemyExposed, Threshold = 1.5f, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.MostExposedEnemy },
                new() { Priority = 2, ConditionType = TacticConditionTypeValue.Fallback, Threshold = 0f, ActionType = BattleActionTypeValue.WaitDefend, TargetSelector = TargetSelectorTypeValue.Self },
            };
        }

        return new List<TacticPresetEntry>
        {
            new() { Priority = 0, ConditionType = TacticConditionTypeValue.EnemyExposed, Threshold = 1.5f, ActionType = BattleActionTypeValue.ActiveSkill, TargetSelector = TargetSelectorTypeValue.MostExposedEnemy, Skill = skill },
            new() { Priority = 1, ConditionType = TacticConditionTypeValue.LowestHpEnemy, Threshold = 0f, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.LowestHpEnemy },
            new() { Priority = 2, ConditionType = TacticConditionTypeValue.Fallback, Threshold = 0f, ActionType = BattleActionTypeValue.WaitDefend, TargetSelector = TargetSelectorTypeValue.Self },
        };
    }

    private static void ApplyLoopBRecruitmentMetadata(
        UnitArchetypeDefinition archetype,
        string archetypeId,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        archetype.RecruitTier = archetypeId switch
        {
            "guardian" or "raider" or "bulwark" or "reaver" or "marksman" => RecruitTier.Rare,
            "rift_stalker" or "bastion_penitent" or "pale_executor" or "mirror_cantor" => RecruitTier.Rare,
            "hexer" or "shaman" => RecruitTier.Epic,
            _ => RecruitTier.Common,
        };
        archetype.IsRecruitable = true;
        archetype.IsSummonOnly = false;
        archetype.IsEventOnly = false;
        archetype.IsBossOnly = false;
        archetype.IsUnreleased = false;
        archetype.IsTestOnly = false;

        var planTagIds = archetypeId switch
        {
            "warden" => new[] { "vanguard", "frontline", "guard", "physical" },
            "guardian" => new[] { "vanguard", "frontline", "guard", "support" },
            "bulwark" => new[] { "vanguard", "frontline", "shield_skill", "support" },
            "slayer" => new[] { "duelist", "frontline", "strike", "execute" },
            "raider" => new[] { "duelist", "frontline", "mark", "physical" },
            "reaver" => new[] { "duelist", "frontline", "burst", "execute" },
            "hunter" => new[] { "ranger", "backline", "projectile", "physical" },
            "scout" => new[] { "ranger", "backline", "mark", "exposed" },
            "marksman" => new[] { "ranger", "backline", "pierce", "physical" },
            "priest" => new[] { "mystic", "backline", "support", "heal" },
            "hexer" => new[] { "mystic", "backline", "magical", "silence" },
            "shaman" => new[] { "mystic", "backline", "magical", "burn" },
            "rift_stalker" => new[] { "ranger", "backline", "dash", "exposed" },
            "bastion_penitent" => new[] { "vanguard", "frontline", "guard", "shield_skill" },
            "pale_executor" => new[] { "duelist", "frontline", "mark", "execute" },
            "mirror_cantor" => new[] { "mystic", "backline", "cleanse", "support" },
            _ => new[] { archetype.Class?.Id ?? string.Empty },
        };
        var scoutTagIds = archetypeId switch
        {
            "warden" or "guardian" or "bulwark" => new[] { "frontline", "guard", "vanguard" },
            "slayer" or "raider" or "reaver" => new[] { "frontline", "physical", "duelist" },
            "hunter" or "scout" or "marksman" => new[] { "backline", "physical", "ranger" },
            "priest" => new[] { "backline", "support", "heal", "mystic" },
            "hexer" or "shaman" => new[] { "backline", "magical", "mystic" },
            "rift_stalker" => new[] { "backline", "exposed", "ranger" },
            "bastion_penitent" => new[] { "frontline", "guard", "vanguard" },
            "pale_executor" => new[] { "frontline", "execute", "duelist" },
            "mirror_cantor" => new[] { "backline", "cleanse", "mystic" },
            _ => Array.Empty<string>(),
        };

        archetype.RecruitPlanTags = ResolveTags(tags, planTagIds);
        archetype.ScoutBiasTags = ResolveTags(tags, scoutTagIds);

        var activePoolIds = ResolveFlexUtilitySkillIds(archetype.Class?.Id ?? string.Empty);
        var passivePoolIds = ResolveFlexSupportSkillIds(archetype.Class?.Id ?? string.Empty);

        archetype.FlexUtilitySkillPool = ResolveSkillPool(skills, activePoolIds);
        archetype.FlexSupportSkillPool = ResolveSkillPool(skills, passivePoolIds);
        archetype.RecruitFlexActivePool = archetype.FlexUtilitySkillPool.ToList();
        archetype.RecruitFlexPassivePool = archetype.FlexSupportSkillPool.ToList();
        archetype.RecruitBannedPairings = BuildRecruitBannedPairings(archetype.Class?.Id ?? string.Empty);
        archetype.Loadout ??= new UnitLoadoutDefinition();
        archetype.Loadout.SignatureActive = archetype.Loadout.SignatureActive
                                            ?? archetype.LockedSignatureActiveSkill
                                            ?? archetype.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.CoreActive);
        archetype.Loadout.FlexActive = archetype.Loadout.FlexActive
                                       ?? archetype.FlexUtilitySkillPool.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive)
                                       ?? archetype.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive);
    }

    private static string[] ResolveFlexUtilitySkillIds(string classId)
    {
        return classId switch
        {
            "vanguard" => new[] { "skill_guardian_utility", "skill_warden_utility" },
            "duelist" => new[] { "skill_slayer_utility", "skill_raider_utility", "skill_reaver_utility" },
            "ranger" => new[] { "skill_hunter_utility", "skill_marksman_utility", "skill_scout_utility" },
            "mystic" => new[] { "skill_minor_heal", "skill_hexer_utility", "skill_shaman_utility" },
            _ => Array.Empty<string>(),
        };
    }

    private static string[] ResolveFlexSupportSkillIds(string classId)
    {
        return classId switch
        {
            "vanguard" => new[] { "skill_vanguard_support_1", "skill_vanguard_support_2", "support_guarded", "support_purifying", "support_anchored" },
            "duelist" => new[] { "skill_duelist_support_1", "skill_duelist_support_2", "support_executioner", "support_swift", "support_brutal" },
            "ranger" => new[] { "skill_ranger_support_1", "skill_ranger_support_2", "support_longshot", "support_hunter_mark", "support_piercing", "support_swift" },
            "mystic" => new[] { "skill_mystic_support_1", "skill_mystic_support_2", "support_purifying", "support_siphon", "support_echo", "support_lingering" },
            _ => Array.Empty<string>(),
        };
    }

    private static List<RecruitBannedPairingDefinition> BuildRecruitBannedPairings(string classId)
    {
        return classId switch
        {
            "vanguard" => new List<RecruitBannedPairingDefinition>
            {
                new() { FlexActiveId = "skill_warden_utility", FlexPassiveId = "support_anchored" },
            },
            "duelist" => new List<RecruitBannedPairingDefinition>
            {
                new() { FlexActiveId = "skill_reaver_utility", FlexPassiveId = "support_brutal" },
            },
            "ranger" => new List<RecruitBannedPairingDefinition>
            {
                new() { FlexActiveId = "skill_scout_utility", FlexPassiveId = "support_longshot" },
            },
            "mystic" => new List<RecruitBannedPairingDefinition>
            {
                new() { FlexActiveId = "skill_minor_heal", FlexPassiveId = "support_siphon" },
            },
            _ => new List<RecruitBannedPairingDefinition>(),
        };
    }

    private static List<SkillDefinitionAsset> ResolveSkillPool(
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        IEnumerable<string> skillIds)
    {
        return skillIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && skills.ContainsKey(id))
            .Select(id => skills[id])
            .Distinct()
            .ToList();
    }

    private static void PatchLoopBRecruitmentSkills(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        PatchRecruitSkillMetadata("skill_guardian_core", tags, "guard_signature", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "guard", "support" }, new[] { "frontline", "vanguard" });
        PatchRecruitSkillMetadata("skill_bulwark_core", tags, "bulwark_signature", string.Empty, new[] { "vanguard", "frontline", "shield_skill" }, new[] { "vanguard", "support", "shield_skill" }, new[] { "frontline", "vanguard" });
        PatchRecruitSkillMetadata("skill_slayer_core", tags, "slayer_signature", string.Empty, new[] { "duelist", "frontline", "strike" }, new[] { "duelist", "execute", "physical" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_raider_core", tags, "raider_signature", string.Empty, new[] { "duelist", "frontline", "mark" }, new[] { "duelist", "mark", "physical" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_hexer_core", tags, "hexer_signature", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "silence" }, new[] { "backline", "magical" });
        PatchRecruitSkillMetadata("skill_priest_core", tags, "priest_signature", string.Empty, new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "heal" }, new[] { "backline", "support" });
        PatchRecruitSkillMetadata("skill_shaman_core", tags, "shaman_signature", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "zone" }, new[] { "backline", "magical" });

        PatchRecruitSkillMetadata("skill_warden_utility", tags, "guard_cleanse", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "cleanse" }, new[] { "frontline", "support" });
        PatchRecruitSkillMetadata("skill_guardian_utility", tags, "guard_rally", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "physical" }, new[] { "frontline", "support" });
        PatchRecruitSkillMetadata("skill_slayer_utility", tags, "bleed_followup", string.Empty, new[] { "duelist", "frontline", "execute" }, new[] { "duelist", "physical", "execute" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_raider_utility", tags, "mark_followup", string.Empty, new[] { "duelist", "frontline", "mark" }, new[] { "duelist", "physical", "mark" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_reaver_utility", tags, "burst_followup", string.Empty, new[] { "duelist", "frontline", "burst" }, new[] { "duelist", "physical", "burst" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_hunter_utility", tags, "hunter_mark", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "mark", "physical" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("skill_marksman_utility", tags, "marksman_pierce", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "pierce", "physical" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("skill_scout_utility", tags, "scout_exposed", string.Empty, new[] { "ranger", "backline", "mark" }, new[] { "ranger", "exposed", "physical" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("skill_minor_heal", tags, "minor_heal", string.Empty, new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "heal" }, new[] { "backline", "support" });
        PatchRecruitSkillMetadata("skill_hexer_utility", tags, "hexer_silence", string.Empty, new[] { "mystic", "backline", "silence" }, new[] { "mystic", "magical", "silence" }, new[] { "backline", "magical" });
        PatchRecruitSkillMetadata("skill_shaman_utility", tags, "shaman_zone", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "zone" }, new[] { "backline", "magical" });

        PatchRecruitSkillMetadata("skill_vanguard_support_1", tags, "guard_support", string.Empty, new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "guard" }, new[] { "frontline", "support" });
        PatchRecruitSkillMetadata("skill_vanguard_support_2", tags, "bulwark_support", string.Empty, new[] { "vanguard", "frontline", "shield_skill" }, new[] { "vanguard", "support", "shield_skill" }, new[] { "frontline", "support" });
        PatchRecruitSkillMetadata("skill_duelist_support_1", tags, "slayer_support", string.Empty, new[] { "duelist", "frontline", "execute" }, new[] { "duelist", "physical", "execute" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_duelist_support_2", tags, "raider_support", string.Empty, new[] { "duelist", "frontline", "mark" }, new[] { "duelist", "physical", "mark" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("skill_ranger_support_1", tags, "hunter_support", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "mark" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("skill_ranger_support_2", tags, "scout_support", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "exposed" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("skill_mystic_support_1", tags, "priest_support", string.Empty, new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "heal" }, new[] { "backline", "support" });
        PatchRecruitSkillMetadata("skill_mystic_support_2", tags, "hexer_support", string.Empty, new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "silence" }, new[] { "backline", "magical" });
        PatchRecruitSkillMetadata("support_guarded", tags, "guard_signature", "vanguard_guard", new[] { "vanguard", "frontline", "guard" }, new[] { "vanguard", "support", "guard" }, new[] { "frontline", "support" });
        PatchRecruitSkillMetadata("support_anchored", tags, "anchored_support", "vanguard_guard", new[] { "vanguard", "frontline", "shield_skill" }, new[] { "vanguard", "support", "shield_skill" }, new[] { "frontline", "support" });
        PatchRecruitSkillMetadata("support_executioner", tags, "executioner_support", "duelist_stance", new[] { "duelist", "frontline", "execute" }, new[] { "duelist", "physical", "execute" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("support_brutal", tags, "brutal_support", "duelist_stance", new[] { "duelist", "frontline", "strike" }, new[] { "duelist", "physical", "burst" }, new[] { "frontline", "physical" });
        PatchRecruitSkillMetadata("support_longshot", tags, "longshot_support", "ranger_tempo", new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "pierce" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("support_hunter_mark", tags, "hunter_mark_support", "ranger_tempo", new[] { "ranger", "backline", "mark" }, new[] { "ranger", "physical", "mark" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("support_piercing", tags, "piercing_support", string.Empty, new[] { "ranger", "backline", "pierce" }, new[] { "ranger", "physical", "pierce" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("support_swift", tags, "swift_support", string.Empty, new[] { "ranger", "backline", "projectile" }, new[] { "ranger", "physical", "mark" }, new[] { "backline", "physical" });
        PatchRecruitSkillMetadata("support_purifying", tags, "priest_signature", "mystic_alignment", new[] { "mystic", "backline", "heal" }, new[] { "mystic", "support", "cleanse" }, new[] { "backline", "support" });
        PatchRecruitSkillMetadata("support_siphon", tags, "siphon_support", "mystic_alignment", new[] { "mystic", "backline", "burn" }, new[] { "mystic", "magical", "burn" }, new[] { "backline", "magical" });
        PatchRecruitSkillMetadata("support_echo", tags, "echo_support", string.Empty, new[] { "mystic", "backline", "zone" }, new[] { "mystic", "support", "zone" }, new[] { "backline", "support" });
        PatchRecruitSkillMetadata("support_lingering", tags, "lingering_support", string.Empty, new[] { "mystic", "backline", "zone" }, new[] { "mystic", "magical", "zone" }, new[] { "backline", "magical" });
    }

    private static void PatchRecruitSkillMetadata(
        string skillId,
        IReadOnlyDictionary<string, StableTagDefinition> tags,
        string effectFamilyId,
        string mutuallyExclusiveGroupId,
        IEnumerable<string> nativeTagIds,
        IEnumerable<string> planTagIds,
        IEnumerable<string> scoutTagIds)
    {
        var skill = LoadDefinition<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{skillId}.asset");
        if (skill == null)
        {
            return;
        }

        skill.EffectFamilyId = effectFamilyId;
        skill.MutuallyExclusiveGroupId = mutuallyExclusiveGroupId;
        skill.RecruitNativeTags = ResolveTags(tags, nativeTagIds);
        skill.RecruitPlanTags = ResolveTags(tags, planTagIds);
        skill.RecruitScoutTags = ResolveTags(tags, scoutTagIds);
        EditorUtility.SetDirty(skill);
    }

    private static void CreateAugments()
    {
        var definitions = new (string id, string enName, string koName, string enDesc, string koDesc, ContentRarity rarity, bool permanent, string stat, float value)[]
        {
            ("augment_silver_guard", "Guard Instinct", "수비 본능", "Improves defensive stability.", "방어 안정성을 높입니다.", ContentRarity.Common, false, "armor", 1f),
            ("augment_silver_focus", "Focus", "집중", "Sharpens offensive focus.", "공격 집중도를 높입니다.", ContentRarity.Common, false, "phys_power", 1f),
            ("augment_silver_stride", "Stride", "질주", "Improves battle tempo.", "전투 템포를 높입니다.", ContentRarity.Common, false, "attack_speed", 1f),
            ("augment_silver_clarity", "Clarity", "명료", "Improves control resistance.", "제어 저항을 소폭 높입니다.", ContentRarity.Common, false, "resist", 1f),
            ("augment_silver_reach", "Reach", "사거리", "Improves threat reach.", "위협 도달 거리를 소폭 늘립니다.", ContentRarity.Common, false, "attack_range", 0.2f),
            ("augment_gold_bastion", "Bastion", "보루", "Boosts frontline endurance.", "전열 생존력을 강화합니다.", ContentRarity.Rare, false, "max_health", 4f),
            ("augment_gold_fury", "Fury", "격노", "Boosts offensive output.", "공격 출력을 강화합니다.", ContentRarity.Rare, false, "phys_power", 2f),
            ("augment_gold_mending", "Mending", "치유술", "Boosts healing throughput.", "회복 효율을 강화합니다.", ContentRarity.Rare, false, "heal_power", 2f),
            ("augment_gold_ward", "Ward", "결계", "Boosts mixed defensive stability.", "복합 방어 안정성을 강화합니다.", ContentRarity.Rare, false, "resist", 2f),
            ("augment_gold_haste", "Haste", "가속", "Improves skill cadence.", "기술 순환 속도를 높입니다.", ContentRarity.Rare, false, "cooldown_recovery", 0.08f),
            ("augment_platinum_overrun", "Overrun", "돌파", "Heavily boosts attack.", "공격력을 크게 강화합니다.", ContentRarity.Epic, false, "phys_power", 3f),
            ("augment_platinum_wall", "Wall", "철벽", "Heavily boosts defense.", "방어력을 크게 강화합니다.", ContentRarity.Epic, false, "armor", 3f),
            ("augment_platinum_surge", "Surge", "급류", "Heavily boosts speed.", "속도를 크게 강화합니다.", ContentRarity.Epic, false, "attack_speed", 2f),
            ("augment_platinum_clarity", "Absolute Clarity", "완전한 명료", "Heavily improves arcane output.", "마법 출력을 크게 강화합니다.", ContentRarity.Epic, false, "mag_power", 3f),
            ("augment_platinum_tenacity", "Unbroken Tenacity", "불굴", "Heavily improves control stability.", "제어 안정성을 크게 강화합니다.", ContentRarity.Epic, false, "tenacity", 0.2f),
            ("augment_perm_legacy_blade", "Legacy Blade", "유산의 검", "Permanent attack bonus.", "영구 공격 보너스.", ContentRarity.Epic, true, "phys_power", 1f),
            ("augment_perm_legacy_hide", "Legacy Hide", "유산의 가죽", "Permanent defense bonus.", "영구 방어 보너스.", ContentRarity.Epic, true, "armor", 1f),
            ("augment_perm_legacy_grace", "Legacy Grace", "유산의 은총", "Permanent speed bonus.", "영구 속도 보너스.", ContentRarity.Epic, true, "attack_speed", 1f),
            ("augment_perm_legacy_crown", "Legacy Crown", "유산의 관", "Permanent resistance bonus.", "영구 저항 보너스.", ContentRarity.Epic, true, "resist", 1f),
            ("augment_perm_legacy_lantern", "Legacy Lantern", "유산의 등불", "Permanent support bonus.", "영구 지원 보너스.", ContentRarity.Epic, true, "heal_power", 1f),
            ("augment_perm_legacy_spur", "Legacy Spur", "유산의 박차", "Permanent mobility bonus.", "영구 기동 보너스.", ContentRarity.Epic, true, "move_speed", 0.04f),
        };

        foreach (var d in definitions)
        {
            CreateAsset<AugmentDefinition>($"{ResourcesRoot}/Augments/{d.id}.asset", a =>
            {
                a.Id = d.id;
                a.NameKey = ContentLocalizationTables.BuildAugmentNameKey(d.id);
                a.DescriptionKey = ContentLocalizationTables.BuildAugmentDescriptionKey(d.id);
                a.FamilyId = d.permanent ? "permanent_legacy" : d.rarity.ToString().ToLowerInvariant();
                a.Rarity = d.rarity;
                a.IsPermanent = d.permanent;
                a.Modifiers = new List<SerializableStatModifier>
                {
                    new() { StatId = d.stat, Value = d.value }
                };
                UpsertStringEntry(ContentLocalizationTables.Augments, a.NameKey, d.koName, d.enName);
                UpsertStringEntry(ContentLocalizationTables.Augments, a.DescriptionKey, d.koDesc, d.enDesc);
            });
        }
    }

    private static void CreateItems()
    {
        var items = new (string id, string enName, string koName, string enDesc, string koDesc, ItemSlotType slot, string stat, float value)[]
        {
            ("item_iron_sword", "Iron Sword", "철검", "Reliable frontline weapon.", "믿을 수 있는 전열 무기.", ItemSlotType.Weapon, "phys_power", 2f),
            ("item_bone_blade", "Bone Blade", "뼈칼날", "Aggressive melee blade.", "공격적인 근접 무기.", ItemSlotType.Weapon, "phys_power", 2f),
            ("item_leather_armor", "Leather Armor", "가죽 갑옷", "Light defensive armor.", "가벼운 방어구.", ItemSlotType.Armor, "armor", 1f),
            ("item_bone_plate", "Bone Plate", "골판 갑옷", "Bulky endurance armor.", "버티기에 강한 방어구.", ItemSlotType.Armor, "max_health", 3f),
            ("item_lucky_charm", "Lucky Charm", "행운 부적", "Tempo-focused accessory.", "템포 중심 장신구.", ItemSlotType.Accessory, "attack_speed", 1f),
            ("item_prayer_bead", "Prayer Bead", "기도 구슬", "Support accessory for healing.", "회복 지원용 장신구.", ItemSlotType.Accessory, "heal_power", 2f),
            ("item_rift_bow", "Rift Bow", "균열궁", "Mobile ranger weapon.", "기동 사수용 무기.", ItemSlotType.Weapon, "phys_power", 2f),
            ("item_penitent_shield", "Penitent Shield", "참회의 방패", "Defensive vanguard weapon.", "방어형 선봉 무기.", ItemSlotType.Weapon, "armor", 1f),
            ("item_cantor_focus", "Cantor Focus", "성가 초점구", "Support mystic focus.", "지원 신비술사용 초점구.", ItemSlotType.Weapon, "mag_power", 2f),
            ("item_layered_armor", "Layered Armor", "겹갑옷", "General-purpose layered protection.", "범용 겹방어구.", ItemSlotType.Armor, "armor", 1f),
            ("item_wayfinder_trinket", "Wayfinder Trinket", "길잡이 장신구", "Mobility accessory for repositioning.", "재배치용 기동 장신구.", ItemSlotType.Accessory, "move_speed", 0.08f),
            ("item_oath_bead", "Oath Bead", "맹세 구슬", "Support accessory for steady recovery.", "안정 회복 지원 장신구.", ItemSlotType.Accessory, "heal_power", 1f),
        };

        foreach (var d in items)
        {
            CreateAsset<ItemBaseDefinition>($"{ResourcesRoot}/Items/{d.id}.asset", a =>
            {
                a.Id = d.id;
                a.NameKey = ContentLocalizationTables.BuildItemNameKey(d.id);
                a.DescriptionKey = ContentLocalizationTables.BuildItemDescriptionKey(d.id);
                a.SlotType = d.slot;
                a.IdentityKind = ItemIdentityValue.Baseline;
                a.BudgetBand = d.slot switch
                {
                    ItemSlotType.Weapon => "personal_power_10",
                    ItemSlotType.Armor => "personal_power_9",
                    _ => "personal_power_8"
                };
                a.BaseModifiers = new List<SerializableStatModifier>
                {
                    new() { StatId = d.stat, Value = d.value }
                };
                UpsertStringEntry(ContentLocalizationTables.Items, a.NameKey, d.koName, d.enName);
                UpsertStringEntry(ContentLocalizationTables.Items, a.DescriptionKey, d.koDesc, d.enDesc);
            });
        }
    }

    private static void CreateAffixes()
    {
        var affixes = new (string id, string enName, string koName, string stat, float value)[]
        {
            ("affix_sturdy", "Sturdy", "견고", "armor", 1f),
            ("affix_fierce", "Fierce", "맹렬", "phys_power", 1f),
            ("affix_quick", "Quick", "신속", "attack_speed", 1f),
            ("affix_vital", "Vital", "활력", "max_health", 2f),
            ("affix_blessed", "Blessed", "축복", "heal_power", 1f),
            ("affix_heavy", "Heavy", "중량", "armor", 2f),
            ("affix_sharp", "Sharp", "예리", "phys_power", 2f),
            ("affix_hasty", "Hasty", "재촉", "attack_speed", 2f),
            ("affix_warded", "Warded", "결계", "resist", 1f),
            ("affix_piercing", "Piercing", "관통", "phys_pen", 0.7f),
            ("affix_focusing", "Focusing", "집속", "mag_power", 1f),
            ("affix_cleansing", "Cleansing", "정화", "heal_power", 1f),
            ("affix_reaching", "Reaching", "장거리", "attack_range", 0.2f),
            ("affix_lithe", "Lithe", "날렵", "move_speed", 0.05f),
        };

        foreach (var d in affixes)
        {
            CreateAsset<AffixDefinition>($"{ResourcesRoot}/Affixes/{d.id}.asset", a =>
            {
                a.Id = d.id;
                a.NameKey = ContentLocalizationTables.BuildAffixNameKey(d.id);
                a.DescriptionKey = ContentLocalizationTables.BuildAffixDescriptionKey(d.id);
                a.Category = d.stat switch
                {
                    "phys_power" or "mag_power" or "phys_pen" or "mag_pen" => AffixCategoryValue.OffenseFlat,
                    "armor" or "resist" => AffixCategoryValue.DefenseFlat,
                    "max_health" or "tenacity" => AffixCategoryValue.DefenseScaling,
                    _ => AffixCategoryValue.Utility
                };
                a.AllowedSlotTypes = d.stat switch
                {
                    "phys_power" or "mag_power" or "phys_pen" or "mag_pen" or "attack_range" => new List<ItemSlotType> { ItemSlotType.Weapon },
                    "armor" or "resist" => new List<ItemSlotType> { ItemSlotType.Armor, ItemSlotType.Accessory },
                    "max_health" or "tenacity" => new List<ItemSlotType> { ItemSlotType.Armor, ItemSlotType.Accessory },
                    _ => new List<ItemSlotType> { ItemSlotType.Accessory, ItemSlotType.Armor }
                };
                a.Modifiers = new List<SerializableStatModifier>
                {
                    new() { StatId = d.stat, Value = d.value }
                };
                UpsertStringEntry(ContentLocalizationTables.Affixes, a.NameKey, d.koName, d.enName);
                UpsertStringEntry(ContentLocalizationTables.Affixes, a.DescriptionKey, d.koName, d.enName);
            });
        }
    }

    private static void CreateSafeTargetPassiveNodes(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        CreateClassSafeTargetPassiveNodes("vanguard", tags, new[]
        {
            NodeSeed("small_13", PassiveNodeKindValue.Small, 3, new[] { "passive_vanguard_small_10" }, "견고한 발판", "Firm Footing", "저항을 소폭 높입니다.", "Slightly improves resistance.", new[] { "frontline", "guard" }, Mods(("resist", 0.5f))),
            NodeSeed("small_14", PassiveNodeKindValue.Small, 3, new[] { "passive_vanguard_small_11" }, "보호 거리", "Guard Reach", "보호 반경을 소폭 넓힙니다.", "Slightly expands protect radius.", new[] { "frontline", "shield_skill" }, Mods(("protect_radius", 0.25f))),
            NodeSeed("notable_06", PassiveNodeKindValue.Notable, 4, new[] { "passive_vanguard_small_13" }, "방벽 규율", "Bulwark Discipline", "체력과 저항을 함께 높입니다.", "Improves health and resistance together.", new[] { "frontline", "guard" }, Mods(("max_health", 2f), ("resist", 0.5f))),
            NodeSeed("notable_07", PassiveNodeKindValue.Notable, 4, new[] { "passive_vanguard_small_14" }, "흔들림 없는 전열", "Unshaken Line", "방어와 강인함을 높입니다.", "Improves armor and tenacity.", new[] { "frontline", "shield_skill" }, Mods(("armor", 1f), ("tenacity", 0.05f))),
            NodeSeed("notable_08", PassiveNodeKindValue.Notable, 4, new[] { "passive_vanguard_notable_05" }, "도발 반경", "Challenge Radius", "보호와 위협 반경을 넓힙니다.", "Expands protect and aggro radius.", new[] { "frontline", "guard" }, Mods(("protect_radius", 0.35f), ("aggro_radius", 0.4f))),
            NodeSeed("keystone_02", PassiveNodeKindValue.Keystone, 5, new[] { "passive_vanguard_notable_06", "passive_vanguard_notable_07" }, "불굴의 방패", "Unbroken Shield", "전열 유지력을 크게 높입니다.", "Greatly improves frontline staying power.", new[] { "frontline", "guard", "shield_skill" }, Mods(("armor", 1.2f), ("max_health", 3f), ("tenacity", 0.08f))),
        });

        CreateClassSafeTargetPassiveNodes("duelist", tags, new[]
        {
            NodeSeed("small_13", PassiveNodeKindValue.Small, 3, new[] { "passive_duelist_small_10" }, "치명 각도", "Killing Angle", "치명타 배율을 소폭 높입니다.", "Slightly improves critical multiplier.", new[] { "frontline", "strike" }, Mods(("crit_multiplier", 0.08f))),
            NodeSeed("small_14", PassiveNodeKindValue.Small, 3, new[] { "passive_duelist_small_11" }, "빠른 전환", "Quick Pivot", "대상 전환 지연을 줄입니다.", "Reduces target switch delay.", new[] { "frontline", "physical" }, Mods(("target_switch_delay", -0.04f))),
            NodeSeed("notable_06", PassiveNodeKindValue.Notable, 4, new[] { "passive_duelist_small_13" }, "처형 자세", "Execution Stance", "물리 출력과 치명타 배율을 높입니다.", "Improves physical output and critical multiplier.", new[] { "frontline", "execute" }, Mods(("phys_power", 0.8f), ("crit_multiplier", 0.08f))),
            NodeSeed("notable_07", PassiveNodeKindValue.Notable, 4, new[] { "passive_duelist_small_14" }, "끊김 없는 추격", "Relentless Chase", "이동과 공격 예열을 개선합니다.", "Improves movement and attack windup.", new[] { "frontline", "physical" }, Mods(("move_speed", 0.05f), ("attack_windup", -0.04f))),
            NodeSeed("notable_08", PassiveNodeKindValue.Notable, 4, new[] { "passive_duelist_notable_05" }, "피의 빈틈", "Blood Opening", "흡혈과 관통력을 높입니다.", "Improves lifesteal and penetration.", new[] { "frontline", "execute" }, Mods(("lifesteal", 0.02f), ("phys_pen", 0.6f))),
            NodeSeed("keystone_02", PassiveNodeKindValue.Keystone, 5, new[] { "passive_duelist_notable_06", "passive_duelist_notable_07" }, "창백한 결말", "Pale Finale", "마무리 폭발력을 크게 높입니다.", "Greatly improves execution burst.", new[] { "frontline", "execute", "physical" }, Mods(("phys_power", 1.2f), ("crit_chance", 0.02f), ("target_switch_delay", -0.06f))),
        });

        CreateClassSafeTargetPassiveNodes("ranger", tags, new[]
        {
            NodeSeed("small_13", PassiveNodeKindValue.Small, 3, new[] { "passive_ranger_small_10" }, "긴 호흡", "Long Breath", "공격 사거리를 소폭 늘립니다.", "Slightly increases attack range.", new[] { "backline", "projectile" }, Mods(("attack_range", 0.15f))),
            NodeSeed("small_14", PassiveNodeKindValue.Small, 3, new[] { "passive_ranger_small_11" }, "날카로운 투사체", "Keen Projectile", "투사체 속도를 높입니다.", "Improves projectile speed.", new[] { "backline", "projectile" }, Mods(("projectile_speed", 0.25f))),
            NodeSeed("notable_06", PassiveNodeKindValue.Notable, 4, new[] { "passive_ranger_small_13" }, "약점 사거리", "Weakside Reach", "물리 출력과 사거리를 높입니다.", "Improves physical output and range.", new[] { "backline", "projectile" }, Mods(("phys_power", 0.8f), ("attack_range", 0.2f))),
            NodeSeed("notable_07", PassiveNodeKindValue.Notable, 4, new[] { "passive_ranger_small_14" }, "정조준 흐름", "Aimed Flow", "치명타 확률과 투사체 속도를 높입니다.", "Improves critical chance and projectile speed.", new[] { "backline", "mark" }, Mods(("crit_chance", 0.02f), ("projectile_speed", 0.35f))),
            NodeSeed("notable_08", PassiveNodeKindValue.Notable, 4, new[] { "passive_ranger_notable_05" }, "그림자 이동", "Shadow Step", "이동과 대상 전환을 개선합니다.", "Improves movement and target switching.", new[] { "backline", "exposed" }, Mods(("move_speed", 0.05f), ("target_switch_delay", -0.04f))),
            NodeSeed("keystone_02", PassiveNodeKindValue.Keystone, 5, new[] { "passive_ranger_notable_06", "passive_ranger_notable_07" }, "균열 저격", "Rift Marksmanship", "후열 화력의 사거리와 정확도를 크게 높입니다.", "Greatly improves backline range and precision.", new[] { "backline", "projectile", "mark" }, Mods(("phys_power", 1f), ("attack_range", 0.25f), ("crit_chance", 0.02f))),
        });

        CreateClassSafeTargetPassiveNodes("mystic", tags, new[]
        {
            NodeSeed("small_13", PassiveNodeKindValue.Small, 3, new[] { "passive_mystic_small_10" }, "고요한 의식", "Quiet Rite", "저항을 소폭 높입니다.", "Slightly improves resistance.", new[] { "backline", "support" }, Mods(("resist", 0.4f))),
            NodeSeed("small_14", PassiveNodeKindValue.Small, 3, new[] { "passive_mystic_small_11" }, "빠른 기도", "Swift Prayer", "재사용 회복을 높입니다.", "Improves cooldown recovery.", new[] { "backline", "heal" }, Mods(("cooldown_recovery", 0.04f))),
            NodeSeed("notable_06", PassiveNodeKindValue.Notable, 4, new[] { "passive_mystic_small_13" }, "정화 성가", "Cleansing Canticle", "회복과 재사용 회복을 높입니다.", "Improves healing and cooldown recovery.", new[] { "backline", "cleanse", "heal" }, Mods(("heal_power", 0.8f), ("cooldown_recovery", 0.04f))),
            NodeSeed("notable_07", PassiveNodeKindValue.Notable, 4, new[] { "passive_mystic_small_14" }, "집속 주문", "Focused Hex", "마법 출력과 관통력을 높입니다.", "Improves magic output and penetration.", new[] { "backline", "magical" }, Mods(("mag_power", 0.8f), ("mag_pen", 0.6f))),
            NodeSeed("notable_08", PassiveNodeKindValue.Notable, 4, new[] { "passive_mystic_notable_05" }, "거울 안정", "Mirror Stability", "강인함과 저항을 높입니다.", "Improves tenacity and resistance.", new[] { "backline", "support" }, Mods(("tenacity", 0.04f), ("resist", 0.5f))),
            NodeSeed("keystone_02", PassiveNodeKindValue.Keystone, 5, new[] { "passive_mystic_notable_06", "passive_mystic_notable_07" }, "쌍성 성가", "Twin Cantor", "지원과 마법 순환을 크게 높입니다.", "Greatly improves support and magic cadence.", new[] { "backline", "support", "magical" }, Mods(("heal_power", 1f), ("mag_power", 0.8f), ("cooldown_recovery", 0.06f))),
        });
    }

    private static void CreateClassSafeTargetPassiveNodes(
        string classId,
        IReadOnlyDictionary<string, StableTagDefinition> tags,
        IReadOnlyList<PassiveNodeSeed> seeds)
    {
        var boardId = $"board_{classId}";
        var board = LoadDefinition<PassiveBoardDefinition>($"{ResourcesRoot}/PassiveBoards/board_{classId}.asset");
        if (board == null)
        {
            Debug.LogWarning($"SM safe-target passive node generation will patch fallback board YAML for class '{classId}'.");
        }

        var createdNodes = new List<PassiveNodeDefinition>();
        foreach (var seed in seeds)
        {
            var nodeId = $"passive_{classId}_{seed.Suffix}";
            var node = CreateAsset<PassiveNodeDefinition>($"{ResourcesRoot}/PassiveNodes/{nodeId}.asset", asset =>
            {
                asset.Id = nodeId;
                asset.BoardId = board != null ? board.Id : boardId;
                asset.NameKey = ContentLocalizationTables.BuildPassiveNodeNameKey(nodeId);
                asset.DescriptionKey = ContentLocalizationTables.BuildPassiveNodeDescriptionKey(nodeId);
                asset.NodeKind = seed.Kind;
                asset.PrerequisiteNodeIds = seed.PrerequisiteIds.ToList();
                asset.MutualExclusionTags = new List<StableTagDefinition>();
                asset.BoardDepth = seed.Depth;
                asset.CompileTags = ResolveTags(tags, new[] { classId }.Concat(seed.CompileTagIds));
                asset.RuleModifierTags = new List<StableTagDefinition>();
                asset.Modifiers = seed.Modifiers
                    .Select(modifier => new SerializableStatModifier { StatId = modifier.StatId, Op = modifier.Op, Value = modifier.Value })
                    .ToList();
                UpsertStringEntry(ContentLocalizationTables.Passives, asset.NameKey, seed.KoName, seed.EnName);
                UpsertStringEntry(ContentLocalizationTables.Passives, asset.DescriptionKey, seed.KoDescription, seed.EnDescription);
            });
            PatchSerializedPassiveNodeTags(node);
            createdNodes.Add(node);
        }

        if (board == null)
        {
            PatchPassiveBoardFallbackYaml(classId, createdNodes);
            return;
        }

        var orderedNodes = new List<PassiveNodeDefinition>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in (board.Nodes ?? new List<PassiveNodeDefinition>()).Concat(createdNodes))
        {
            if (node == null || string.IsNullOrWhiteSpace(node.Id) || !seen.Add(node.Id))
            {
                continue;
            }

            orderedNodes.Add(node);
        }

        board.Nodes = orderedNodes;
        EditorUtility.SetDirty(board);
    }

    private static void PatchPassiveBoardFallbackYaml(string classId, IReadOnlyList<PassiveNodeDefinition> createdNodes)
    {
        var boardPath = $"{ResourcesRoot}/PassiveBoards/board_{classId}.asset";
        if (!File.Exists(boardPath))
        {
            Debug.LogWarning($"SM safe-target passive board YAML patch skipped missing file '{boardPath}'.");
            return;
        }

        var referenceLines = createdNodes
            .Select(node => BuildAssetReferenceLine($"{ResourcesRoot}/PassiveNodes/{node.Id}.asset"))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        if (referenceLines.Count == 0)
        {
            return;
        }

        var lines = File.ReadAllLines(boardPath).ToList();
        var nodesLineIndex = lines.FindIndex(line => string.Equals(line.Trim(), "Nodes:", StringComparison.Ordinal));
        if (nodesLineIndex < 0)
        {
            Debug.LogWarning($"SM safe-target passive board YAML patch skipped board without Nodes block '{boardPath}'.");
            return;
        }

        var insertIndex = nodesLineIndex + 1;
        while (insertIndex < lines.Count && lines[insertIndex].StartsWith("  - ", StringComparison.Ordinal))
        {
            insertIndex++;
        }

        var existing = lines
            .Skip(nodesLineIndex + 1)
            .Take(insertIndex - nodesLineIndex - 1)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var referenceLine in referenceLines)
        {
            if (existing.Contains(referenceLine))
            {
                continue;
            }

            lines.Insert(insertIndex, referenceLine);
            insertIndex++;
            existing.Add(referenceLine);
        }

        File.WriteAllLines(boardPath, lines);
        AssetDatabase.ImportAsset(boardPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static string BuildAssetReferenceLine(string assetPath)
    {
        var referenceValue = BuildAssetReferenceValue(assetPath);
        return string.IsNullOrWhiteSpace(referenceValue)
            ? string.Empty
            : $"  - {referenceValue}";
    }

    private static void PatchArchetypeSkillFallbackYaml(string assetPath, string classId, IReadOnlyList<string> defaultSkillIds)
    {
        if (!File.Exists(assetPath))
        {
            return;
        }

        var activePoolIds = ResolveFlexUtilitySkillIds(classId);
        var passivePoolIds = ResolveFlexSupportSkillIds(classId);
        var flexActiveId = defaultSkillIds.FirstOrDefault(id => activePoolIds.Contains(id, StringComparer.Ordinal))
                           ?? activePoolIds.FirstOrDefault()
                           ?? string.Empty;
        var signatureActiveId = defaultSkillIds.FirstOrDefault(id => !activePoolIds.Contains(id, StringComparer.Ordinal) && !passivePoolIds.Contains(id, StringComparer.Ordinal))
                                ?? defaultSkillIds.FirstOrDefault()
                                ?? string.Empty;

        var lines = File.ReadAllLines(assetPath).ToList();
        ReplaceYamlReferenceList(lines, "Skills:", defaultSkillIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "FlexUtilitySkillPool:", activePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "FlexSupportSkillPool:", passivePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "RecruitFlexActivePool:", activePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "RecruitFlexPassivePool:", passivePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceValue(lines, "SignatureActive:", BuildSkillAssetPath(signatureActiveId));
        ReplaceYamlReferenceValue(lines, "FlexActive:", BuildSkillAssetPath(flexActiveId));
        File.WriteAllLines(assetPath, lines);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static void PatchSafeTargetSpecialistFallbackYaml()
    {
        var specs = new[]
        {
            new SpecialistFallbackSpec(
                "rift_stalker",
                "beastkin",
                "ranger",
                DeploymentAnchorValue.BackBottom,
                TeamPostureTypeValue.CollapseWeakSide,
                new[] { "skill_scout_core", "skill_scout_utility", "skill_ranger_passive_2", "support_swift" },
                new[] { "ranger", "backline", "dash", "exposed" },
                new[] { "backline", "exposed", "ranger" }),
            new SpecialistFallbackSpec(
                "bastion_penitent",
                "human",
                "vanguard",
                DeploymentAnchorValue.FrontCenter,
                TeamPostureTypeValue.ProtectCarry,
                new[] { "skill_bulwark_core", "skill_warden_utility", "skill_vanguard_passive_2", "support_anchored" },
                new[] { "vanguard", "frontline", "guard", "shield_skill" },
                new[] { "frontline", "guard", "vanguard" }),
            new SpecialistFallbackSpec(
                "pale_executor",
                "undead",
                "duelist",
                DeploymentAnchorValue.FrontBottom,
                TeamPostureTypeValue.CollapseWeakSide,
                new[] { "skill_reaver_core", "skill_raider_utility", "skill_duelist_passive_2", "support_executioner" },
                new[] { "duelist", "frontline", "mark", "execute" },
                new[] { "frontline", "execute", "duelist" }),
            new SpecialistFallbackSpec(
                "mirror_cantor",
                "human",
                "mystic",
                DeploymentAnchorValue.BackCenter,
                TeamPostureTypeValue.ProtectCarry,
                new[] { "skill_priest_core", "skill_hexer_utility", "skill_mystic_passive_1", "support_purifying" },
                new[] { "mystic", "backline", "cleanse", "support" },
                new[] { "backline", "cleanse", "mystic" }),
        };

        foreach (var spec in specs)
        {
            PatchSpecialistArchetypeFallbackYaml(spec);
            PatchSpecialistCharacterFallbackYaml(spec);
        }
    }

    private static void PatchSpecialistArchetypeFallbackYaml(SpecialistFallbackSpec spec)
    {
        var assetPath = $"{ResourcesRoot}/Archetypes/archetype_{spec.Id}.asset";
        if (!File.Exists(assetPath))
        {
            return;
        }

        var classId = spec.ClassId;
        var roleFamily = classId == "duelist" ? "striker" : classId;
        var weaponFamily = ResolveWeaponFamily(classId);
        var roleTag = ResolveRoleTag(classId);
        var recruitTier = RecruitTier.Rare;
        var roleProfile = ResolveRoleProfile(spec.Id, classId);
        var budgetCard = BuildArchetypeBudgetCard(roleProfile, LoopCContentGovernance.FromRecruitTier(recruitTier));
        var activePoolIds = ResolveFlexUtilitySkillIds(classId);
        var passivePoolIds = ResolveFlexSupportSkillIds(classId);
        var flexActiveId = spec.DefaultSkillIds.FirstOrDefault(id => activePoolIds.Contains(id, StringComparer.Ordinal))
                           ?? activePoolIds.FirstOrDefault()
                           ?? string.Empty;
        var signatureActiveId = spec.DefaultSkillIds.FirstOrDefault(id => !activePoolIds.Contains(id, StringComparer.Ordinal) && !passivePoolIds.Contains(id, StringComparer.Ordinal))
                                ?? spec.DefaultSkillIds.FirstOrDefault()
                                ?? string.Empty;

        var lines = File.ReadAllLines(assetPath).ToList();
        ReplaceYamlScalar(lines, "Id:", spec.Id);
        ReplaceYamlScalar(lines, "NameKey:", ContentLocalizationTables.BuildArchetypeNameKey(spec.Id));
        ReplaceYamlScalar(lines, "ScopeKind:", ((int)ArchetypeScopeValue.Specialist).ToString());
        ReplaceYamlReferenceValue(lines, "Race:", $"{ResourcesRoot}/Races/race_{spec.RaceId}.asset");
        ReplaceYamlReferenceValue(lines, "Class:", $"{ResourcesRoot}/Classes/class_{classId}.asset");
        ReplaceYamlReferenceValue(lines, "TraitPool:", $"{ResourcesRoot}/Traits/traitpool_{spec.Id}.asset");
        ReplaceYamlReferenceList(lines, "Skills:", spec.DefaultSkillIds.Select(BuildSkillAssetPath));
        ReplaceYamlTacticPreset(lines, classId, BuildSkillAssetPath(signatureActiveId));
        ReplaceYamlScalar(lines, "DefaultAnchor:", ((int)spec.DefaultAnchor).ToString());
        ReplaceYamlScalar(lines, "PreferredTeamPosture:", ((int)spec.PreferredPosture).ToString());
        ReplaceYamlScalar(lines, "RoleTag:", roleTag);
        ReplaceYamlScalar(lines, "RoleFamilyTag:", roleFamily);
        ReplaceYamlScalar(lines, "PrimaryWeaponFamilyTag:", weaponFamily);
        ReplaceYamlScalar(lines, "RecruitTier:", ((int)recruitTier).ToString());
        ReplaceYamlBudgetCard(lines, budgetCard);
        ReplaceYamlScalar(lines, "IsRecruitable:", "1");
        ReplaceYamlScalar(lines, "IsSummonOnly:", "0");
        ReplaceYamlScalar(lines, "IsEventOnly:", "0");
        ReplaceYamlScalar(lines, "IsBossOnly:", "0");
        ReplaceYamlScalar(lines, "IsUnreleased:", "0");
        ReplaceYamlScalar(lines, "IsTestOnly:", "0");
        ReplaceYamlReferenceList(lines, "RecruitPlanTags:", spec.PlanTagIds.Select(BuildStableTagAssetPath));
        ReplaceYamlReferenceList(lines, "ScoutBiasTags:", spec.ScoutTagIds.Select(BuildStableTagAssetPath));
        ReplaceYamlReferenceList(lines, "FlexUtilitySkillPool:", activePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "FlexSupportSkillPool:", passivePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "RecruitFlexActivePool:", activePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlReferenceList(lines, "RecruitFlexPassivePool:", passivePoolIds.Select(BuildSkillAssetPath));
        ReplaceYamlRecruitBannedPairings(lines, classId);
        ReplaceYamlReferenceValue(lines, "SignatureActive:", BuildSkillAssetPath(signatureActiveId));
        ReplaceYamlReferenceValue(lines, "FlexActive:", BuildSkillAssetPath(flexActiveId));
        ReplaceYamlReferenceValue(lines, "FootprintProfile:", $"{ResourcesRoot}/FootprintProfiles/footprint_{classId}.asset");
        ReplaceYamlReferenceValue(lines, "BehaviorProfile:", $"{ResourcesRoot}/BehaviorProfiles/behavior_{classId}.asset");
        ReplaceYamlReferenceValue(lines, "MobilityProfile:", $"{ResourcesRoot}/MobilityProfiles/mobility_{classId}.asset");

        File.WriteAllLines(assetPath, lines);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static void PatchSpecialistCharacterFallbackYaml(SpecialistFallbackSpec spec)
    {
        var assetPath = $"{ResourcesRoot}/Characters/character_{spec.Id}.asset";
        if (!File.Exists(assetPath))
        {
            return;
        }

        var lines = File.ReadAllLines(assetPath).ToList();
        ReplaceYamlScalar(lines, "Id:", spec.Id);
        ReplaceYamlScalar(lines, "NameKey:", ContentLocalizationTables.BuildCharacterNameKey(spec.Id));
        ReplaceYamlScalar(lines, "DescriptionKey:", ContentLocalizationTables.BuildCharacterDescriptionKey(spec.Id));
        ReplaceYamlReferenceValue(lines, "Race:", $"{ResourcesRoot}/Races/race_{spec.RaceId}.asset");
        ReplaceYamlReferenceValue(lines, "Class:", $"{ResourcesRoot}/Classes/class_{spec.ClassId}.asset");
        ReplaceYamlReferenceValue(lines, "DefaultArchetype:", $"{ResourcesRoot}/Archetypes/archetype_{spec.Id}.asset");
        ReplaceYamlReferenceValue(lines, "DefaultRoleInstruction:", $"{ResourcesRoot}/RoleInstructions/{ResolveRoleTag(spec.ClassId)}.asset");

        File.WriteAllLines(assetPath, lines);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    private static string BuildStableTagAssetPath(string tagId)
    {
        return string.IsNullOrWhiteSpace(tagId)
            ? string.Empty
            : $"{ResourcesRoot}/StableTags/tag_{tagId}.asset";
    }

    private static string BuildSkillAssetPath(string skillId)
    {
        return string.IsNullOrWhiteSpace(skillId)
            ? string.Empty
            : $"{ResourcesRoot}/Skills/{skillId}.asset";
    }

    private static string ResolveWeaponFamily(string classId)
    {
        return classId switch
        {
            "vanguard" => "shield",
            "duelist" => "blade",
            "ranger" => "bow",
            "mystic" => "focus",
            _ => string.Empty,
        };
    }

    private static string ResolveRoleTag(string classId)
    {
        return classId switch
        {
            "vanguard" => "anchor",
            "duelist" => "bruiser",
            "ranger" => "carry",
            "mystic" => "support",
            _ => "frontline",
        };
    }

    private static CombatRoleBudgetProfile ResolveRoleProfile(string archetypeId, string classId)
    {
        return classId switch
        {
            "vanguard" => CombatRoleBudgetProfile.Vanguard,
            "ranger" => CombatRoleBudgetProfile.Ranger,
            "mystic" when string.Equals(archetypeId, "priest", StringComparison.Ordinal) || string.Equals(archetypeId, "mirror_cantor", StringComparison.Ordinal) => CombatRoleBudgetProfile.Support,
            "mystic" => CombatRoleBudgetProfile.Arcanist,
            "duelist" when string.Equals(archetypeId, "raider", StringComparison.Ordinal) || string.Equals(archetypeId, "reaver", StringComparison.Ordinal) => CombatRoleBudgetProfile.Bruiser,
            "duelist" => CombatRoleBudgetProfile.Duelist,
            _ => CombatRoleBudgetProfile.Vanguard,
        };
    }

    private static BudgetCard BuildArchetypeBudgetCard(CombatRoleBudgetProfile roleProfile, ContentRarity rarity)
    {
        var target = LoopCContentGovernance.UnitBudgetTargets[rarity].Target;
        var vector = roleProfile switch
        {
            CombatRoleBudgetProfile.Vanguard => MakeBudgetVector(10, 4, 36, 24, 4, 6, 8, 8),
            CombatRoleBudgetProfile.Bruiser => MakeBudgetVector(24, 12, 28, 6, 14, 2, 6, 8),
            CombatRoleBudgetProfile.Duelist => MakeBudgetVector(16, 28, 10, 4, 22, 2, 6, 12),
            CombatRoleBudgetProfile.Ranger => MakeBudgetVector(28, 8, 8, 4, 12, 4, 8, 28),
            CombatRoleBudgetProfile.Arcanist => MakeBudgetVector(8, 30, 6, 20, 8, 6, 8, 14),
            CombatRoleBudgetProfile.Support => MakeBudgetVector(6, 6, 18, 8, 6, 32, 6, 18),
            CombatRoleBudgetProfile.Summoner => MakeBudgetVector(18, 8, 12, 6, 8, 24, 14, 10),
            _ => MakeBudgetVector(14, 12, 18, 8, 8, 8, 8, 24),
        };
        AdjustBudgetFinalScore(vector, target);

        var (threats, counters) = ResolveArchetypeTopology(roleProfile, rarity);
        return BuildBudgetCard(
            BudgetDomain.UnitBlueprint,
            rarity,
            PowerBand.Standard,
            roleProfile,
            vector,
            keywordCount: rarity switch
            {
                ContentRarity.Common => 2,
                ContentRarity.Rare => 3,
                _ => 4,
            },
            conditionClauseCount: rarity == ContentRarity.Common ? 1 : 2,
            ruleExceptionCount: rarity == ContentRarity.Epic ? 1 : 0,
            threats,
            counters);
    }

    private static void ReplaceYamlReferenceList(List<string> lines, string header, IEnumerable<string> assetPaths)
    {
        var index = FindYamlFieldLineIndex(lines, header);
        if (index < 0)
        {
            return;
        }

        var indent = lines[index].Length - lines[index].TrimStart().Length;
        var prefix = new string(' ', indent);
        lines[index] = $"{prefix}{header}";

        var removeIndex = index + 1;
        while (removeIndex < lines.Count && lines[removeIndex].StartsWith($"{prefix}- ", StringComparison.Ordinal))
        {
            lines.RemoveAt(removeIndex);
        }

        var insertIndex = index + 1;
        foreach (var assetPath in assetPaths)
        {
            var referenceValue = BuildAssetReferenceValue(assetPath);
            if (string.IsNullOrWhiteSpace(referenceValue))
            {
                continue;
            }

            lines.Insert(insertIndex, $"{prefix}- {referenceValue}");
            insertIndex++;
        }
    }

    private static void ReplaceYamlScalar(List<string> lines, string field, string value)
    {
        var index = FindYamlFieldLineIndex(lines, field);
        if (index < 0)
        {
            return;
        }

        var indent = lines[index].Length - lines[index].TrimStart().Length;
        lines[index] = $"{new string(' ', indent)}{field} {value ?? string.Empty}".TrimEnd();
    }

    private static void ReplaceYamlReferenceValue(List<string> lines, string field, string assetPath)
    {
        var index = FindYamlFieldLineIndex(lines, field);
        if (index < 0)
        {
            return;
        }

        var indent = lines[index].Length - lines[index].TrimStart().Length;
        var referenceValue = BuildAssetReferenceValue(assetPath);
        lines[index] = string.IsNullOrWhiteSpace(referenceValue)
            ? $"{new string(' ', indent)}{field} {{fileID: 0}}"
            : $"{new string(' ', indent)}{field} {referenceValue}";
    }

    private static void ReplaceYamlTacticPreset(List<string> lines, string classId, string signatureSkillPath)
    {
        var signatureSkill = BuildAssetReferenceValue(signatureSkillPath);
        if (string.IsNullOrWhiteSpace(signatureSkill))
        {
            signatureSkill = "{fileID: 0}";
        }

        var entries = classId == "mystic"
            ? new[]
            {
                (Priority: 0, ConditionType: 1, Threshold: "0.6", ActionType: 1, TargetSelector: 1, Skill: signatureSkill),
                (Priority: 1, ConditionType: 4, Threshold: "1.5", ActionType: 0, TargetSelector: 5, Skill: "{fileID: 0}"),
                (Priority: 2, ConditionType: 5, Threshold: "0", ActionType: 2, TargetSelector: 0, Skill: "{fileID: 0}"),
            }
            : new[]
            {
                (Priority: 0, ConditionType: 4, Threshold: "1.5", ActionType: 1, TargetSelector: 5, Skill: signatureSkill),
                (Priority: 1, ConditionType: 3, Threshold: "0", ActionType: 0, TargetSelector: 3, Skill: "{fileID: 0}"),
                (Priority: 2, ConditionType: 5, Threshold: "0", ActionType: 2, TargetSelector: 0, Skill: "{fileID: 0}"),
            };

        ReplaceYamlBlock(lines, "TacticPreset:", indent =>
        {
            var prefix = new string(' ', indent);
            var nested = $"{prefix}  ";
            var block = new List<string>();
            foreach (var entry in entries)
            {
                block.Add($"{prefix}- Priority: {entry.Priority}");
                block.Add($"{nested}ConditionType: {entry.ConditionType}");
                block.Add($"{nested}Threshold: {entry.Threshold}");
                block.Add($"{nested}ActionType: {entry.ActionType}");
                block.Add($"{nested}TargetSelector: {entry.TargetSelector}");
                block.Add($"{nested}Skill: {entry.Skill}");
            }

            return block;
        });
    }

    private static void ReplaceYamlBudgetCard(List<string> lines, BudgetCard budgetCard)
    {
        ReplaceYamlBlock(lines, "BudgetCard:", indent =>
        {
            var nested = $"{new string(' ', indent)}  ";
            var vector = budgetCard.Vector;
            var block = new List<string>
            {
                $"{nested}Domain: {(int)budgetCard.Domain}",
                $"{nested}Rarity: {(int)budgetCard.Rarity}",
                $"{nested}PowerBand: {(int)budgetCard.PowerBand}",
                $"{nested}RoleProfile: {(int)budgetCard.RoleProfile}",
                $"{nested}Vector:",
                $"{nested}  SustainedDamage: {vector.SustainedDamage}",
                $"{nested}  BurstDamage: {vector.BurstDamage}",
                $"{nested}  Durability: {vector.Durability}",
                $"{nested}  Control: {vector.Control}",
                $"{nested}  Mobility: {vector.Mobility}",
                $"{nested}  Support: {vector.Support}",
                $"{nested}  CounterCoverage: {vector.CounterCoverage}",
                $"{nested}  Reliability: {vector.Reliability}",
                $"{nested}  Economy: {vector.Economy}",
                $"{nested}  DrawbackCredit: {vector.DrawbackCredit}",
                $"{nested}KeywordCount: {budgetCard.KeywordCount}",
                $"{nested}ConditionClauseCount: {budgetCard.ConditionClauseCount}",
                $"{nested}RuleExceptionCount: {budgetCard.RuleExceptionCount}",
                $"{nested}DeclaredThreatPatterns: {SerializePackedEnumArray(budgetCard.DeclaredThreatPatterns)}",
            };

            if (budgetCard.DeclaredCounterTools.Length == 0)
            {
                block.Add($"{nested}DeclaredCounterTools: []");
            }
            else
            {
                block.Add($"{nested}DeclaredCounterTools:");
                foreach (var counter in budgetCard.DeclaredCounterTools)
                {
                    block.Add($"{nested}- Tool: {(int)counter.Tool}");
                    block.Add($"{nested}  Strength: {(int)counter.Strength}");
                }
            }

            block.Add($"{nested}DeclaredFeatureFlags: {(int)budgetCard.DeclaredFeatureFlags}");
            return block;
        });
    }

    private static void ReplaceYamlRecruitBannedPairings(List<string> lines, string classId)
    {
        var pairs = BuildRecruitBannedPairings(classId);
        ReplaceYamlBlock(lines, "RecruitBannedPairings:", indent =>
        {
            if (pairs.Count == 0)
            {
                return Array.Empty<string>();
            }

            var prefix = new string(' ', indent);
            return pairs
                .SelectMany(pair => new[]
                {
                    $"{prefix}- FlexActiveId: {pair.FlexActiveId}",
                    $"{prefix}  FlexPassiveId: {pair.FlexPassiveId}",
                })
                .ToList();
        });
    }

    private static void ReplaceYamlBlock(List<string> lines, string header, Func<int, IReadOnlyList<string>> buildBlock)
    {
        var index = FindYamlFieldLineIndex(lines, header);
        if (index < 0)
        {
            return;
        }

        var indent = lines[index].Length - lines[index].TrimStart().Length;
        var prefix = new string(' ', indent);
        lines[index] = $"{prefix}{header}";

        var removeIndex = index + 1;
        while (removeIndex < lines.Count)
        {
            var trimmed = lines[removeIndex].Trim();
            if (!string.IsNullOrWhiteSpace(trimmed)
                && lines[removeIndex].Length - lines[removeIndex].TrimStart().Length <= indent
                && !lines[removeIndex].StartsWith($"{prefix}- ", StringComparison.Ordinal))
            {
                break;
            }

            lines.RemoveAt(removeIndex);
        }

        var insertIndex = index + 1;
        foreach (var line in buildBlock(indent))
        {
            lines.Insert(insertIndex, line);
            insertIndex++;
        }
    }

    private static string SerializePackedEnumArray<TEnum>(IEnumerable<TEnum> values)
        where TEnum : Enum
    {
        return string.Concat(values.Select(value => $"{Convert.ToInt32(value):x2}000000"));
    }

    private static int FindYamlFieldLineIndex(IReadOnlyList<string> lines, string field)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (string.Equals(trimmed, field, StringComparison.Ordinal)
                || trimmed.StartsWith($"{field} ", StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string BuildAssetReferenceValue(string assetPath)
    {
        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        return string.IsNullOrWhiteSpace(guid)
            ? string.Empty
            : $"{{fileID: 11400000, guid: {guid}, type: 2}}";
    }

    private sealed record SpecialistFallbackSpec(
        string Id,
        string RaceId,
        string ClassId,
        DeploymentAnchorValue DefaultAnchor,
        TeamPostureTypeValue PreferredPosture,
        string[] DefaultSkillIds,
        string[] PlanTagIds,
        string[] ScoutTagIds);

    private static PassiveNodeSeed NodeSeed(
        string suffix,
        PassiveNodeKindValue kind,
        int depth,
        string[] prerequisiteIds,
        string koName,
        string enName,
        string koDescription,
        string enDescription,
        string[] compileTagIds,
        SerializableStatModifier[] modifiers)
    {
        return new PassiveNodeSeed(suffix, kind, depth, prerequisiteIds, koName, enName, koDescription, enDescription, compileTagIds, modifiers);
    }

    private static SerializableStatModifier[] Mods(params (string StatId, float Value)[] modifiers)
    {
        return modifiers
            .Select(modifier => new SerializableStatModifier { StatId = modifier.StatId, Value = modifier.Value })
            .ToArray();
    }

    private sealed record PassiveNodeSeed(
        string Suffix,
        PassiveNodeKindValue Kind,
        int Depth,
        string[] PrerequisiteIds,
        string KoName,
        string EnName,
        string KoDescription,
        string EnDescription,
        string[] CompileTagIds,
        SerializableStatModifier[] Modifiers);

    private static Dictionary<string, RewardTableDefinition> CreateRewardTables()
    {
        var battle = CreateAsset<RewardTableDefinition>($"{ResourcesRoot}/Rewards/rewardtable_battle.asset", a =>
        {
            a.Id = "rewardtable_battle";
            a.NameKey = ContentLocalizationTables.BuildRewardTableNameKey(a.Id);
            a.DescriptionKey = $"content.reward_table.{ContentLocalizationTables.NormalizeId(a.Id)}.desc";
            a.Rewards = new List<RewardEntry>
            {
                MakeRewardEntry("reward.gold.10", RewardType.Gold, 10, "10 Gold", "골드 10"),
                MakeRewardEntry("augment_silver_guard", RewardType.TemporaryAugment, 1, "Guard Instinct", "수비 본능"),
                MakeRewardEntry("reward.echo.35", RewardType.Echo, 35, "35 Echo", "에코 35"),
            };
            UpsertStringEntry(ContentLocalizationTables.Rewards, a.NameKey, "전투 보상", "Battle Rewards");
            UpsertStringEntry(ContentLocalizationTables.Rewards, a.DescriptionKey, "전투 직후 선택 보상", "Post-battle choice rewards");
        });

        var expedition = CreateAsset<RewardTableDefinition>($"{ResourcesRoot}/Rewards/rewardtable_expedition_end.asset", a =>
        {
            a.Id = "rewardtable_expedition_end";
            a.NameKey = ContentLocalizationTables.BuildRewardTableNameKey(a.Id);
            a.DescriptionKey = $"content.reward_table.{ContentLocalizationTables.NormalizeId(a.Id)}.desc";
            a.Rewards = new List<RewardEntry>
            {
                MakeRewardEntry("reward.gold.30", RewardType.Gold, 30, "30 Gold", "골드 30"),
                MakeRewardEntry("reward.echo.60", RewardType.Echo, 60, "60 Echo", "에코 60"),
                MakeRewardEntry("augment_perm_legacy_blade", RewardType.TemporaryAugment, 1, "Legacy Blade", "유산의 검"),
            };
            UpsertStringEntry(ContentLocalizationTables.Rewards, a.NameKey, "원정 종료 보상", "Expedition End Rewards");
            UpsertStringEntry(ContentLocalizationTables.Rewards, a.DescriptionKey, "원정 종료 후 선택 보상", "End-of-expedition choice rewards");
        });

        return new Dictionary<string, RewardTableDefinition>
        {
            [battle.Id] = battle,
            [expedition.Id] = expedition,
        };
    }

    private static void CreateExpedition(Dictionary<string, RewardTableDefinition> rewardTables)
    {
        CreateAsset<ExpeditionDefinition>($"{ResourcesRoot}/Expeditions/expedition_mvp_demo.asset", a =>
        {
            a.Id = "expedition_mvp_demo";
            a.NameKey = ContentLocalizationTables.BuildExpeditionNameKey(a.Id);
            a.DescriptionKey = ContentLocalizationTables.BuildExpeditionDescriptionKey(a.Id);
            a.Nodes = new List<ExpeditionNodeDefinition>
            {
                MakeExpeditionNode(a.Id, "node_1", "Fork", "분기", "Choose the left or right route.", "좌우 경로를 고릅니다.", "Battle Reward", "전투 보상", rewardTables["rewardtable_battle"]),
                MakeExpeditionNode(a.Id, "node_2", "Skirmish", "교전", "A short battle route.", "짧은 전투 경로입니다.", "Battle Reward", "전투 보상", rewardTables["rewardtable_battle"]),
                MakeExpeditionNode(a.Id, "node_3", "Cache", "보급", "A supply cache route.", "보급품을 챙기는 경로입니다.", "Battle Reward", "전투 보상", rewardTables["rewardtable_battle"]),
                MakeExpeditionNode(a.Id, "node_4", "Elite", "정예", "A tougher elite route.", "강한 적과 싸우는 경로입니다.", "Battle Reward", "전투 보상", rewardTables["rewardtable_battle"]),
                MakeExpeditionNode(a.Id, "node_5", "Return", "복귀", "Return safely to town.", "안전하게 마을로 복귀합니다.", "Expedition End Reward", "원정 종료 보상", rewardTables["rewardtable_expedition_end"]),
            };
            UpsertStringEntry(ContentLocalizationTables.Expedition, a.NameKey, "MVP 데모 원정", "MVP Demo Expedition");
            UpsertStringEntry(ContentLocalizationTables.Expedition, a.DescriptionKey, "vertical slice 검증용 원정", "Expedition for vertical slice validation");
        });
    }

    private static TraitEntry MakeTrait(string archetypeId, string id, string enName, string koName, string statId, float value)
    {
        var nameKey = ContentLocalizationTables.BuildTraitNameKey(archetypeId, id);
        var descriptionKey = ContentLocalizationTables.BuildTraitDescriptionKey(archetypeId, id);
        UpsertStringEntry(ContentLocalizationTables.Traits, nameKey, koName, enName);
        UpsertStringEntry(ContentLocalizationTables.Traits, descriptionKey, koName, enName);
        return new TraitEntry
        {
            Id = id,
            NameKey = nameKey,
            DescriptionKey = descriptionKey,
            Modifiers = new List<SerializableStatModifier>
            {
                new() { StatId = statId, Value = value }
            }
        };
    }

    private static RewardEntry MakeRewardEntry(string id, RewardType type, int amount, string enLabel, string koLabel)
    {
        var labelKey = ContentLocalizationTables.BuildRewardLabelKey(id);
        UpsertStringEntry(ContentLocalizationTables.Rewards, labelKey, koLabel, enLabel);
        return new RewardEntry
        {
            Id = id,
            RewardType = type,
            Amount = amount,
            LabelKey = labelKey
        };
    }

    private static ExpeditionNodeDefinition MakeExpeditionNode(
        string expeditionId,
        string nodeId,
        string enLabel,
        string koLabel,
        string enDescription,
        string koDescription,
        string enRewardSummary,
        string koRewardSummary,
        RewardTableDefinition rewardTable)
    {
        var labelKey = ContentLocalizationTables.BuildExpeditionNodeLabelKey(expeditionId, nodeId);
        var descriptionKey = ContentLocalizationTables.BuildExpeditionNodeDescriptionKey(expeditionId, nodeId);
        var rewardKey = ContentLocalizationTables.BuildExpeditionNodeRewardKey(expeditionId, nodeId);
        UpsertStringEntry(ContentLocalizationTables.Expedition, labelKey, koLabel, enLabel);
        UpsertStringEntry(ContentLocalizationTables.Expedition, descriptionKey, koDescription, enDescription);
        UpsertStringEntry(ContentLocalizationTables.Expedition, rewardKey, koRewardSummary, enRewardSummary);
        return new ExpeditionNodeDefinition
        {
            Id = nodeId,
            LabelKey = labelKey,
            DescriptionKey = descriptionKey,
            RewardSummaryKey = rewardKey,
            RewardTable = rewardTable
        };
    }

    private static Dictionary<string, StableTagDefinition> CreateStableTags()
    {
        var definitions = new (string id, string enName, string koName)[]
        {
            ("shield", "Shield", "방패"),
            ("blade", "Blade", "검"),
            ("bow", "Bow", "활"),
            ("focus", "Focus", "초점구"),
            ("greatblade", "Greatblade", "대검"),
            ("polearm", "Polearm", "장병기"),
            ("vanguard", "Vanguard", "선봉"),
            ("duelist", "Duelist", "결투가"),
            ("ranger", "Ranger", "사수"),
            ("mystic", "Mystic", "신비술사"),
            ("frontline", "Frontline", "전열"),
            ("backline", "Backline", "후열"),
            ("support", "Support", "지원"),
            ("physical", "Physical", "물리"),
            ("magical", "Magical", "마법"),
            ("melee", "Melee", "근접"),
            ("projectile", "Projectile", "투사체"),
            ("aoe", "AOE", "광역"),
            ("zone", "Zone", "지대"),
            ("trap", "Trap", "함정"),
            ("aura", "Aura", "오라"),
            ("strike", "Strike", "타격"),
            ("burst", "Burst", "폭발"),
            ("heal", "Heal", "회복"),
            ("shield_skill", "Shield", "보호"),
            ("dash", "Dash", "돌진"),
            ("guard", "Guard", "수호"),
            ("mark", "Mark", "표식"),
            ("cleanse", "Cleanse", "해제"),
            ("burn", "Burn", "화상"),
            ("bleed", "Bleed", "출혈"),
            ("wound", "Wound", "상처"),
            ("sunder", "Sunder", "파쇄"),
            ("exposed", "Exposed", "노출"),
            ("slow", "Slow", "감속"),
            ("silence", "Silence", "침묵"),
            ("execute", "Execute", "마무리"),
            ("pierce", "Pierce", "관통"),
            ("chain", "Chain", "연쇄"),
            ("support_brutal", "Brutal", "잔혹"),
            ("support_swift", "Swift", "신속"),
            ("support_piercing", "Piercing", "관통 지원"),
            ("support_echo", "Echo", "메아리"),
            ("support_lingering", "Lingering", "잔향"),
            ("support_purifying", "Purifying", "정화"),
            ("support_guarded", "Guarded", "수호 강화"),
            ("support_executioner", "Executioner", "처형자"),
            ("support_longshot", "Longshot", "장사정"),
            ("support_siphon", "Siphon", "흡수"),
            ("support_anchored", "Anchored", "고정"),
            ("support_hunter_mark", "Hunter Mark", "사냥 표식"),
        };

        return definitions.ToDictionary(tuple => tuple.id, tuple => CreateAsset<StableTagDefinition>($"{ResourcesRoot}/StableTags/tag_{tuple.id}.asset", asset =>
        {
            asset.Id = tuple.id;
            asset.NameKey = $"content.tag.{ContentLocalizationTables.NormalizeId(tuple.id)}.name";
            UpsertStringEntry(ContentLocalizationTables.Tags, asset.NameKey, tuple.koName, tuple.enName);
        }));
    }

    private static void CreateSupportModifierSkills(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var definitions = new[]
        {
            new { Id = "support_brutal", EnName = "Brutal Support", KoName = "잔혹 지원", Include = new[] { "strike", "burst" }, Exclude = Array.Empty<string>(), Weapons = Array.Empty<string>(), Classes = Array.Empty<string>() },
            new { Id = "support_swift", EnName = "Swift Support", KoName = "신속 지원", Include = new[] { "projectile", "dash" }, Exclude = Array.Empty<string>(), Weapons = Array.Empty<string>(), Classes = Array.Empty<string>() },
            new { Id = "support_piercing", EnName = "Piercing Support", KoName = "관통 지원", Include = new[] { "projectile", "pierce" }, Exclude = new[] { "heal", "shield_skill" }, Weapons = new[] { "bow" }, Classes = Array.Empty<string>() },
            new { Id = "support_echo", EnName = "Echo Support", KoName = "메아리 지원", Include = new[] { "burst", "aura" }, Exclude = Array.Empty<string>(), Weapons = Array.Empty<string>(), Classes = Array.Empty<string>() },
            new { Id = "support_lingering", EnName = "Lingering Support", KoName = "잔향 지원", Include = new[] { "zone", "aoe" }, Exclude = Array.Empty<string>(), Weapons = Array.Empty<string>(), Classes = Array.Empty<string>() },
            new { Id = "support_purifying", EnName = "Purifying Support", KoName = "정화 지원", Include = new[] { "heal", "shield_skill", "cleanse" }, Exclude = new[] { "bleed", "execute" }, Weapons = Array.Empty<string>(), Classes = new[] { "mystic" } },
            new { Id = "support_guarded", EnName = "Guarded Support", KoName = "수호 지원", Include = new[] { "guard", "shield_skill" }, Exclude = Array.Empty<string>(), Weapons = new[] { "shield" }, Classes = new[] { "vanguard" } },
            new { Id = "support_executioner", EnName = "Executioner Support", KoName = "처형 지원", Include = new[] { "strike", "burst", "execute" }, Exclude = new[] { "heal", "shield_skill" }, Weapons = new[] { "blade" }, Classes = new[] { "duelist" } },
            new { Id = "support_longshot", EnName = "Longshot Support", KoName = "장사정 지원", Include = new[] { "projectile", "mark" }, Exclude = Array.Empty<string>(), Weapons = new[] { "bow" }, Classes = new[] { "ranger" } },
            new { Id = "support_siphon", EnName = "Siphon Support", KoName = "흡수 지원", Include = new[] { "burst", "burn" }, Exclude = Array.Empty<string>(), Weapons = new[] { "focus" }, Classes = new[] { "mystic" } },
            new { Id = "support_anchored", EnName = "Anchored Support", KoName = "고정 지원", Include = new[] { "shield_skill", "guard", "aura" }, Exclude = new[] { "dash" }, Weapons = new[] { "shield" }, Classes = new[] { "vanguard" } },
            new { Id = "support_hunter_mark", EnName = "Hunter Mark Support", KoName = "사냥 표식 지원", Include = new[] { "mark", "projectile" }, Exclude = new[] { "heal" }, Weapons = new[] { "bow" }, Classes = new[] { "ranger" } },
        };

        foreach (var definition in definitions)
        {
            CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{definition.Id}.asset", asset =>
            {
                asset.Id = definition.Id;
                asset.NameKey = ContentLocalizationTables.BuildSkillNameKey(definition.Id);
                asset.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(definition.Id);
                asset.Kind = SkillKindValue.Utility;
                asset.SlotKind = SkillSlotKindValue.Support;
                asset.DamageType = DamageTypeValue.Physical;
                asset.Delivery = SkillDeliveryValue.Aura;
                asset.TargetRule = SkillTargetRuleValue.Self;
                asset.Power = 0f;
                asset.Range = 0f;
                asset.PowerFlat = 0f;
                asset.CanCrit = false;
                asset.CompileTags = ResolveTags(tags, definition.Include.Concat(new[] { definition.Id }));
                asset.RuleModifierTags = ResolveTags(tags, new[] { definition.Id });
                asset.SupportAllowedTags = ResolveTags(tags, definition.Include);
                asset.SupportBlockedTags = ResolveTags(tags, definition.Exclude);
                asset.RequiredWeaponTags = ResolveTags(tags, definition.Weapons);
                asset.RequiredClassTags = ResolveTags(tags, definition.Classes);
                asset.AppliedStatuses = new List<StatusApplicationRule>();
                asset.CleanseProfileId = string.Empty;
                UpsertStringEntry(ContentLocalizationTables.Skills, asset.NameKey, definition.KoName, definition.EnName);
                UpsertStringEntry(ContentLocalizationTables.Skills, asset.DescriptionKey, $"{definition.KoName} modifier", $"{definition.EnName} modifier");
            });
        }
    }

    private static void PatchLaunchFloorItemsAndSkills(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        foreach (var item in LoadDefinitions<ItemBaseDefinition>($"{ResourcesRoot}/Items"))
        {
            var itemFamilyTag = NormalizeItemFamilyTag(item);
            var weaponFamilyTag = item.SlotType == ItemSlotType.Weapon ? itemFamilyTag : string.Empty;
            var craftCurrencyTag = string.IsNullOrWhiteSpace(item.CraftCurrencyTag)
                ? (item.IdentityKind == ItemIdentityValue.Unique ? "boss_sigil" : "ember_dust")
                : item.CraftCurrencyTag;
            var allowedCraftOperations = item.AllowedCraftOperations.Count == 0
                ? new List<CraftOperationKindValue>
                {
                    CraftOperationKindValue.Temper,
                    CraftOperationKindValue.Reforge,
                    CraftOperationKindValue.Seal,
                    item.IdentityKind == ItemIdentityValue.Unique ? CraftOperationKindValue.Imprint : CraftOperationKindValue.Salvage,
                    CraftOperationKindValue.Salvage,
                }.Distinct().ToList()
                : item.AllowedCraftOperations.ToList();

            item.ItemFamilyTag = itemFamilyTag;
            item.WeaponFamilyTag = weaponFamilyTag;
            item.AffixPoolTag = NormalizeAffixPoolTag(item, itemFamilyTag);
            item.CraftCategory = NormalizeCraftCategory(item, itemFamilyTag);
            item.CraftCurrencyTag = craftCurrencyTag;
            item.AllowedCraftOperations = allowedCraftOperations;
            var allowedClassTags = ResolveItemAllowedClassTags(tags, item.CraftCategory);
            if (allowedClassTags.Count > 0)
            {
                item.AllowedClassTags = allowedClassTags;
            }

            PatchSerializedItemContract(
                item,
                itemFamilyTag,
                weaponFamilyTag,
                item.AffixPoolTag,
                item.CraftCategory,
                craftCurrencyTag,
                allowedCraftOperations,
                item.CompileTags.Where(IsValidTagReference).Distinct().ToList(),
                item.RuleModifierTags.Where(IsValidTagReference).Distinct().ToList(),
                allowedClassTags,
                item.AllowedArchetypeTags.Where(IsValidTagReference).Distinct().ToList(),
                item.UniqueRuleTags.Where(IsValidTagReference).Distinct().ToList());
            EditorUtility.SetDirty(item);
        }

        PatchSkill("skill_guardian_core", tags, new[] { "strike", "guard", "shield_skill" }, Array.Empty<string>(), new[] { "shield" }, new[] { "vanguard" }, new[] { MakeStatus("status_guarded", "guarded", 2.0f, 0f) }, string.Empty);
        PatchSkill("skill_bulwark_core", tags, new[] { "guard", "shield_skill", "aura" }, Array.Empty<string>(), new[] { "shield" }, new[] { "vanguard" }, new[] { MakeStatus("status_barrier", "barrier", 0f, 6f) }, string.Empty);
        PatchSkill("skill_warden_utility", tags, new[] { "guard", "cleanse" }, Array.Empty<string>(), new[] { "shield" }, new[] { "vanguard" }, new[] { MakeStatus("status_unstoppable", "unstoppable", 0.8f, 0f) }, "break_and_unstoppable");
        PatchSkill("skill_slayer_core", tags, new[] { "strike", "bleed", "execute" }, Array.Empty<string>(), new[] { "blade" }, new[] { "duelist" }, new[] { MakeStatus("status_bleed", "bleed", 3f, 2f) }, string.Empty);
        PatchSkill("skill_raider_core", tags, new[] { "strike", "mark", "execute" }, Array.Empty<string>(), new[] { "blade" }, new[] { "duelist" }, new[] { MakeStatus("status_marked", "marked", 3f, 0f) }, string.Empty);
        PatchSkill("skill_hunter_utility", tags, new[] { "projectile", "slow", "mark" }, Array.Empty<string>(), new[] { "bow" }, new[] { "ranger" }, new[] { MakeStatus("status_slow", "slow", 2f, 0.25f) }, string.Empty);
        PatchSkill("skill_marksman_utility", tags, new[] { "projectile", "sunder", "pierce" }, Array.Empty<string>(), new[] { "bow" }, new[] { "ranger" }, new[] { MakeStatus("status_sunder", "sunder", 3f, 0.15f) }, string.Empty);
        PatchSkill("skill_scout_utility", tags, new[] { "projectile", "exposed", "trap" }, Array.Empty<string>(), new[] { "bow" }, new[] { "ranger" }, new[] { MakeStatus("status_exposed", "exposed", 2.5f, 0f) }, string.Empty);
        PatchSkill("skill_hexer_core", tags, new[] { "burst", "burn", "silence" }, Array.Empty<string>(), new[] { "focus" }, new[] { "mystic" }, new[] { MakeStatus("status_burn", "burn", 3f, 2f), MakeStatus("status_silence", "silence", 1.25f, 0f) }, string.Empty);
        PatchSkill("skill_priest_core", tags, new[] { "heal", "cleanse", "shield_skill" }, Array.Empty<string>(), new[] { "focus" }, new[] { "mystic" }, new[] { MakeStatus("status_barrier_priest", "barrier", 0f, 5f) }, "cleanse_control");
        PatchSkill("skill_shaman_core", tags, new[] { "burst", "zone", "burn" }, Array.Empty<string>(), new[] { "focus" }, new[] { "mystic" }, new[] { MakeStatus("status_burn_shaman", "burn", 4f, 1.5f) }, string.Empty);
        PatchLoopBRecruitmentSkills(tags);
    }

    private static void RepairResidualAuthoring(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        RepairReferenceDefinitionAuthoring();
        RepairItemTagReferences(tags);
        RepairAffixTagReferences();
        RepairPassiveNodeTagReferences();
        RepairSkillTagReferences(tags);
        RepairArchetypeTagReferences(tags);
        RepairAugmentTagReferences();
    }

    private static void ApplyLoopCGovernance(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        foreach (var skill in LoadDefinitions<SkillDefinitionAsset>($"{ResourcesRoot}/Skills"))
        {
            ApplyLoopCSkillGovernance(skill);
            EditorUtility.SetDirty(skill);
        }

        foreach (var affix in LoadDefinitions<AffixDefinition>($"{ResourcesRoot}/Affixes"))
        {
            ApplyLoopCAffixGovernance(affix);
            EditorUtility.SetDirty(affix);
        }

        foreach (var augment in LoadDefinitions<AugmentDefinition>($"{ResourcesRoot}/Augments"))
        {
            ApplyLoopCAugmentGovernance(augment);
            EditorUtility.SetDirty(augment);
        }

        foreach (var status in LoadDefinitions<StatusFamilyDefinition>($"{ResourcesRoot}/StatusFamilies"))
        {
            ApplyLoopCStatusGovernance(status);
            EditorUtility.SetDirty(status);
        }

        ApplyLoopCSynergyGovernance();
    }

    private static void ApplyLoopCArchetypeGovernance(UnitArchetypeDefinition archetype)
    {
        var rarity = LoopCContentGovernance.FromRecruitTier(archetype.RecruitTier);
        var roleProfile = ResolveRoleProfile(archetype);
        var target = LoopCContentGovernance.UnitBudgetTargets[rarity].Target;
        var vector = roleProfile switch
        {
            CombatRoleBudgetProfile.Vanguard => MakeBudgetVector(10, 4, 36, 24, 4, 6, 8, 8),
            CombatRoleBudgetProfile.Bruiser => MakeBudgetVector(24, 12, 28, 6, 14, 2, 6, 8),
            CombatRoleBudgetProfile.Duelist => MakeBudgetVector(16, 28, 10, 4, 22, 2, 6, 12),
            CombatRoleBudgetProfile.Ranger => MakeBudgetVector(28, 8, 8, 4, 12, 4, 8, 28),
            CombatRoleBudgetProfile.Arcanist => MakeBudgetVector(8, 30, 6, 20, 8, 6, 8, 14),
            CombatRoleBudgetProfile.Support => MakeBudgetVector(6, 6, 18, 8, 6, 32, 6, 18),
            CombatRoleBudgetProfile.Summoner => MakeBudgetVector(18, 8, 12, 6, 8, 24, 14, 10),
            _ => MakeBudgetVector(14, 12, 18, 8, 8, 8, 8, 24),
        };
        AdjustBudgetFinalScore(vector, target);

        var (threats, counters) = ResolveArchetypeTopology(roleProfile, rarity);
        archetype.BudgetCard = BuildBudgetCard(
            BudgetDomain.UnitBlueprint,
            rarity,
            PowerBand.Standard,
            roleProfile,
            vector,
            keywordCount: rarity switch
            {
                ContentRarity.Common => 2,
                ContentRarity.Rare => 3,
                _ => 4,
            },
            conditionClauseCount: rarity == ContentRarity.Common ? 1 : 2,
            ruleExceptionCount: rarity == ContentRarity.Epic ? 1 : 0,
            threats,
            counters);
    }

    private static void ApplyLoopCSkillGovernance(SkillDefinitionAsset skill)
    {
        var band = skill.SlotKind switch
        {
            SkillSlotKindValue.CoreActive => PowerBand.Standard,
            SkillSlotKindValue.UtilityActive when string.Equals(skill.Id, "skill_minor_heal", StringComparison.Ordinal) => PowerBand.Standard,
            SkillSlotKindValue.UtilityActive => PowerBand.Minor,
            SkillSlotKindValue.Passive => PowerBand.Minor,
            SkillSlotKindValue.Support => PowerBand.Minor,
            _ => PowerBand.Standard,
        };

        var target = LoopCContentGovernance.PowerBandTargets[band].Target;
        var counters = ResolveSkillCounterHints(skill);
        var vector = ResolveSkillBudgetVector(skill, target, counters);
        var rarity = band == PowerBand.Minor ? ContentRarity.Common : ContentRarity.Rare;
        skill.BudgetCard = BuildBudgetCard(
            BudgetDomain.Skill,
            rarity,
            band,
            CombatRoleBudgetProfile.None,
            vector,
            keywordCount: band == PowerBand.Minor ? 2 : 3,
            conditionClauseCount: band == PowerBand.Minor ? 1 : 2,
            ruleExceptionCount: 0,
            Array.Empty<ThreatPattern>(),
            counters);
    }

    private static void ApplyLoopCAffixGovernance(AffixDefinition affix)
    {
        var vector = affix.Category switch
        {
            AffixCategoryValue.OffenseFlat => MakeBudgetVector(3, 2, 0, 0, 0, 0, 0, 1),
            AffixCategoryValue.DefenseFlat => MakeBudgetVector(0, 0, 5, 0, 0, 0, 0, 1),
            AffixCategoryValue.DefenseScaling => MakeBudgetVector(0, 0, 4, 0, 0, 1, 0, 1),
            _ => MakeBudgetVector(0, 0, 1, 0, 1, 3, 0, 1),
        };
        AdjustBudgetFinalScore(vector, LoopCContentGovernance.AffixBudgetTargets[ContentRarity.Common].Target);
        affix.BudgetCard = BuildBudgetCard(
            BudgetDomain.Affix,
            ContentRarity.Common,
            PowerBand.Standard,
            CombatRoleBudgetProfile.None,
            vector,
            keywordCount: 1,
            conditionClauseCount: 0,
            ruleExceptionCount: 0,
            Array.Empty<ThreatPattern>(),
            Array.Empty<CounterToolContribution>());
    }

    private static void ApplyLoopCAugmentGovernance(AugmentDefinition augment)
    {
        augment.Rarity = NormalizeAuthoredContentRarity(augment.Rarity);
        var target = ResolveAuthoredAugmentBudgetTarget(augment);
        var band = augment.Rarity switch
        {
            ContentRarity.Common => PowerBand.Major,
            ContentRarity.Rare => PowerBand.Signature,
            _ => PowerBand.Keystone,
        };
        var vector = ResolveAugmentBudgetVector(augment, target);
        augment.BudgetCard = BuildBudgetCard(
            BudgetDomain.Augment,
            augment.Rarity,
            band,
            CombatRoleBudgetProfile.None,
            vector,
            keywordCount: augment.Rarity switch
            {
                ContentRarity.Common => 2,
                ContentRarity.Rare => 3,
                _ => 4,
            },
            conditionClauseCount: augment.Rarity == ContentRarity.Common ? 1 : 2,
            ruleExceptionCount: augment.Rarity == ContentRarity.Epic ? 1 : 0,
            Array.Empty<ThreatPattern>(),
            Array.Empty<CounterToolContribution>());
    }

    private static int ResolveAuthoredAugmentBudgetTarget(AugmentDefinition augment)
    {
        var target = LoopCContentGovernance.AugmentBudgetTargets[augment.Rarity].Target;
        return augment.Rarity == ContentRarity.Epic && !augment.IsPermanent
            ? target - LoopCContentGovernance.AugmentBudgetTargets[augment.Rarity].Tolerance
            : target;
    }

    private static ContentRarity NormalizeAuthoredContentRarity(ContentRarity rarity)
    {
        return LoopCContentGovernance.AugmentBudgetTargets.ContainsKey(rarity)
            ? rarity
            : ContentRarity.Epic;
    }

    private static void ApplyLoopCStatusGovernance(StatusFamilyDefinition status)
    {
        var isMinor = status.IsHardControl || string.Equals(status.Id, "root", StringComparison.Ordinal) || string.Equals(status.Id, "silence", StringComparison.Ordinal);
        var band = isMinor ? PowerBand.Minor : PowerBand.Micro;
        var counters = ResolveStatusCounterHints(status);
        var threats = ResolveStatusThreatHints(status);
        var vector = status.Group switch
        {
            StatusGroupValue.Control => MakeBudgetVector(0, 0, 0, isMinor ? 6 : 3, 0, 0, counters.Length > 0 ? 2 : 0, 0),
            StatusGroupValue.Attrition => MakeBudgetVector(4, 0, 0, 0, 0, 0, counters.Length > 0 ? 2 : 0, 0),
            StatusGroupValue.TacticalMark => MakeBudgetVector(0, 0, 0, 1, 0, 1, counters.Length > 0 ? 3 : 0, 0),
            StatusGroupValue.DefensiveBoon => MakeBudgetVector(0, 0, 2, 0, 0, 2, counters.Length > 0 ? 2 : 0, 0),
            _ => MakeBudgetVector(0, 0, 0, 0, 0, 0, 0, 0),
        };
        AdjustBudgetFinalScore(vector, LoopCContentGovernance.PowerBandTargets[band].Target);
        status.BudgetCard = BuildBudgetCard(
            BudgetDomain.Status,
            ContentRarity.Common,
            band,
            CombatRoleBudgetProfile.None,
            vector,
            keywordCount: band == PowerBand.Micro ? 1 : 2,
            conditionClauseCount: 0,
            ruleExceptionCount: 0,
            threats,
            counters);
    }

    private static void ApplyLoopCSynergyGovernance()
    {
        var tiers = LoadDefinitions<SynergyTierDefinition>($"{ResourcesRoot}/Synergies");
        foreach (var tier in tiers)
        {
            var isMajorBreakpoint = tier.Threshold != 2;
            var target = isMajorBreakpoint ? 18 : 12;
            var vector = isMajorBreakpoint
                ? MakeBudgetVector(6, 2, 4, 2, 0, 2, 0, 2)
                : MakeBudgetVector(3, 1, 3, 1, 0, 2, 0, 2);
            AdjustBudgetFinalScore(vector, target);
            tier.BudgetCard = BuildBudgetCard(
                BudgetDomain.SynergyBreakpoint,
                ContentRarity.Common,
                isMajorBreakpoint ? PowerBand.Major : PowerBand.Standard,
                CombatRoleBudgetProfile.None,
                vector,
                keywordCount: 2,
                conditionClauseCount: 1,
                ruleExceptionCount: 0,
                Array.Empty<ThreatPattern>(),
                Array.Empty<CounterToolContribution>());
            EditorUtility.SetDirty(tier);
        }

        var tiersById = tiers
            .Where(tier => !string.IsNullOrWhiteSpace(tier.Id))
            .ToDictionary(tier => tier.Id, StringComparer.Ordinal);
        foreach (var synergy in LoadDefinitions<SynergyDefinition>($"{ResourcesRoot}/Synergies"))
        {
            synergy.Tiers = FirstPlayableAuthoringContract.GetExpectedSynergyThresholds(synergy)
                .Select(threshold => tiersById.TryGetValue($"synergytier_{synergy.Id}_{threshold}", out var tier) ? tier : null)
                .Where(tier => tier != null)
                .Select(tier => tier!)
                .OrderBy(tier => tier.Threshold)
                .ToList();
            EditorUtility.SetDirty(synergy);
        }
    }

    private static CombatRoleBudgetProfile ResolveRoleProfile(UnitArchetypeDefinition archetype)
    {
        return archetype.Class?.Id switch
        {
            "vanguard" => CombatRoleBudgetProfile.Vanguard,
            "ranger" => CombatRoleBudgetProfile.Ranger,
            "mystic" when string.Equals(archetype.Id, "priest", StringComparison.Ordinal) || string.Equals(archetype.Id, "mirror_cantor", StringComparison.Ordinal) => CombatRoleBudgetProfile.Support,
            "mystic" => CombatRoleBudgetProfile.Arcanist,
            "duelist" when string.Equals(archetype.Id, "raider", StringComparison.Ordinal) || string.Equals(archetype.Id, "reaver", StringComparison.Ordinal) => CombatRoleBudgetProfile.Bruiser,
            "duelist" => CombatRoleBudgetProfile.Duelist,
            _ => CombatRoleBudgetProfile.Vanguard,
        };
    }

    private static (ThreatPattern[] Threats, CounterToolContribution[] Counters) ResolveArchetypeTopology(CombatRoleBudgetProfile profile, ContentRarity rarity)
    {
        var counters = profile switch
        {
            CombatRoleBudgetProfile.Vanguard => new[] { MakeCounter(CounterTool.InterceptPeel, CounterCoverageStrength.Standard) },
            CombatRoleBudgetProfile.Bruiser => new[] { MakeCounter(CounterTool.GuardBreakMultiHit, CounterCoverageStrength.Standard) },
            CombatRoleBudgetProfile.Duelist => new[] { MakeCounter(CounterTool.ArmorShred, CounterCoverageStrength.Standard) },
            CombatRoleBudgetProfile.Ranger => new[] { MakeCounter(CounterTool.TrackingArea, CounterCoverageStrength.Standard) },
            CombatRoleBudgetProfile.Arcanist => new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Standard) },
            CombatRoleBudgetProfile.Support => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard) },
            CombatRoleBudgetProfile.Summoner => new[] { MakeCounter(CounterTool.CleaveWaveclear, CounterCoverageStrength.Light) },
            _ => Array.Empty<CounterToolContribution>(),
        };

        if (rarity != ContentRarity.Common && counters.Length == 1 && profile is CombatRoleBudgetProfile.Arcanist or CombatRoleBudgetProfile.Support)
        {
            counters = counters.Concat(new[] { MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Light) }).ToArray();
        }

        var threats = profile switch
        {
            CombatRoleBudgetProfile.Vanguard => new[] { ThreatPattern.ArmorFrontline },
            CombatRoleBudgetProfile.Bruiser => new[] { ThreatPattern.DiveBackline },
            CombatRoleBudgetProfile.Duelist => new[] { ThreatPattern.DiveBackline },
            CombatRoleBudgetProfile.Ranger => new[] { ThreatPattern.EvasiveSkirmish },
            CombatRoleBudgetProfile.Arcanist => new[] { ThreatPattern.ControlChain },
            CombatRoleBudgetProfile.Support => new[] { ThreatPattern.SustainBall },
            CombatRoleBudgetProfile.Summoner => new[] { ThreatPattern.SwarmFlood },
            _ => Array.Empty<ThreatPattern>(),
        };
        return (threats, counters);
    }

    private static CounterToolContribution[] ResolveSkillCounterHints(SkillDefinitionAsset skill)
    {
        if (skill.Id.Contains("hunter_mark", StringComparison.Ordinal) || skill.Id.Contains("longshot", StringComparison.Ordinal) || skill.Id.Contains("piercing", StringComparison.Ordinal))
        {
            return new[] { MakeCounter(CounterTool.TrackingArea, CounterCoverageStrength.Light) };
        }

        if (skill.Id.Contains("hexer", StringComparison.Ordinal) || skill.AppliedStatuses.Any(status => string.Equals(status.StatusId, "silence", StringComparison.Ordinal)))
        {
            return new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Light) };
        }

        if (skill.Id.Contains("purifying", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(skill.CleanseProfileId))
        {
            return new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Light) };
        }

        return Array.Empty<CounterToolContribution>();
    }

    private static ThreatPattern[] ResolveStatusThreatHints(StatusFamilyDefinition status)
    {
        return status.Id switch
        {
            "guarded" => new[] { ThreatPattern.GuardBulwark },
            "barrier" => new[] { ThreatPattern.SustainBall },
            "marked" => new[] { ThreatPattern.DiveBackline },
            _ => Array.Empty<ThreatPattern>(),
        };
    }

    private static CounterToolContribution[] ResolveStatusCounterHints(StatusFamilyDefinition status)
    {
        return status.Id switch
        {
            "sunder" => new[] { MakeCounter(CounterTool.ArmorShred, CounterCoverageStrength.Standard) },
            "exposed" => new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Standard) },
            "wound" => new[] { MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Standard) },
            "unstoppable" => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard) },
            _ => Array.Empty<CounterToolContribution>(),
        };
    }

    private static BudgetVector ResolveSkillBudgetVector(SkillDefinitionAsset skill, int target, IReadOnlyList<CounterToolContribution> counters)
    {
        var vector = skill.Kind switch
        {
            SkillKindValue.Heal => MakeBudgetVector(0, 0, 0, 0, 0, target / 2, counters.Count > 0 ? 1 : 0, target / 2 - (counters.Count > 0 ? 1 : 0)),
            _ when skill.DamageType == DamageTypeValue.Magical => MakeBudgetVector(target / 4, target / 3, 0, Math.Max(1, target / 4), 0, 0, counters.Count > 0 ? 1 : 0, Math.Max(1, target - (target / 4 + target / 3 + Math.Max(1, target / 4) + (counters.Count > 0 ? 1 : 0)))),
            _ when skill.Delivery == SkillDeliveryValue.Projectile || skill.Delivery == SkillDeliveryValue.Ranged => MakeBudgetVector(target / 2, target / 6, 0, 0, 1, 0, counters.Count > 0 ? 1 : 0, target - (target / 2 + target / 6 + 1 + (counters.Count > 0 ? 1 : 0))),
            _ => MakeBudgetVector(target / 3, target / 3, 0, 0, Math.Max(1, target / 6), 0, counters.Count > 0 ? 1 : 0, Math.Max(1, target - (target / 3 + target / 3 + Math.Max(1, target / 6) + (counters.Count > 0 ? 1 : 0)))),
        };
        AdjustBudgetFinalScore(vector, target);
        return vector;
    }

    private static BudgetVector ResolveAugmentBudgetVector(AugmentDefinition augment, int target)
    {
        var statId = augment.Modifiers.FirstOrDefault()?.StatId ?? string.Empty;
        var vector = statId switch
        {
            "armor" or "max_health" or "resist" or "tenacity" => MakeBudgetVector(0, 0, target / 2, 0, 0, target / 6, 0, target / 3),
            "heal_power" => MakeBudgetVector(0, 0, target / 6, 0, 0, target / 2, 0, target / 3),
            "attack_speed" or "move_speed" or "attack_range" or "cooldown_recovery" => MakeBudgetVector(target / 3, target / 6, 0, 0, target / 6, 0, 0, target / 3),
            _ => MakeBudgetVector(target / 3, target / 3, 0, 0, 0, 0, 0, target / 3),
        };
        AdjustBudgetFinalScore(vector, target);
        return vector;
    }

    private static BudgetCard BuildBudgetCard(
        BudgetDomain domain,
        ContentRarity rarity,
        PowerBand powerBand,
        CombatRoleBudgetProfile roleProfile,
        BudgetVector vector,
        int keywordCount,
        int conditionClauseCount,
        int ruleExceptionCount,
        ThreatPattern[] threats,
        CounterToolContribution[] counters)
    {
        return new BudgetCard
        {
            Domain = domain,
            Rarity = rarity,
            PowerBand = powerBand,
            RoleProfile = roleProfile,
            Vector = vector,
            KeywordCount = keywordCount,
            ConditionClauseCount = conditionClauseCount,
            RuleExceptionCount = ruleExceptionCount,
            DeclaredThreatPatterns = threats,
            DeclaredCounterTools = counters,
            DeclaredFeatureFlags = ContentFeatureFlag.None,
        };
    }

    private static BudgetVector MakeBudgetVector(
        int sustainedDamage,
        int burstDamage,
        int durability,
        int control,
        int mobility,
        int support,
        int counterCoverage,
        int reliability,
        int economy = 0,
        int drawbackCredit = 0)
    {
        return new BudgetVector
        {
            SustainedDamage = sustainedDamage,
            BurstDamage = burstDamage,
            Durability = durability,
            Control = control,
            Mobility = mobility,
            Support = support,
            CounterCoverage = counterCoverage,
            Reliability = reliability,
            Economy = economy,
            DrawbackCredit = drawbackCredit,
        };
    }

    private static void AdjustBudgetFinalScore(BudgetVector vector, int target)
    {
        var delta = target - vector.FinalScore;
        vector.Reliability += delta;
        if (vector.Reliability < 0)
        {
            vector.DrawbackCredit += -vector.Reliability;
            vector.Reliability = 0;
        }
    }

    private static CounterToolContribution MakeCounter(CounterTool tool, CounterCoverageStrength strength)
    {
        return new CounterToolContribution
        {
            Tool = tool,
            Strength = strength,
        };
    }

    private static void RepairReferenceDefinitionAuthoring()
    {
        foreach (var definition in new[]
                 {
                     ("human", ContentLocalizationTables.BuildRaceNameKey("human"), ContentLocalizationTables.BuildRaceDescriptionKey("human")),
                     ("beastkin", ContentLocalizationTables.BuildRaceNameKey("beastkin"), ContentLocalizationTables.BuildRaceDescriptionKey("beastkin")),
                     ("undead", ContentLocalizationTables.BuildRaceNameKey("undead"), ContentLocalizationTables.BuildRaceDescriptionKey("undead")),
                 })
        {
            var asset = LoadDefinition<RaceDefinition>($"{ResourcesRoot}/Races/race_{definition.Item1}.asset");
            if (asset == null)
            {
                continue;
            }

            asset.Id = definition.Item1;
            asset.NameKey = definition.Item2;
            asset.DescriptionKey = definition.Item3;
            PatchSerializedLocalizedIdentity(asset, asset.Id, asset.NameKey, asset.DescriptionKey);
            EditorUtility.SetDirty(asset);
        }

        foreach (var definition in new[]
                 {
                     ("vanguard", ContentLocalizationTables.BuildClassNameKey("vanguard"), ContentLocalizationTables.BuildClassDescriptionKey("vanguard")),
                     ("duelist", ContentLocalizationTables.BuildClassNameKey("duelist"), ContentLocalizationTables.BuildClassDescriptionKey("duelist")),
                     ("ranger", ContentLocalizationTables.BuildClassNameKey("ranger"), ContentLocalizationTables.BuildClassDescriptionKey("ranger")),
                     ("mystic", ContentLocalizationTables.BuildClassNameKey("mystic"), ContentLocalizationTables.BuildClassDescriptionKey("mystic")),
                 })
        {
            var asset = LoadDefinition<ClassDefinition>($"{ResourcesRoot}/Classes/class_{definition.Item1}.asset");
            if (asset == null)
            {
                continue;
            }

            asset.Id = definition.Item1;
            asset.NameKey = definition.Item2;
            asset.DescriptionKey = definition.Item3;
            PatchSerializedLocalizedIdentity(asset, asset.Id, asset.NameKey, asset.DescriptionKey);
            EditorUtility.SetDirty(asset);
        }

        foreach (var archetypeId in new[] { "warden", "guardian", "bulwark", "slayer", "raider", "reaver", "hunter", "scout", "marksman", "priest", "hexer", "shaman" })
        {
            var asset = LoadDefinition<TraitPoolDefinition>($"{ResourcesRoot}/Traits/traitpool_{archetypeId}.asset");
            if (asset == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(asset.Id))
            {
                asset.Id = $"traitpool_{archetypeId}";
            }

            if (string.IsNullOrWhiteSpace(asset.ArchetypeId))
            {
                asset.ArchetypeId = archetypeId;
            }

            if (asset.PositiveTraits.Count == 0)
            {
                asset.PositiveTraits = new List<TraitEntry>
                {
                    MakeTrait(archetypeId, $"{archetypeId}_positive_brave", "Brave", "용맹", "phys_power", 2f),
                    MakeTrait(archetypeId, $"{archetypeId}_positive_sturdy", "Sturdy", "견고", "armor", 1f),
                    MakeTrait(archetypeId, $"{archetypeId}_positive_swift", "Swift", "민첩", "attack_speed", 1f),
                };
            }

            if (asset.NegativeTraits.Count == 0)
            {
                asset.NegativeTraits = new List<TraitEntry>
                {
                    MakeTrait(archetypeId, $"{archetypeId}_negative_frail", "Frail", "허약", "max_health", -3f),
                    MakeTrait(archetypeId, $"{archetypeId}_negative_clumsy", "Clumsy", "서툼", "phys_power", -1f),
                    MakeTrait(archetypeId, $"{archetypeId}_negative_slow", "Slow", "둔함", "attack_speed", -1f),
                };
            }

            PatchSerializedTraitPool(asset, asset.Id, asset.ArchetypeId, asset.PositiveTraits, asset.NegativeTraits);
            EditorUtility.SetDirty(asset);
        }
    }

    private static bool HasCanonicalAuthoringDrift()
    {
        var archetypes = LoadDefinitions<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes");
        if (archetypes.Any(asset =>
                string.IsNullOrWhiteSpace(asset.Id)
                || asset.Race == null
                || asset.Class == null
                || asset.TraitPool == null
                || asset.Skills == null
                || asset.Skills.Count == 0
                || asset.Skills.Any(skill => skill == null)))
        {
            return true;
        }

        var races = LoadDefinitions<RaceDefinition>($"{ResourcesRoot}/Races");
        if (races.Any(race => string.IsNullOrWhiteSpace(race.Id) || string.IsNullOrWhiteSpace(race.NameKey)))
        {
            return true;
        }

        var classes = LoadDefinitions<ClassDefinition>($"{ResourcesRoot}/Classes");
        if (classes.Any(@class => string.IsNullOrWhiteSpace(@class.Id) || string.IsNullOrWhiteSpace(@class.NameKey)))
        {
            return true;
        }

        var traitPools = LoadDefinitions<TraitPoolDefinition>($"{ResourcesRoot}/Traits");
        if (traitPools.Any(pool => string.IsNullOrWhiteSpace(pool.Id) || pool.PositiveTraits.Count < 3 || pool.NegativeTraits.Count < 3))
        {
            return true;
        }

        var skills = LoadDefinitions<SkillDefinitionAsset>($"{ResourcesRoot}/Skills");
        return skills.Any(skill =>
            string.IsNullOrWhiteSpace(skill.Id)
            || string.IsNullOrWhiteSpace(skill.NameKey)
            || string.IsNullOrWhiteSpace(skill.DescriptionKey));
    }

    private static void RepairItemTagReferences(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        foreach (var item in LoadDefinitions<ItemBaseDefinition>($"{ResourcesRoot}/Items"))
        {
            var itemFamilyTag = NormalizeItemFamilyTag(item);
            var weaponFamilyTag = item.SlotType == ItemSlotType.Weapon ? itemFamilyTag : string.Empty;
            var craftCategory = NormalizeCraftCategory(item, itemFamilyTag);
            var craftCurrencyTag = string.IsNullOrWhiteSpace(item.CraftCurrencyTag)
                ? (item.IdentityKind == ItemIdentityValue.Unique ? "boss_sigil" : "ember_dust")
                : item.CraftCurrencyTag;
            var allowedCraftOperations = item.AllowedCraftOperations.Count == 0
                ? new List<CraftOperationKindValue>
                {
                    CraftOperationKindValue.Temper,
                    CraftOperationKindValue.Reforge,
                    CraftOperationKindValue.Seal,
                    item.IdentityKind == ItemIdentityValue.Unique ? CraftOperationKindValue.Imprint : CraftOperationKindValue.Salvage,
                    CraftOperationKindValue.Salvage,
                }.Distinct().ToList()
                : item.AllowedCraftOperations.ToList();

            var compileTags = item.CompileTags.Where(IsValidTagReference).Distinct().ToList();
            if (compileTags.Count == 0 && item.SlotType == ItemSlotType.Weapon)
            {
                compileTags = ResolveTags(tags, new[] { weaponFamilyTag });
            }

            var ruleModifierTags = item.RuleModifierTags.Where(IsValidTagReference).Distinct().ToList();
            var allowedClassTags = item.AllowedClassTags.Where(IsValidTagReference).Distinct().ToList();
            if (allowedClassTags.Count == 0)
            {
                allowedClassTags = ResolveItemAllowedClassTags(tags, craftCategory);
            }

            var allowedArchetypeTags = item.AllowedArchetypeTags.Where(IsValidTagReference).Distinct().ToList();
            var uniqueRuleTags = item.UniqueRuleTags.Where(IsValidTagReference).Distinct().ToList();

            item.ItemFamilyTag = itemFamilyTag;
            item.WeaponFamilyTag = weaponFamilyTag;
            item.AffixPoolTag = NormalizeAffixPoolTag(item, itemFamilyTag);
            item.CraftCategory = craftCategory;
            item.CraftCurrencyTag = craftCurrencyTag;
            item.AllowedCraftOperations = allowedCraftOperations;
            item.CompileTags = compileTags;
            item.RuleModifierTags = ruleModifierTags;
            item.AllowedClassTags = allowedClassTags;
            item.AllowedArchetypeTags = allowedArchetypeTags;
            item.UniqueRuleTags = uniqueRuleTags;

            PatchSerializedItemContract(item, itemFamilyTag, weaponFamilyTag, item.AffixPoolTag, craftCategory, craftCurrencyTag, allowedCraftOperations, compileTags, ruleModifierTags, allowedClassTags, allowedArchetypeTags, uniqueRuleTags);
            EditorUtility.SetDirty(item);
        }
    }

    private static void RepairAffixTagReferences()
    {
        foreach (var asset in LoadDefinitions<AffixDefinition>($"{ResourcesRoot}/Affixes"))
        {
            asset.CompileTags = asset.CompileTags.Where(IsValidTagReference).Distinct().ToList();
            asset.RuleModifierTags = asset.RuleModifierTags.Where(IsValidTagReference).Distinct().ToList();
            PatchSerializedAffixTags(asset);
            EditorUtility.SetDirty(asset);
        }
    }

    private static void RepairPassiveNodeTagReferences()
    {
        foreach (var asset in LoadDefinitions<PassiveNodeDefinition>($"{ResourcesRoot}/PassiveNodes"))
        {
            asset.CompileTags = asset.CompileTags.Where(IsValidTagReference).Distinct().ToList();
            asset.RuleModifierTags = asset.RuleModifierTags.Where(IsValidTagReference).Distinct().ToList();
            asset.MutualExclusionTags = asset.MutualExclusionTags.Where(IsValidTagReference).Distinct().ToList();
            PatchSerializedPassiveNodeTags(asset);
            EditorUtility.SetDirty(asset);
        }
    }

    private static void RepairSkillTagReferences(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        foreach (var skill in LoadDefinitions<SkillDefinitionAsset>($"{ResourcesRoot}/Skills"))
        {
            var compileTags = skill.CompileTags.Where(IsValidTagReference).Distinct().ToList();
            var blockedTags = skill.SupportBlockedTags.Where(IsValidTagReference).Distinct().ToList();
            var weaponTags = skill.RequiredWeaponTags.Where(IsValidTagReference).Distinct().ToList();
            var classTags = skill.RequiredClassTags.Where(IsValidTagReference).Distinct().ToList();

            if (weaponTags.Count == 0 && TryInferSkillFamily(skill.Id, out var weaponTagId, out var classTagId, out var defaultCompileTagId))
            {
                weaponTags = ResolveTags(tags, new[] { weaponTagId });
                if (classTags.Count == 0)
                {
                    classTags = ResolveTags(tags, new[] { classTagId });
                }

                if (compileTags.Count == 0)
                {
                    compileTags = ResolveTags(tags, new[] { defaultCompileTagId });
                }
            }

            skill.CompileTags = compileTags;
            skill.SupportAllowedTags = skill.SupportAllowedTags.Where(IsValidTagReference).Distinct().ToList();
            skill.SupportBlockedTags = blockedTags;
            skill.RequiredWeaponTags = weaponTags;
            skill.RequiredClassTags = classTags;
            PatchSerializedSkillContract(skill, compileTags, blockedTags, weaponTags, classTags, skill.AppliedStatuses, skill.CleanseProfileId);
            EditorUtility.SetDirty(skill);
        }
    }

    private static void RepairArchetypeTagReferences(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        foreach (var archetype in LoadDefinitions<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes"))
        {
            var classId = archetype.Class?.Id ?? string.Empty;
            var recruitPlanTags = archetype.RecruitPlanTags.Where(IsValidTagReference).Distinct().ToList();
            if (recruitPlanTags.Count == 0)
            {
                recruitPlanTags = ResolveTags(tags, new[] { classId, archetype.RoleFamilyTag, archetype.PrimaryWeaponFamilyTag });
            }

            var scoutBiasTags = archetype.ScoutBiasTags.Where(IsValidTagReference).Distinct().ToList();
            if (scoutBiasTags.Count == 0)
            {
                scoutBiasTags = ResolveTags(tags, new[] { classId, archetype.RoleTag });
            }

            var supportModifierBiasTags = ResolveArchetypeSupportModifierBiasTags(archetype.Id, classId, tags);
            var recruitBannedPairings = BuildRecruitBannedPairings(classId)
                .GroupBy(pair => $"{pair.FlexActiveId}::{pair.FlexPassiveId}", StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            archetype.RecruitPlanTags = recruitPlanTags;
            archetype.ScoutBiasTags = scoutBiasTags;
            archetype.SupportModifierBiasTags = supportModifierBiasTags;
            archetype.RecruitBannedPairings = recruitBannedPairings;
            PatchSerializedArchetypeContract(archetype, recruitPlanTags, scoutBiasTags, supportModifierBiasTags, recruitBannedPairings);
            EditorUtility.SetDirty(archetype);
        }
    }

    private static void RepairAugmentTagReferences()
    {
        foreach (var augment in LoadDefinitions<AugmentDefinition>($"{ResourcesRoot}/Augments"))
        {
            augment.Tags = augment.Tags.Where(IsValidTagReference).Distinct().ToList();
            augment.BuildBiasTags = augment.BuildBiasTags.Where(IsValidTagReference).Distinct().ToList();
            augment.ProtectionTags = augment.ProtectionTags.Where(IsValidTagReference).Distinct().ToList();
            augment.MutualExclusionTags = augment.MutualExclusionTags.Where(IsValidTagReference).Distinct().ToList();
            augment.RequiresTags = augment.RequiresTags.Where(IsValidTagReference).Distinct().ToList();
            augment.RuleModifierTags = augment.RuleModifierTags.Where(IsValidTagReference).Distinct().ToList();
            PatchSerializedAugmentTags(augment);
            EditorUtility.SetDirty(augment);
        }
    }

    private static void CreateStatusCatalog()
    {
        var statuses = new[]
        {
            new { Id = "stun", En = "Stun", Ko = "기절", Group = StatusGroupValue.Control, Hard = true, Diminishing = true, Tenacity = true, Scale = 1f, RuleOnly = false },
            new { Id = "root", En = "Root", Ko = "속박", Group = StatusGroupValue.Control, Hard = true, Diminishing = true, Tenacity = true, Scale = 1f, RuleOnly = false },
            new { Id = "silence", En = "Silence", Ko = "침묵", Group = StatusGroupValue.Control, Hard = true, Diminishing = true, Tenacity = true, Scale = 0.5f, RuleOnly = false },
            new { Id = "slow", En = "Slow", Ko = "감속", Group = StatusGroupValue.Control, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "burn", En = "Burn", Ko = "화상", Group = StatusGroupValue.Attrition, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "bleed", En = "Bleed", Ko = "출혈", Group = StatusGroupValue.Attrition, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "wound", En = "Wound", Ko = "상처", Group = StatusGroupValue.Attrition, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "sunder", En = "Sunder", Ko = "파쇄", Group = StatusGroupValue.Attrition, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "marked", En = "Marked", Ko = "표식", Group = StatusGroupValue.TacticalMark, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "exposed", En = "Exposed", Ko = "노출", Group = StatusGroupValue.TacticalMark, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "barrier", En = "Barrier", Ko = "보호막", Group = StatusGroupValue.DefensiveBoon, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "guarded", En = "Guarded", Ko = "수호", Group = StatusGroupValue.DefensiveBoon, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
            new { Id = "unstoppable", En = "Unstoppable", Ko = "저지불가", Group = StatusGroupValue.DefensiveBoon, Hard = false, Diminishing = false, Tenacity = false, Scale = 0f, RuleOnly = false },
        };

        foreach (var definition in statuses)
        {
            CreateAsset<StatusFamilyDefinition>($"{ResourcesRoot}/StatusFamilies/status_family_{definition.Id}.asset", asset =>
            {
                asset.Id = definition.Id;
                asset.NameKey = ContentLocalizationTables.BuildStatusNameKey(definition.Id);
                asset.DescriptionKey = ContentLocalizationTables.BuildStatusDescriptionKey(definition.Id);
                asset.Group = definition.Group;
                asset.IsHardControl = definition.Hard;
                asset.UsesControlDiminishing = definition.Diminishing;
                asset.AffectedByTenacity = definition.Tenacity;
                asset.TenacityScale = definition.Scale;
                asset.IsRuleModifierOnly = definition.RuleOnly;
                asset.CompileTags = new List<string> { definition.Id };
                UpsertStringEntry(ContentLocalizationTables.Status, asset.NameKey, definition.Ko, definition.En);
                UpsertStringEntry(ContentLocalizationTables.Status, asset.DescriptionKey, $"{definition.Ko} 상태", $"{definition.En} status");
            });
        }

        CreateAsset<CleanseProfileDefinition>($"{ResourcesRoot}/CleanseProfiles/cleanse_profile_cleanse_basic.asset", asset =>
        {
            asset.Id = "cleanse_basic";
            asset.NameKey = ContentLocalizationTables.BuildCleanseProfileNameKey(asset.Id);
            asset.DescriptionKey = ContentLocalizationTables.BuildCleanseProfileDescriptionKey(asset.Id);
            asset.RemovesStatusIds = new List<string> { "slow", "burn", "bleed", "wound", "sunder", "marked", "exposed" };
            UpsertStringEntry(ContentLocalizationTables.Status, asset.NameKey, "기본 정화", "Basic Cleanse");
            UpsertStringEntry(ContentLocalizationTables.Status, asset.DescriptionKey, "기본 약화 해제", "Removes basic debuffs");
        });

        CreateAsset<CleanseProfileDefinition>($"{ResourcesRoot}/CleanseProfiles/cleanse_profile_cleanse_control.asset", asset =>
        {
            asset.Id = "cleanse_control";
            asset.NameKey = ContentLocalizationTables.BuildCleanseProfileNameKey(asset.Id);
            asset.DescriptionKey = ContentLocalizationTables.BuildCleanseProfileDescriptionKey(asset.Id);
            asset.RemovesStatusIds = new List<string> { "root", "silence", "slow", "burn", "bleed", "wound", "sunder", "marked", "exposed" };
            UpsertStringEntry(ContentLocalizationTables.Status, asset.NameKey, "제어 정화", "Control Cleanse");
            UpsertStringEntry(ContentLocalizationTables.Status, asset.DescriptionKey, "제어 포함 약화 해제", "Removes control plus basic debuffs");
        });

        CreateAsset<CleanseProfileDefinition>($"{ResourcesRoot}/CleanseProfiles/cleanse_profile_break_and_unstoppable.asset", asset =>
        {
            asset.Id = "break_and_unstoppable";
            asset.NameKey = ContentLocalizationTables.BuildCleanseProfileNameKey(asset.Id);
            asset.DescriptionKey = ContentLocalizationTables.BuildCleanseProfileDescriptionKey(asset.Id);
            asset.RemovesStatusIds = new List<string> { "stun", "root", "silence" };
            asset.RemovesOneHardControl = true;
            asset.GrantsUnstoppable = true;
            asset.GrantedUnstoppableDurationSeconds = 0.8f;
            UpsertStringEntry(ContentLocalizationTables.Status, asset.NameKey, "해방과 저지불가", "Break and Unstoppable");
            UpsertStringEntry(ContentLocalizationTables.Status, asset.DescriptionKey, "강한 제어 해제 후 저지불가 부여", "Breaks one hard control and grants unstoppable");
        });

        CreateAsset<ControlDiminishingRuleDefinition>($"{ResourcesRoot}/ControlDiminishingRules/control_diminishing_launch_floor.asset", asset =>
        {
            asset.Id = "control_diminishing_launch_floor";
            asset.NameKey = ContentLocalizationTables.BuildControlDiminishingNameKey(asset.Id);
            asset.DescriptionKey = ContentLocalizationTables.BuildControlDiminishingDescriptionKey(asset.Id);
            asset.ControlResistMultiplier = 0.5f;
            asset.WindowSeconds = 1.5f;
            asset.FullTenacityStatusIds = new List<string> { "stun", "root" };
            asset.PartialTenacityStatusIds = new List<string> { "silence" };
            UpsertStringEntry(ContentLocalizationTables.Status, asset.NameKey, "출시 제어 저감", "Launch Control Diminishing");
            UpsertStringEntry(ContentLocalizationTables.Status, asset.DescriptionKey, "하드 CC 종료 후 짧은 제어 저항 창", "Short control resist window after hard CC ends");
        });
    }

    private static void CreateTraitTokens()
    {
        var definitions = new[]
        {
            (Id: "trait_reroll_token", Reward: RewardType.Echo, En: "Echo Bundle", Ko: "에코 묶음"),
            (Id: "trait_lock_token", Reward: RewardType.TraitLockToken, En: "Trait Lock Token", Ko: "특성 고정 토큰"),
            (Id: "trait_purge_token", Reward: RewardType.TraitPurgeToken, En: "Trait Purge Token", Ko: "특성 제거 토큰"),
        };

        foreach (var definition in definitions)
        {
            CreateAsset<TraitTokenDefinition>($"{ResourcesRoot}/TraitTokens/{definition.Id}.asset", asset =>
            {
                asset.Id = definition.Id;
                asset.NameKey = ContentLocalizationTables.BuildTraitTokenNameKey(definition.Id);
                asset.DescriptionKey = ContentLocalizationTables.BuildTraitTokenDescriptionKey(definition.Id);
                asset.RewardType = definition.Reward;
                UpsertStringEntry(ContentLocalizationTables.Rewards, asset.NameKey, definition.Ko, definition.En);
                UpsertStringEntry(ContentLocalizationTables.Rewards, asset.DescriptionKey, $"{definition.Ko} 설명", $"{definition.En} description");
            });
        }
    }

    private static void CreateRewardSourcesAndDropTables()
    {
        CreateRewardSource("reward_source_skirmish", "Skirmish", "소규모 전투", RewardSourceKindValue.Skirmish, "drop_table_skirmish", new[] { RarityBracketValue.Common, RarityBracketValue.Advanced });
        CreateRewardSource("reward_source_elite", "Elite", "정예 전투", RewardSourceKindValue.Elite, "drop_table_elite", new[] { RarityBracketValue.Advanced, RarityBracketValue.Elite });
        CreateRewardSource("reward_source_boss", "Boss", "보스 전투", RewardSourceKindValue.Boss, "drop_table_boss", new[] { RarityBracketValue.Elite, RarityBracketValue.Boss });
        CreateRewardSource("reward_source_extract", "Extract", "철수 정산", RewardSourceKindValue.ExtractEndRun, "drop_table_extract", new[] { RarityBracketValue.Common, RarityBracketValue.Advanced });
        CreateRewardSource("reward_source_shrine_event", "Shrine/Event", "사당/이벤트", RewardSourceKindValue.ShrineEvent, "drop_table_shrine_event", new[] { RarityBracketValue.Advanced, RarityBracketValue.Elite });
        CreateRewardSource("reward_source_salvage", "Salvage", "분해", RewardSourceKindValue.SalvageDismantle, "drop_table_salvage", new[] { RarityBracketValue.Common, RarityBracketValue.Advanced });

        var siteAnswerLanes = GetSiteAnswerLaneContracts();

        CreateDropTable("drop_table_skirmish", "reward_source_skirmish", new[]
        {
            MakeLootEntry("gold_skirmish", RewardType.Gold, 8, RarityBracketValue.Common, true, 1),
            MakeLootEntry("ember_dust_skirmish", RewardType.EmberDust, 2, RarityBracketValue.Common, true, 1),
            MakeLootEntry("item_iron_sword", RewardType.Item, 1, RarityBracketValue.Advanced, false, 3),
            MakeLootEntry("skill_power_strike", RewardType.SkillShard, 1, RarityBracketValue.Advanced, false, 2),
        }.Concat(MakeRewardRouteEntries("skirmish", RewardType.EmberDust, 1, RarityBracketValue.Advanced, siteAnswerLanes)).ToArray());

        CreateDropTable("drop_table_elite", "reward_source_elite", new[]
        {
            MakeLootEntry("gold_elite", RewardType.Gold, 12, RarityBracketValue.Advanced, true, 1),
            MakeLootEntry("ember_dust_elite", RewardType.EmberDust, 4, RarityBracketValue.Advanced, true, 1),
            MakeLootEntry("item_bone_blade", RewardType.Item, 1, RarityBracketValue.Elite, false, 3),
            MakeLootEntry("skill_precision_shot", RewardType.SkillManual, 1, RarityBracketValue.Elite, false, 2),
        }.Concat(MakeRewardRouteEntries("elite", RewardType.Echo, 12, RarityBracketValue.Elite, siteAnswerLanes)).ToArray());

        CreateDropTable("drop_table_boss", "reward_source_boss", new[]
        {
            MakeLootEntry("boss_sigil_drop", RewardType.BossSigil, 1, RarityBracketValue.Boss, true, 1),
            MakeLootEntry("echo_crystal_boss", RewardType.EchoCrystal, 2, RarityBracketValue.Elite, true, 1),
            MakeLootEntry("item_prayer_bead", RewardType.Item, 1, RarityBracketValue.Boss, false, 2),
            MakeLootEntry("skill_minor_heal", RewardType.SkillManual, 1, RarityBracketValue.Boss, false, 2),
        }.Concat(MakeRewardRouteEntries("boss", RewardType.EchoCrystal, 1, RarityBracketValue.Boss, siteAnswerLanes)).ToArray());

        CreateDropTable("drop_table_extract", "reward_source_extract", new[]
        {
            MakeLootEntry("gold_extract", RewardType.Gold, 16, RarityBracketValue.Common, true, 1),
            MakeLootEntry("ember_dust_extract", RewardType.EmberDust, 5, RarityBracketValue.Advanced, true, 1),
            MakeLootEntry("echo_extract", RewardType.Echo, 35, RarityBracketValue.Advanced, false, 2),
        });

        CreateDropTable("drop_table_shrine_event", "reward_source_shrine_event", new[]
        {
            MakeLootEntry("trait_lock_token", RewardType.TraitLockToken, 1, RarityBracketValue.Elite, true, 1),
            MakeLootEntry("echo_crystal_shrine", RewardType.EchoCrystal, 1, RarityBracketValue.Advanced, false, 2),
        });

        CreateDropTable("drop_table_salvage", "reward_source_salvage", new[]
        {
            MakeLootEntry("ember_dust_salvage", RewardType.EmberDust, 3, RarityBracketValue.Common, true, 1),
            MakeLootEntry("echo_crystal_salvage", RewardType.EchoCrystal, 1, RarityBracketValue.Advanced, false, 2),
        });

        CreateAsset<LootBundleDefinition>($"{ResourcesRoot}/LootBundles/loot_bundle_extract_bonus.asset", asset =>
        {
            asset.Id = "loot_bundle_extract_bonus";
            asset.NameKey = ContentLocalizationTables.BuildLootBundleNameKey(asset.Id);
            asset.DescriptionKey = ContentLocalizationTables.BuildLootBundleDescriptionKey(asset.Id);
            asset.RewardSourceId = "reward_source_extract";
            asset.Entries = new List<LootBundleEntryDefinition>
            {
                MakeLootEntry("gold_extract_bonus", RewardType.Gold, 6, RarityBracketValue.Common, true, 1),
                MakeLootEntry("ember_extract_bonus", RewardType.EmberDust, 2, RarityBracketValue.Advanced, true, 1),
            };
            UpsertStringEntry(ContentLocalizationTables.Rewards, asset.NameKey, "철수 보너스 상자", "Extract Bonus Cache");
            UpsertStringEntry(ContentLocalizationTables.Rewards, asset.DescriptionKey, "철수 시 추가 정산", "Extra payout when extracting");
        });
    }

    private static IReadOnlyList<(string SiteId, string AnswerLaneId)> GetSiteAnswerLaneContracts()
    {
        return new[]
        {
            ("site_ashen_gate", "answer_lane_guard_anchor"),
            ("site_cinder_watch", "answer_lane_reach_anti_carry"),
            ("site_forgotten_warren", "answer_lane_anti_swarm_persistence"),
            ("site_twisted_den", "answer_lane_peel_cleanse"),
            ("site_ruined_crypt", "answer_lane_anti_sustain_finish"),
            ("site_grave_sanctum", "answer_lane_hybrid_boss_prep"),
        };
    }

    private static IEnumerable<LootBundleEntryDefinition> MakeRewardRouteEntries(
        string routeIdPrefix,
        RewardType rewardType,
        int amount,
        RarityBracketValue rarity,
        IReadOnlyList<(string SiteId, string AnswerLaneId)> siteAnswerLanes)
    {
        return siteAnswerLanes.Select(route => MakeLootEntry(
            $"{routeIdPrefix}_{route.SiteId}_{route.AnswerLaneId}",
            rewardType,
            amount,
            rarity,
            guaranteed: false,
            weight: 1,
            requiredContextTags: new[] { route.SiteId, route.AnswerLaneId }));
    }

    private static void CreateCampaignEncounterCatalog()
    {
        var sites = new[]
        {
            new { ChapterId = "chapter_ashen_frontier", ChapterOrder = 1, ChapterName = "Ashen Frontier", ChapterNameKo = "잿빛 변경", ChapterDesc = "Open the frontier routes.", ChapterDescKo = "변경의 첫 루트를 연다.", SiteId = "site_ashen_gate", SiteOrder = 1, SiteName = "Ashen Gate", SiteNameKo = "잿문", SiteDesc = "Vanguard outpost route.", SiteDescKo = "전열 진영과 충돌하는 루트.", Faction = "faction_ashen_vanguard", AnswerLaneId = "answer_lane_guard_anchor", EncounterFamilyIds = new[] { "encounter_family_bastion_front", "encounter_family_protect_carry", "encounter_family_control_cleanse", "encounter_family_sustain_grind" }, SkirmishA = new[] { "bastion_penitent", "warden", "hexer", "raider" }, SkirmishB = new[] { "guardian", "scout", "hexer", "hunter" }, Elite = new[] { "bulwark", "hunter", "mirror_cantor", "warden" }, BossCaptain = "bulwark", BossEscorts = new[] { "hunter", "hexer" }, OverlayId = "boss_overlay_ashen_gate" },
            new { ChapterId = "chapter_ashen_frontier", ChapterOrder = 1, ChapterName = "Ashen Frontier", ChapterNameKo = "잿빛 변경", ChapterDesc = "Open the frontier routes.", ChapterDescKo = "변경의 첫 루트를 연다.", SiteId = "site_cinder_watch", SiteOrder = 2, SiteName = "Cinder Watch", SiteNameKo = "잿불 망대", SiteDesc = "Ranged harassment route.", SiteDescKo = "원거리 압박이 많은 루트.", Faction = "faction_cinder_watch", AnswerLaneId = "answer_lane_reach_anti_carry", EncounterFamilyIds = new[] { "encounter_family_mark_execute", "encounter_family_weakside_dive", "encounter_family_tempo_swarm", "encounter_family_protect_carry" }, SkirmishA = new[] { "hunter", "scout", "pale_executor", "priest" }, SkirmishB = new[] { "marksman", "scout", "guardian", "hexer" }, Elite = new[] { "marksman", "bulwark", "scout", "hexer" }, BossCaptain = "marksman", BossEscorts = new[] { "scout", "priest" }, OverlayId = "boss_overlay_cinder_watch" },
            new { ChapterId = "chapter_warren_depths", ChapterOrder = 2, ChapterName = "Warren Depths", ChapterNameKo = "망실 굴지", ChapterDesc = "Break the hidden warrens.", ChapterDescKo = "숨은 굴을 돌파한다.", SiteId = "site_forgotten_warren", SiteOrder = 1, SiteName = "Forgotten Warren", SiteNameKo = "망실 굴", SiteDesc = "Attrition-heavy tunnels.", SiteDescKo = "소모전이 긴 터널.", Faction = "faction_warren_pack", AnswerLaneId = "answer_lane_anti_swarm_persistence", EncounterFamilyIds = new[] { "encounter_family_sustain_grind", "encounter_family_tempo_swarm", "encounter_family_summon_pressure", "encounter_family_weakside_dive" }, SkirmishA = new[] { "raider", "scout", "shaman", "hunter" }, SkirmishB = new[] { "reaver", "scout", "shaman", "hunter" }, Elite = new[] { "reaver", "raider", "scout", "shaman" }, BossCaptain = "reaver", BossEscorts = new[] { "rift_stalker", "shaman" }, OverlayId = "boss_overlay_forgotten_warren" },
            new { ChapterId = "chapter_warren_depths", ChapterOrder = 2, ChapterName = "Warren Depths", ChapterNameKo = "망실 굴지", ChapterDesc = "Break the hidden warrens.", ChapterDescKo = "숨은 굴을 돌파한다.", SiteId = "site_twisted_den", SiteOrder = 2, SiteName = "Twisted Den", SiteNameKo = "뒤틀린 소굴", SiteDesc = "Ambush-heavy den.", SiteDescKo = "기습이 많은 소굴.", Faction = "faction_twisted_den", AnswerLaneId = "answer_lane_peel_cleanse", EncounterFamilyIds = new[] { "encounter_family_weakside_dive", "encounter_family_mark_execute", "encounter_family_summon_pressure", "encounter_family_tempo_swarm" }, SkirmishA = new[] { "rift_stalker", "raider", "scout", "shaman" }, SkirmishB = new[] { "slayer", "hunter", "scout", "priest" }, Elite = new[] { "slayer", "reaver", "scout", "priest" }, BossCaptain = "slayer", BossEscorts = new[] { "scout", "priest" }, OverlayId = "boss_overlay_twisted_den" },
            new { ChapterId = "chapter_ruined_crypts", ChapterOrder = 3, ChapterName = "Ruined Crypts", ChapterNameKo = "폐허 묘실", ChapterDesc = "Seal the crypt lords.", ChapterDescKo = "묘실의 군주를 봉인한다.", SiteId = "site_ruined_crypt", SiteOrder = 1, SiteName = "Ruined Crypt", SiteNameKo = "폐허 묘실", SiteDesc = "Undead elite route.", SiteDescKo = "언데드 정예 루트.", Faction = "faction_bone_host", AnswerLaneId = "answer_lane_anti_sustain_finish", EncounterFamilyIds = new[] { "encounter_family_control_cleanse", "encounter_family_sustain_grind", "encounter_family_protect_carry", "encounter_family_summon_pressure" }, SkirmishA = new[] { "guardian", "hexer", "mirror_cantor", "hunter" }, SkirmishB = new[] { "bulwark", "hexer", "priest", "marksman" }, Elite = new[] { "bulwark", "hexer", "priest", "marksman" }, BossCaptain = "hexer", BossEscorts = new[] { "priest", "guardian" }, OverlayId = "boss_overlay_ruined_crypt" },
            new { ChapterId = "chapter_ruined_crypts", ChapterOrder = 3, ChapterName = "Ruined Crypts", ChapterNameKo = "폐허 묘실", ChapterDesc = "Seal the crypt lords.", ChapterDescKo = "묘실의 군주를 봉인한다.", SiteId = "site_grave_sanctum", SiteOrder = 2, SiteName = "Grave Sanctum", SiteNameKo = "무덤 성소", SiteDesc = "Final ritual route.", SiteDescKo = "최종 의식 루트.", Faction = "faction_grave_sanctum", AnswerLaneId = "answer_lane_hybrid_boss_prep", EncounterFamilyIds = new[] { "encounter_family_bastion_front", "encounter_family_control_cleanse", "encounter_family_mark_execute", "encounter_family_bastion_front" }, SkirmishA = new[] { "guardian", "hexer", "shaman", "marksman" }, SkirmishB = new[] { "bulwark", "hexer", "shaman", "hunter" }, Elite = new[] { "bulwark", "pale_executor", "hexer", "shaman" }, BossCaptain = "shaman", BossEscorts = new[] { "bastion_penitent", "hexer" }, OverlayId = "boss_overlay_grave_sanctum" },
        };

        foreach (var chapterGroup in sites.GroupBy(site => site.ChapterId))
        {
            var first = chapterGroup.First();
            CreateAsset<CampaignChapterDefinition>($"{ResourcesRoot}/CampaignChapters/{chapterGroup.Key}.asset", asset =>
            {
                asset.Id = chapterGroup.Key;
                asset.NameKey = ContentLocalizationTables.BuildCampaignChapterNameKey(asset.Id);
                asset.DescriptionKey = ContentLocalizationTables.BuildCampaignChapterDescriptionKey(asset.Id);
                asset.StoryOrder = first.ChapterOrder;
                asset.SiteIds = chapterGroup.OrderBy(site => site.SiteOrder).Select(site => site.SiteId).ToList();
                asset.UnlocksEndlessOnClear = first.ChapterOrder == 3;
                UpsertStringEntry(ContentLocalizationTables.Campaign, asset.NameKey, first.ChapterNameKo, first.ChapterName);
                UpsertStringEntry(ContentLocalizationTables.Campaign, asset.DescriptionKey, first.ChapterDescKo, first.ChapterDesc);
            });
        }

        foreach (var site in sites)
        {
            var encounterIds = new List<string> { $"{site.SiteId}_skirmish_1", $"{site.SiteId}_skirmish_2", $"{site.SiteId}_elite_1", $"{site.SiteId}_boss_1" };

            CreateAsset<ExpeditionSiteDefinition>($"{ResourcesRoot}/ExpeditionSites/{site.SiteId}.asset", asset =>
            {
                asset.Id = site.SiteId;
                asset.ChapterId = site.ChapterId;
                asset.NameKey = ContentLocalizationTables.BuildExpeditionSiteNameKey(asset.Id);
                asset.DescriptionKey = ContentLocalizationTables.BuildExpeditionSiteDescriptionKey(asset.Id);
                asset.SiteOrder = site.SiteOrder;
                asset.FactionId = site.Faction;
                asset.EncounterIds = encounterIds;
                asset.ExtractRewardSourceId = "reward_source_extract";
                asset.ThreatTier = site.ChapterOrder switch { 1 => ThreatTierValue.Tier1, 2 => ThreatTierValue.Tier2, _ => ThreatTierValue.Tier3 };
                UpsertStringEntry(ContentLocalizationTables.Campaign, asset.NameKey, site.SiteNameKo, site.SiteName);
                UpsertStringEntry(ContentLocalizationTables.Campaign, asset.DescriptionKey, site.SiteDescKo, site.SiteDesc);
            });

            CreateBossOverlay(site.OverlayId, site.SiteName, site.SiteNameKo);
            CreateEnemySquad($"{site.SiteId}_skirmish_1_squad", site.Faction, site.SkirmishA, TeamPostureTypeValue.StandardAdvance, ThreatTierValue.Tier1, 1);
            CreateEnemySquad($"{site.SiteId}_skirmish_2_squad", site.Faction, site.SkirmishB, TeamPostureTypeValue.StandardAdvance, ThreatTierValue.Tier1, 1);
            CreateEnemySquad($"{site.SiteId}_elite_1_squad", site.Faction, site.Elite, TeamPostureTypeValue.CollapseWeakSide, ThreatTierValue.Tier2, 2);
            CreateBossSquad($"{site.SiteId}_boss_1_squad", site.Faction, site.BossCaptain, site.BossEscorts, ThreatTierValue.Tier3, 3);

            CreateEncounter($"{site.SiteId}_skirmish_1", site.SiteId, site.Faction, EncounterKindValue.Skirmish, $"{site.SiteId}_skirmish_1_squad", string.Empty, "reward_source_skirmish", ThreatTierValue.Tier1, 1, 1, "chapter_entry", site.EncounterFamilyIds[0], site.AnswerLaneId);
            CreateEncounter($"{site.SiteId}_skirmish_2", site.SiteId, site.Faction, EncounterKindValue.Skirmish, $"{site.SiteId}_skirmish_2_squad", string.Empty, "reward_source_skirmish", ThreatTierValue.Tier1, 1, 1, "chapter_entry", site.EncounterFamilyIds[1], site.AnswerLaneId);
            CreateEncounter($"{site.SiteId}_elite_1", site.SiteId, site.Faction, EncounterKindValue.Elite, $"{site.SiteId}_elite_1_squad", string.Empty, "reward_source_elite", ThreatTierValue.Tier2, 2, 2, "site_mid", site.EncounterFamilyIds[2], site.AnswerLaneId);
            CreateEncounter($"{site.SiteId}_boss_1", site.SiteId, site.Faction, EncounterKindValue.Boss, $"{site.SiteId}_boss_1_squad", site.OverlayId, "reward_source_boss", ThreatTierValue.Tier3, 3, 3, "site_boss", site.EncounterFamilyIds[3], site.AnswerLaneId);
        }
    }

    private static void CreateRewardSource(string id, string enName, string koName, RewardSourceKindValue kind, string dropTableId, IReadOnlyList<RarityBracketValue> rarityBrackets)
    {
        var asset = CreateAsset<RewardSourceDefinition>($"{ResourcesRoot}/RewardSources/{id}.asset", definition =>
        {
            definition.Id = id;
            definition.NameKey = ContentLocalizationTables.BuildRewardSourceNameKey(id);
            definition.DescriptionKey = ContentLocalizationTables.BuildRewardSourceDescriptionKey(id);
            definition.Kind = kind;
            definition.DropTableId = dropTableId;
            definition.UsesRewardCards = kind != RewardSourceKindValue.SalvageDismantle;
            definition.AllowedRarityBrackets = rarityBrackets.ToList();
            UpsertStringEntry(ContentLocalizationTables.Rewards, definition.NameKey, koName, enName);
            UpsertStringEntry(ContentLocalizationTables.Rewards, definition.DescriptionKey, $"{koName} 자동 드롭", $"{enName} automatic drops");
        });
        PatchSerializedRewardSource(asset, asset.Id, asset.NameKey, asset.DescriptionKey, asset.Kind, asset.DropTableId, asset.UsesRewardCards, asset.AllowedRarityBrackets);
    }

    private static void CreateDropTable(string id, string rewardSourceId, IReadOnlyList<LootBundleEntryDefinition> entries)
    {
        var asset = CreateAsset<DropTableDefinition>($"{ResourcesRoot}/DropTables/{id}.asset", definition =>
        {
            definition.Id = id;
            definition.NameKey = ContentLocalizationTables.BuildDropTableNameKey(id);
            definition.DescriptionKey = ContentLocalizationTables.BuildDropTableDescriptionKey(id);
            definition.RewardSourceId = rewardSourceId;
            definition.Entries = entries.ToList();
            UpsertStringEntry(ContentLocalizationTables.Rewards, definition.NameKey, id, id);
            UpsertStringEntry(ContentLocalizationTables.Rewards, definition.DescriptionKey, $"{rewardSourceId} 드롭 테이블", $"{rewardSourceId} drop table");
        });
        PatchSerializedDropTable(asset, asset.Id, asset.NameKey, asset.DescriptionKey, asset.RewardSourceId, asset.Entries);
    }

    private static LootBundleEntryDefinition MakeLootEntry(
        string id,
        RewardType type,
        int amount,
        RarityBracketValue rarity,
        bool guaranteed,
        int weight,
        IEnumerable<string>? requiredContextTags = null)
    {
        return new LootBundleEntryDefinition
        {
            Id = id,
            RewardType = type,
            Amount = amount,
            RarityBracket = rarity,
            IsGuaranteed = guaranteed,
            Weight = weight,
            RequiredContextTags = (requiredContextTags ?? Array.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }

    private static void CreateBossOverlay(string id, string siteName, string siteNameKo)
    {
        CreateAsset<BossOverlayDefinition>($"{ResourcesRoot}/BossOverlays/{id}.asset", asset =>
        {
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildBossOverlayNameKey(id);
            asset.DescriptionKey = ContentLocalizationTables.BuildBossOverlayDescriptionKey(id);
            asset.PhaseTrigger = BossPhaseTriggerValue.HealthBelowHalf;
            asset.ThreatCost = 1;
            asset.SignatureAuraTag = "boss_aura_curse";
            asset.SignatureUtilityTag = "boss_utility_revive";
            asset.RewardDropTags = new List<string> { "boss", id };
            asset.AppliedStatuses = new List<StatusApplicationRule> { MakeStatus($"{id}_guarded", "guarded", 999f, 0f) };
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.NameKey, $"{siteNameKo} 우두머리 위상", $"{siteName} Boss Overlay");
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.DescriptionKey, "위상 전환, 오라, 보상 태그를 소유", "Owns phase trigger, aura, and reward tags");
        });
    }

    private static void CreateEnemySquad(string id, string factionId, IReadOnlyList<string> archetypes, TeamPostureTypeValue posture, ThreatTierValue threatTier, int threatCost)
    {
        CreateAsset<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/{id}.asset", asset =>
        {
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildEnemySquadNameKey(id);
            asset.DescriptionKey = ContentLocalizationTables.BuildEnemySquadDescriptionKey(id);
            asset.FactionId = factionId;
            asset.EnemyPosture = posture;
            asset.ThreatTier = threatTier;
            asset.ThreatCost = threatCost;
            asset.RewardDropTags = new List<string> { factionId, threatTier.ToString().ToLowerInvariant() };
            asset.Members = archetypes.Select((archetypeId, index) => new EnemySquadMemberDefinition
            {
                Id = $"{id}_{index + 1}",
                ArchetypeId = archetypeId,
                Anchor = index switch
                {
                    0 => DeploymentAnchorValue.FrontTop,
                    1 => DeploymentAnchorValue.FrontBottom,
                    2 => DeploymentAnchorValue.BackTop,
                    _ => DeploymentAnchorValue.BackBottom,
                },
                Role = EnemySquadMemberRoleValue.Unit,
                PositiveTraitId = string.Empty,
                NegativeTraitId = string.Empty,
                RuleModifierTags = new List<string>()
            }).ToList();
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.NameKey, id, id);
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.DescriptionKey, $"{factionId} 분대", $"{factionId} squad");
        });
    }

    private static void CreateBossSquad(string id, string factionId, string captain, IReadOnlyList<string> escorts, ThreatTierValue threatTier, int threatCost)
    {
        CreateAsset<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/{id}.asset", asset =>
        {
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildEnemySquadNameKey(id);
            asset.DescriptionKey = ContentLocalizationTables.BuildEnemySquadDescriptionKey(id);
            asset.FactionId = factionId;
            asset.EnemyPosture = TeamPostureTypeValue.ProtectCarry;
            asset.ThreatTier = threatTier;
            asset.ThreatCost = threatCost;
            asset.RewardDropTags = new List<string> { factionId, "boss" };
            asset.Members = new List<EnemySquadMemberDefinition>
            {
                new() { Id = $"{id}_captain", ArchetypeId = captain, Anchor = DeploymentAnchorValue.FrontCenter, Role = EnemySquadMemberRoleValue.Captain },
                new() { Id = $"{id}_escort_1", ArchetypeId = escorts[0], Anchor = DeploymentAnchorValue.BackTop, Role = EnemySquadMemberRoleValue.Escort },
                new() { Id = $"{id}_escort_2", ArchetypeId = escorts[1], Anchor = DeploymentAnchorValue.BackBottom, Role = EnemySquadMemberRoleValue.Escort },
            };
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.NameKey, $"{id} 우두머리 분대", $"{id} boss squad");
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.DescriptionKey, "우두머리 + 호위 구조", "Boss captain plus escorts");
        });
    }

    private static void CreateEncounter(string id, string siteId, string factionId, EncounterKindValue kind, string squadId, string overlayId, string rewardSourceId, ThreatTierValue threatTier, int threatCost, int threatSkulls, string difficultyBand, string encounterFamilyId, string answerLaneId)
    {
        CreateAsset<EncounterDefinition>($"{ResourcesRoot}/Encounters/{id}.asset", asset =>
        {
            asset.Id = id;
            asset.NameKey = ContentLocalizationTables.BuildEncounterNameKey(id);
            asset.DescriptionKey = ContentLocalizationTables.BuildEncounterDescriptionKey(id);
            asset.Kind = kind;
            asset.SiteId = siteId;
            asset.EnemySquadTemplateId = squadId;
            asset.BossOverlayId = overlayId;
            asset.RewardSourceId = rewardSourceId;
            asset.FactionId = factionId;
            asset.ThreatTier = threatTier;
            asset.ThreatCost = threatCost;
            asset.ThreatSkulls = threatSkulls;
            asset.DifficultyBand = difficultyBand;
            asset.RewardDropTags = new List<string> { rewardSourceId, factionId, kind.ToString().ToLowerInvariant(), encounterFamilyId, answerLaneId };
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.NameKey, id, id);
            UpsertStringEntry(ContentLocalizationTables.Encounters, asset.DescriptionKey, $"{siteId} {kind}", $"{siteId} {kind}");
        });
    }

    private static void PatchSkill(string id, IReadOnlyDictionary<string, StableTagDefinition> tags, IEnumerable<string> compileTags, IEnumerable<string> blockedTags, IEnumerable<string> requiredWeaponTags, IEnumerable<string> requiredClassTags, IEnumerable<StatusApplicationRule> statuses, string cleanseProfileId)
    {
        var skill = LoadDefinition<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/{id}.asset");
        if (skill == null)
        {
            return;
        }

        var resolvedCompileTags = ResolveTags(tags, compileTags);
        var resolvedBlockedTags = ResolveTags(tags, blockedTags);
        var resolvedWeaponTags = ResolveTags(tags, requiredWeaponTags);
        var resolvedClassTags = ResolveTags(tags, requiredClassTags);
        var resolvedStatuses = statuses.ToList();

        skill.CompileTags = resolvedCompileTags;
        skill.SupportAllowedTags = resolvedCompileTags;
        skill.SupportBlockedTags = resolvedBlockedTags;
        skill.RequiredWeaponTags = resolvedWeaponTags;
        skill.RequiredClassTags = resolvedClassTags;
        skill.AppliedStatuses = resolvedStatuses;
        skill.CleanseProfileId = cleanseProfileId;
        PatchSerializedSkillContract(skill, resolvedCompileTags, resolvedBlockedTags, resolvedWeaponTags, resolvedClassTags, resolvedStatuses, cleanseProfileId);
        EditorUtility.SetDirty(skill);
    }

    private static StatusApplicationRule MakeStatus(string id, string statusId, float durationSeconds, float magnitude)
    {
        return new StatusApplicationRule
        {
            Id = id,
            StatusId = statusId,
            DurationSeconds = durationSeconds,
            Magnitude = magnitude,
            MaxStacks = 1,
            RefreshDurationOnReapply = true,
        };
    }

    private static List<StableTagDefinition> ResolveTags(IReadOnlyDictionary<string, StableTagDefinition> tags, IEnumerable<string> tagIds)
    {
        return tagIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && tags.ContainsKey(id))
            .Select(id => tags[id])
            .Distinct()
            .ToList();
    }

    private static bool TryInferSkillFamily(string skillId, out string weaponTagId, out string classTagId, out string defaultCompileTagId)
    {
        weaponTagId = string.Empty;
        classTagId = string.Empty;
        defaultCompileTagId = string.Empty;

        if (skillId.Contains("vanguard", StringComparison.Ordinal)
            || skillId.Contains("guardian", StringComparison.Ordinal)
            || skillId.Contains("bulwark", StringComparison.Ordinal)
            || skillId.Contains("warden", StringComparison.Ordinal))
        {
            weaponTagId = "shield";
            classTagId = "vanguard";
            defaultCompileTagId = "guard";
            return true;
        }

        if (skillId.Contains("duelist", StringComparison.Ordinal)
            || skillId.Contains("slayer", StringComparison.Ordinal)
            || skillId.Contains("raider", StringComparison.Ordinal)
            || skillId.Contains("reaver", StringComparison.Ordinal))
        {
            weaponTagId = "blade";
            classTagId = "duelist";
            defaultCompileTagId = "strike";
            return true;
        }

        if (skillId.Contains("ranger", StringComparison.Ordinal)
            || skillId.Contains("hunter", StringComparison.Ordinal)
            || skillId.Contains("scout", StringComparison.Ordinal)
            || skillId.Contains("marksman", StringComparison.Ordinal))
        {
            weaponTagId = "bow";
            classTagId = "ranger";
            defaultCompileTagId = "projectile";
            return true;
        }

        if (skillId.Contains("mystic", StringComparison.Ordinal)
            || skillId.Contains("priest", StringComparison.Ordinal)
            || skillId.Contains("hexer", StringComparison.Ordinal)
            || skillId.Contains("shaman", StringComparison.Ordinal))
        {
            weaponTagId = "focus";
            classTagId = "mystic";
            defaultCompileTagId = "burst";
            return true;
        }

        return false;
    }

    private static string NormalizeItemFamilyTag(ItemBaseDefinition item)
    {
        if (!string.IsNullOrWhiteSpace(item.ItemFamilyTag))
        {
            return item.ItemFamilyTag;
        }

        return item.SlotType switch
        {
            ItemSlotType.Weapon => InferWeaponFamily(item.Id),
            ItemSlotType.Armor => "armor",
            _ => "accessory"
        };
    }

    private static string NormalizeAffixPoolTag(ItemBaseDefinition item, string itemFamilyTag)
    {
        if (!string.IsNullOrWhiteSpace(item.AffixPoolTag))
        {
            return item.AffixPoolTag;
        }

        return item.SlotType switch
        {
            ItemSlotType.Weapon => $"pool_{itemFamilyTag}",
            ItemSlotType.Armor => "pool_armor",
            _ => "pool_accessory"
        };
    }

    private static string NormalizeCraftCategory(ItemBaseDefinition item, string itemFamilyTag)
    {
        if (!string.IsNullOrWhiteSpace(item.CraftCategory))
        {
            return item.CraftCategory;
        }

        return itemFamilyTag switch
        {
            "shield" => "vanguard",
            "blade" => "duelist",
            "bow" => "ranger",
            "focus" => "mystic",
            "armor" => "armor",
            _ => "accessory"
        };
    }

    private static string InferWeaponFamily(string itemId)
    {
        if (itemId.Contains("shield", StringComparison.Ordinal))
        {
            return "shield";
        }

        if (itemId.Contains("bow", StringComparison.Ordinal))
        {
            return "bow";
        }

        if (itemId.Contains("focus", StringComparison.Ordinal) || itemId.Contains("bead", StringComparison.Ordinal))
        {
            return "focus";
        }

        return "blade";
    }

    private static List<StableTagDefinition> ResolveItemAllowedClassTags(IReadOnlyDictionary<string, StableTagDefinition> tags, string craftCategory)
    {
        return craftCategory switch
        {
            "vanguard" or "duelist" or "ranger" or "mystic" => ResolveTags(tags, new[] { craftCategory }),
            _ => new List<StableTagDefinition>()
        };
    }

    private static List<StableTagDefinition> ResolveArchetypeSupportModifierBiasTags(
        string archetypeId,
        string classId,
        IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        var tagIds = archetypeId switch
        {
            "warden" => new[] { "support_guarded", "support_anchored" },
            "guardian" => new[] { "support_anchored", "support_guarded" },
            "bulwark" => new[] { "support_purifying", "support_anchored" },
            "slayer" => new[] { "support_executioner", "support_brutal" },
            "raider" => new[] { "support_brutal", "support_executioner" },
            "reaver" => new[] { "support_swift", "support_brutal" },
            "hunter" => new[] { "support_longshot", "support_piercing" },
            "scout" => new[] { "support_swift", "support_longshot" },
            "marksman" => new[] { "support_longshot", "support_piercing" },
            "priest" => new[] { "support_purifying", "support_echo" },
            "hexer" => new[] { "support_siphon", "support_lingering" },
            "shaman" => new[] { "support_lingering", "support_echo" },
            "rift_stalker" => new[] { "support_swift", "support_longshot" },
            "bastion_penitent" => new[] { "support_anchored", "support_guarded" },
            "pale_executor" => new[] { "support_executioner", "support_brutal" },
            "mirror_cantor" => new[] { "support_purifying", "support_echo" },
            _ => classId switch
            {
                "vanguard" => new[] { "support_guarded", "support_purifying", "support_anchored" },
                "duelist" => new[] { "support_executioner", "support_swift", "support_brutal" },
                "ranger" => new[] { "support_longshot", "support_hunter_mark", "support_piercing" },
                "mystic" => new[] { "support_purifying", "support_echo", "support_lingering", "support_siphon" },
                _ => Array.Empty<string>()
            }
        };

        return ResolveTags(tags, tagIds);
    }

    private static void PatchSerializedItemContract(
        ItemBaseDefinition item,
        string itemFamilyTag,
        string weaponFamilyTag,
        string affixPoolTag,
        string craftCategory,
        string craftCurrencyTag,
        IReadOnlyList<CraftOperationKindValue> allowedCraftOperations,
        IReadOnlyList<StableTagDefinition> compileTags,
        IReadOnlyList<StableTagDefinition> ruleModifierTags,
        IReadOnlyList<StableTagDefinition> allowedClassTags,
        IReadOnlyList<StableTagDefinition> allowedArchetypeTags,
        IReadOnlyList<StableTagDefinition> uniqueRuleTags)
    {
        var serializedObject = new SerializedObject(item);
        serializedObject.FindProperty(nameof(ItemBaseDefinition.ItemFamilyTag))!.stringValue = itemFamilyTag;
        serializedObject.FindProperty(nameof(ItemBaseDefinition.WeaponFamilyTag))!.stringValue = weaponFamilyTag;
        serializedObject.FindProperty(nameof(ItemBaseDefinition.AffixPoolTag))!.stringValue = affixPoolTag;
        serializedObject.FindProperty(nameof(ItemBaseDefinition.CraftCategory))!.stringValue = craftCategory;
        serializedObject.FindProperty(nameof(ItemBaseDefinition.CraftCurrencyTag))!.stringValue = craftCurrencyTag;
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(ItemBaseDefinition.CompileTags)), compileTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(ItemBaseDefinition.RuleModifierTags)), ruleModifierTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(ItemBaseDefinition.AllowedClassTags)), allowedClassTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(ItemBaseDefinition.AllowedArchetypeTags)), allowedArchetypeTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(ItemBaseDefinition.UniqueRuleTags)), uniqueRuleTags);
        SetEnumArray(serializedObject.FindProperty(nameof(ItemBaseDefinition.AllowedCraftOperations)), allowedCraftOperations.Select(value => (int)value));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedArchetypeContract(
        UnitArchetypeDefinition archetype,
        IReadOnlyList<StableTagDefinition> recruitPlanTags,
        IReadOnlyList<StableTagDefinition> scoutBiasTags,
        IReadOnlyList<StableTagDefinition> supportModifierBiasTags,
        IReadOnlyList<RecruitBannedPairingDefinition> recruitBannedPairings)
    {
        var serializedObject = new SerializedObject(archetype);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(UnitArchetypeDefinition.RecruitPlanTags)), recruitPlanTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(UnitArchetypeDefinition.ScoutBiasTags)), scoutBiasTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(UnitArchetypeDefinition.SupportModifierBiasTags)), supportModifierBiasTags);
        SetRecruitBannedPairingArray(serializedObject.FindProperty(nameof(UnitArchetypeDefinition.RecruitBannedPairings)), recruitBannedPairings);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedAugmentTags(AugmentDefinition augment)
    {
        var serializedObject = new SerializedObject(augment);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AugmentDefinition.Tags)), augment.Tags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AugmentDefinition.BuildBiasTags)), augment.BuildBiasTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AugmentDefinition.ProtectionTags)), augment.ProtectionTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AugmentDefinition.MutualExclusionTags)), augment.MutualExclusionTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AugmentDefinition.RequiresTags)), augment.RequiresTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AugmentDefinition.RuleModifierTags)), augment.RuleModifierTags);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedAffixTags(AffixDefinition affix)
    {
        var serializedObject = new SerializedObject(affix);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AffixDefinition.CompileTags)), affix.CompileTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(AffixDefinition.RuleModifierTags)), affix.RuleModifierTags);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedPassiveNodeTags(PassiveNodeDefinition node)
    {
        var serializedObject = new SerializedObject(node);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(PassiveNodeDefinition.CompileTags)), node.CompileTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(PassiveNodeDefinition.RuleModifierTags)), node.RuleModifierTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(PassiveNodeDefinition.MutualExclusionTags)), node.MutualExclusionTags);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedSkillContract(
        SkillDefinitionAsset skill,
        IReadOnlyList<StableTagDefinition> compileTags,
        IReadOnlyList<StableTagDefinition> blockedTags,
        IReadOnlyList<StableTagDefinition> requiredWeaponTags,
        IReadOnlyList<StableTagDefinition> requiredClassTags,
        IReadOnlyList<StatusApplicationRule> statuses,
        string cleanseProfileId)
    {
        var serializedObject = new SerializedObject(skill);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(SkillDefinitionAsset.CompileTags)), compileTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(SkillDefinitionAsset.SupportAllowedTags)), compileTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(SkillDefinitionAsset.SupportBlockedTags)), blockedTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(SkillDefinitionAsset.RequiredWeaponTags)), requiredWeaponTags);
        SetObjectReferenceArray(serializedObject.FindProperty(nameof(SkillDefinitionAsset.RequiredClassTags)), requiredClassTags);
        SetStatusRuleArray(serializedObject.FindProperty(nameof(SkillDefinitionAsset.AppliedStatuses)), statuses);
        serializedObject.FindProperty(nameof(SkillDefinitionAsset.CleanseProfileId))!.stringValue = cleanseProfileId ?? string.Empty;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectReferenceArray(SerializedProperty? property, IReadOnlyList<StableTagDefinition> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }

    private static void SetObjectReferenceArray<T>(SerializedProperty? property, IReadOnlyList<T> values)
        where T : UnityEngine.Object
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }

    private static void SetRecruitBannedPairingArray(SerializedProperty? property, IReadOnlyList<RecruitBannedPairingDefinition> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(nameof(RecruitBannedPairingDefinition.FlexActiveId))!.stringValue = values[index].FlexActiveId;
            element.FindPropertyRelative(nameof(RecruitBannedPairingDefinition.FlexPassiveId))!.stringValue = values[index].FlexPassiveId;
        }
    }

    private static bool IsValidTagReference(StableTagDefinition? tag)
    {
        return tag != null && !string.IsNullOrWhiteSpace(tag.Id);
    }

    private static bool HasBrokenTagRefs(IEnumerable<StableTagDefinition> tags)
    {
        return tags.Any(tag => !IsValidTagReference(tag));
    }

    private static List<T> LoadDefinitions<T>(string folder) where T : ScriptableObject
    {
        return AssetDatabase.FindAssets(string.Empty, new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .Select(LoadDefinition<T>)
            .Where(asset => asset != null)
            .ToList()!;
    }

    private static Dictionary<string, T> LoadDefinitionsById<T>(string folder) where T : ScriptableObject
    {
        return LoadDefinitions<T>(folder)
            .Select(asset => new { Asset = asset, Id = asset.GetType().GetField("Id")?.GetValue(asset) as string })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Id))
            .ToDictionary(entry => entry.Id!, entry => entry.Asset, StringComparer.Ordinal);
    }

    private static T? LoadDefinition<T>(string path) where T : ScriptableObject
    {
        var typed = AssetDatabase.LoadAssetAtPath<T>(path);
        if (typed != null)
        {
            return typed;
        }

        typed = AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
        if (typed != null)
        {
            return typed;
        }

        typed = AssetDatabase.LoadMainAssetAtPath(path) as T;
        if (typed != null)
        {
            return typed;
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>().FirstOrDefault();
    }

    private static void SetEnumArray(SerializedProperty? property, IEnumerable<int> values)
    {
        if (property == null)
        {
            return;
        }

        var entries = values.ToList();
        property.arraySize = entries.Count;
        for (var index = 0; index < entries.Count; index++)
        {
            property.GetArrayElementAtIndex(index).enumValueIndex = entries[index];
        }
    }

    private static void SetStatusRuleArray(SerializedProperty? property, IReadOnlyList<StatusApplicationRule> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(nameof(StatusApplicationRule.Id))!.stringValue = values[index].Id;
            element.FindPropertyRelative(nameof(StatusApplicationRule.StatusId))!.stringValue = values[index].StatusId;
            element.FindPropertyRelative(nameof(StatusApplicationRule.DurationSeconds))!.floatValue = values[index].DurationSeconds;
            element.FindPropertyRelative(nameof(StatusApplicationRule.Magnitude))!.floatValue = values[index].Magnitude;
            element.FindPropertyRelative(nameof(StatusApplicationRule.MaxStacks))!.intValue = values[index].MaxStacks;
            element.FindPropertyRelative(nameof(StatusApplicationRule.RefreshDurationOnReapply))!.boolValue = values[index].RefreshDurationOnReapply;
            element.FindPropertyRelative(nameof(StatusApplicationRule.StackCap))!.intValue = values[index].StackCap;
            element.FindPropertyRelative(nameof(StatusApplicationRule.StackPolicy))!.enumValueIndex = (int)values[index].StackPolicy;
            element.FindPropertyRelative(nameof(StatusApplicationRule.RefreshPolicy))!.enumValueIndex = (int)values[index].RefreshPolicy;
            element.FindPropertyRelative(nameof(StatusApplicationRule.ProcAttributionPolicy))!.enumValueIndex = (int)values[index].ProcAttributionPolicy;
            element.FindPropertyRelative(nameof(StatusApplicationRule.OwnershipPolicy))!.enumValueIndex = (int)values[index].OwnershipPolicy;
        }
    }

    private static void PatchSerializedLocalizedIdentity(ScriptableObject asset, string id, string nameKey, string descriptionKey)
    {
        var serializedObject = new SerializedObject(asset);
        var idProperty = serializedObject.FindProperty("Id");
        if (idProperty != null)
        {
            idProperty.stringValue = id ?? string.Empty;
        }

        var nameProperty = serializedObject.FindProperty("NameKey");
        if (nameProperty != null)
        {
            nameProperty.stringValue = nameKey ?? string.Empty;
        }

        var descriptionProperty = serializedObject.FindProperty("DescriptionKey");
        if (descriptionProperty != null)
        {
            descriptionProperty.stringValue = descriptionKey ?? string.Empty;
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedTraitPool(
        TraitPoolDefinition asset,
        string id,
        string archetypeId,
        IReadOnlyList<TraitEntry> positiveTraits,
        IReadOnlyList<TraitEntry> negativeTraits)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty(nameof(TraitPoolDefinition.Id))!.stringValue = id ?? string.Empty;
        serializedObject.FindProperty(nameof(TraitPoolDefinition.ArchetypeId))!.stringValue = archetypeId ?? string.Empty;
        SetTraitEntryArray(serializedObject.FindProperty(nameof(TraitPoolDefinition.PositiveTraits)), positiveTraits);
        SetTraitEntryArray(serializedObject.FindProperty(nameof(TraitPoolDefinition.NegativeTraits)), negativeTraits);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetTraitEntryArray(SerializedProperty? property, IReadOnlyList<TraitEntry> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            var entry = values[index];
            element.FindPropertyRelative(nameof(TraitEntry.Id))!.stringValue = entry.Id ?? string.Empty;
            element.FindPropertyRelative(nameof(TraitEntry.NameKey))!.stringValue = entry.NameKey ?? string.Empty;
            element.FindPropertyRelative(nameof(TraitEntry.DescriptionKey))!.stringValue = entry.DescriptionKey ?? string.Empty;
            SetSerializableModifierArray(element.FindPropertyRelative(nameof(TraitEntry.Modifiers)), entry.Modifiers);
        }
    }

    private static void SetSerializableModifierArray(SerializedProperty? property, IReadOnlyList<SerializableStatModifier> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            element.FindPropertyRelative(nameof(SerializableStatModifier.StatId))!.stringValue = values[index].StatId ?? string.Empty;
            element.FindPropertyRelative(nameof(SerializableStatModifier.Op))!.enumValueIndex = (int)values[index].Op;
            element.FindPropertyRelative(nameof(SerializableStatModifier.Value))!.floatValue = values[index].Value;
        }
    }

    private static void PatchSerializedRewardSource(
        RewardSourceDefinition asset,
        string id,
        string nameKey,
        string descriptionKey,
        RewardSourceKindValue kind,
        string dropTableId,
        bool usesRewardCards,
        IReadOnlyList<RarityBracketValue> allowedRarityBrackets)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty(nameof(RewardSourceDefinition.Id))!.stringValue = id ?? string.Empty;
        serializedObject.FindProperty(nameof(RewardSourceDefinition.NameKey))!.stringValue = nameKey ?? string.Empty;
        serializedObject.FindProperty(nameof(RewardSourceDefinition.DescriptionKey))!.stringValue = descriptionKey ?? string.Empty;
        serializedObject.FindProperty(nameof(RewardSourceDefinition.Kind))!.enumValueIndex = (int)kind;
        serializedObject.FindProperty(nameof(RewardSourceDefinition.DropTableId))!.stringValue = dropTableId ?? string.Empty;
        serializedObject.FindProperty(nameof(RewardSourceDefinition.UsesRewardCards))!.boolValue = usesRewardCards;
        SetEnumArray(serializedObject.FindProperty(nameof(RewardSourceDefinition.AllowedRarityBrackets)), allowedRarityBrackets.Select(value => (int)value));
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PatchSerializedDropTable(
        DropTableDefinition asset,
        string id,
        string nameKey,
        string descriptionKey,
        string rewardSourceId,
        IReadOnlyList<LootBundleEntryDefinition> entries)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty(nameof(DropTableDefinition.Id))!.stringValue = id ?? string.Empty;
        serializedObject.FindProperty(nameof(DropTableDefinition.NameKey))!.stringValue = nameKey ?? string.Empty;
        serializedObject.FindProperty(nameof(DropTableDefinition.DescriptionKey))!.stringValue = descriptionKey ?? string.Empty;
        serializedObject.FindProperty(nameof(DropTableDefinition.RewardSourceId))!.stringValue = rewardSourceId ?? string.Empty;
        SetLootBundleEntryArray(serializedObject.FindProperty(nameof(DropTableDefinition.Entries)), entries);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetLootBundleEntryArray(SerializedProperty? property, IReadOnlyList<LootBundleEntryDefinition> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            var element = property.GetArrayElementAtIndex(index);
            var entry = values[index];
            element.FindPropertyRelative(nameof(LootBundleEntryDefinition.Id))!.stringValue = entry.Id ?? string.Empty;
            element.FindPropertyRelative(nameof(LootBundleEntryDefinition.RewardType))!.enumValueIndex = (int)entry.RewardType;
            element.FindPropertyRelative(nameof(LootBundleEntryDefinition.Amount))!.intValue = entry.Amount;
            element.FindPropertyRelative(nameof(LootBundleEntryDefinition.RarityBracket))!.enumValueIndex = (int)entry.RarityBracket;
            element.FindPropertyRelative(nameof(LootBundleEntryDefinition.Weight))!.intValue = entry.Weight;
            element.FindPropertyRelative(nameof(LootBundleEntryDefinition.IsGuaranteed))!.boolValue = entry.IsGuaranteed;
            SetStringArray(element.FindPropertyRelative(nameof(LootBundleEntryDefinition.RequiredContextTags)), entry.RequiredContextTags);
        }
    }

    private static void SetStringArray(SerializedProperty? property, IReadOnlyList<string> values)
    {
        if (property == null)
        {
            return;
        }

        property.arraySize = values.Count;
        for (var index = 0; index < values.Count; index++)
        {
            property.GetArrayElementAtIndex(index).stringValue = values[index] ?? string.Empty;
        }
    }

    public static void SeedNarrativeContent()
    {
        EnsureFolder(StoryEventsDir);
        EnsureFolder(DialogueSequencesDir);
        EnsureFolder(ChapterBeatsDir);
        EnsureFolder(HeroLoreDir);

        NarrativeSeedData.ValidateSeedCountsOrThrow();

        var storyTableCollection = EnsureStoryTableCollection();
        SeedStoryStringEntries(storyTableCollection, NarrativeSeedData.Strings);
        SeedDialogueSequences(NarrativeSeedData.DialogueSequences);
        SeedHeroLore(NarrativeSeedData.HeroLore);
        SeedStoryEvents(NarrativeSeedData.StoryEvents);
        SeedChapterBeats(NarrativeSeedData.ChapterBeats);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        NarrativeContentValidator.ValidateOrThrow();
    }

    private static StringTableCollection EnsureStoryTableCollection()
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(StoryTableName);
        if (collection != null)
        {
            return collection;
        }

        var locales = LocalizationEditorSettings.GetLocales()
            .Where(locale => locale != null && (locale.Identifier.Code == "ko" || locale.Identifier.Code == "en"))
            .ToList();
        if (locales.Count != 2)
        {
            throw new InvalidOperationException("Missing ko/en locales for narrative string table seeding.");
        }

        collection = LocalizationEditorSettings.CreateStringTableCollection(
            StoryTableName,
            LocalizationFoundationBootstrap.StringTableRoot,
            locales);

        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(collection.SharedData);
        return collection;
    }

    private static void SeedStoryStringEntries(
        StringTableCollection collection,
        IReadOnlyList<StoryStringSeed> seeds)
    {
        var koTable = GetOrCreateStringTable(collection, "ko");
        var enTable = GetOrCreateStringTable(collection, "en");

        foreach (var seed in seeds)
        {
            UpsertNarrativeStringEntry(koTable, seed.Key, seed.Ko, preserveExistingValueWhenIncomingIsEmpty: false);
            UpsertNarrativeStringEntry(enTable, seed.Key, seed.En, preserveExistingValueWhenIncomingIsEmpty: true);
        }

        LocalizationEditorSettings.SetPreloadTableFlag(koTable, true);
        LocalizationEditorSettings.SetPreloadTableFlag(enTable, true);
        EditorUtility.SetDirty(collection);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(koTable);
        EditorUtility.SetDirty(enTable);
    }

    private static void SeedDialogueSequences(IReadOnlyList<DialogueSequenceSeed> seeds)
    {
        foreach (var seed in seeds)
        {
            var assetPath = $"{DialogueSequencesDir}/{seed.Id}.asset";
            CreateAsset<DialogueSequenceDefinition>(assetPath, asset => ApplyDialogueSequence(asset, assetPath, seed));
        }
    }

    private static void SeedHeroLore(IReadOnlyList<HeroLoreSeed> seeds)
    {
        foreach (var seed in seeds)
        {
            var assetPath = $"{HeroLoreDir}/{seed.Id}.asset";
            CreateAsset<HeroLoreDefinition>(assetPath, asset => ApplyHeroLore(asset, seed));
        }
    }

    private static void SeedStoryEvents(IReadOnlyList<StoryEventSeed> seeds)
    {
        foreach (var seed in seeds)
        {
            var assetPath = $"{StoryEventsDir}/{seed.Id}.asset";
            CreateAsset<StoryEventDefinition>(assetPath, asset => ApplyStoryEvent(asset, assetPath, seed));
        }
    }

    private static void SeedChapterBeats(IReadOnlyList<ChapterBeatSeed> seeds)
    {
        foreach (var seed in seeds)
        {
            var assetPath = $"{ChapterBeatsDir}/{seed.Id}.asset";
            CreateAsset<ChapterBeatDefinition>(assetPath, asset => ApplyChapterBeat(asset, seed));
        }
    }

    private static void ApplyDialogueSequence(
        DialogueSequenceDefinition asset,
        string assetPath,
        DialogueSequenceSeed seed)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = seed.Id;

        var lineAssets = SyncChildAssets<DialogueLineDefinition, DialogueLineSeed>(
            assetPath,
            "line",
            seed.Lines,
            (lineAsset, lineSeed, index) => ApplyDialogueLine(lineAsset, seed.Id, lineSeed, index));

        SetObjectReferenceArray(serializedObject.FindProperty("_lines"), lineAssets);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyDialogueLine(
        DialogueLineDefinition asset,
        string sequenceId,
        DialogueLineSeed seed,
        int lineIndex)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = $"{sequenceId}.line.{lineIndex:D2}";
        serializedObject.FindProperty("_speakerId")!.stringValue = seed.SpeakerId;
        serializedObject.FindProperty("_textKey")!.stringValue = NarrativeSeedData.BuildDialogueTextKey(sequenceId, lineIndex);
        serializedObject.FindProperty("_emote")!.stringValue = seed.Emotion ?? string.Empty;
        serializedObject.FindProperty("_autoAdvanceHint")!.floatValue = seed.AutoAdvanceHint;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyStoryEvent(
        StoryEventDefinition asset,
        string assetPath,
        StoryEventSeed seed)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = seed.Id;
        serializedObject.FindProperty("_moment")!.enumValueIndex = (int)seed.Moment;
        serializedObject.FindProperty("_priority")!.intValue = seed.Priority;
        serializedObject.FindProperty("_oncePolicy")!.enumValueIndex = (int)seed.OncePolicy;
        serializedObject.FindProperty("_presentationKey")!.stringValue = seed.PresentationKey;

        var conditionAssets = SyncChildAssets<StoryConditionDefinition, StoryConditionSeed>(
            assetPath,
            "condition",
            seed.Conditions,
            (conditionAsset, conditionSeed, index) => ApplyStoryCondition(conditionAsset, seed.Id, conditionSeed, index));
        SetObjectReferenceArray(serializedObject.FindProperty("_conditions"), conditionAssets);

        var effectiveEffects = BuildEffectiveStoryEffects(seed);
        var effectAssets = SyncChildAssets<StoryEffectDefinition, StoryEffectSeed>(
            assetPath,
            "effect",
            effectiveEffects,
            (effectAsset, effectSeed, index) => ApplyStoryEffect(effectAsset, seed.Id, effectSeed, index));
        SetObjectReferenceArray(serializedObject.FindProperty("_effects"), effectAssets);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static StoryEffectSeed[] BuildEffectiveStoryEffects(StoryEventSeed seed)
    {
        if (string.IsNullOrWhiteSpace(seed.PresentationKey))
        {
            return seed.Effects;
        }

        var effects = new List<StoryEffectSeed>(seed.Effects.Length + 1);
        effects.AddRange(seed.Effects);
        effects.Add(new StoryEffectSeed(
            StoryEffectKind.EnqueuePresentation,
            NarrativeSeedData.InferPresentationKind(seed.PresentationKey).ToString()));
        return effects.ToArray();
    }

    private static void ApplyStoryCondition(
        StoryConditionDefinition asset,
        string eventId,
        StoryConditionSeed seed,
        int index)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = $"{eventId}.condition.{index:D2}";
        serializedObject.FindProperty("_kind")!.enumValueIndex = (int)seed.Kind;
        serializedObject.FindProperty("_operandA")!.stringValue = seed.OperandA ?? string.Empty;
        serializedObject.FindProperty("_operandB")!.stringValue = seed.OperandB ?? string.Empty;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyStoryEffect(
        StoryEffectDefinition asset,
        string eventId,
        StoryEffectSeed seed,
        int index)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = $"{eventId}.effect.{index:D2}";
        serializedObject.FindProperty("_kind")!.enumValueIndex = (int)seed.Kind;
        serializedObject.FindProperty("_payload")!.stringValue = seed.Payload ?? string.Empty;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyChapterBeat(ChapterBeatDefinition asset, ChapterBeatSeed seed)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = seed.Id;
        serializedObject.FindProperty("_chapterId")!.stringValue = seed.ChapterId;
        serializedObject.FindProperty("_siteId")!.stringValue = seed.SiteId;
        serializedObject.FindProperty("_nodeIndex")!.intValue = seed.NodeIndex;
        serializedObject.FindProperty("_beatLabel")!.stringValue = seed.BeatLabel;
        serializedObject.FindProperty("_tensionTarget")!.floatValue = seed.TensionTarget;
        serializedObject.FindProperty("_reliefTarget")!.floatValue = seed.ReliefTarget;
        serializedObject.FindProperty("_humorTarget")!.floatValue = seed.HumorTarget;
        serializedObject.FindProperty("_catharsisTarget")!.floatValue = seed.CatharsisTarget;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyHeroLore(HeroLoreDefinition asset, HeroLoreSeed seed)
    {
        var serializedObject = new SerializedObject(asset);
        serializedObject.FindProperty("_id")!.stringValue = seed.Id;
        serializedObject.FindProperty("_heroId")!.stringValue = seed.HeroId;
        serializedObject.FindProperty("_tier")!.enumValueIndex = (int)seed.Tier;
        serializedObject.FindProperty("_beatBudget")!.intValue = seed.BeatBudget;
        serializedObject.FindProperty("_canonBio")!.stringValue = string.Join(
            "\n\n",
            new[] { seed.SummaryKo }.Concat(seed.BodyKo).Where(value => !string.IsNullOrWhiteSpace(value)));
        serializedObject.FindProperty("_unresolvedHook")!.stringValue = seed.UnresolvedHook ?? string.Empty;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TChild[] SyncChildAssets<TChild, TSeed>(
        string assetPath,
        string childPrefix,
        IReadOnlyList<TSeed> seeds,
        Action<TChild, TSeed, int> apply)
        where TChild : ScriptableObject
    {
        var existingChildren = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .OfType<TChild>()
            .OrderBy(asset => asset.name, StringComparer.Ordinal)
            .ToList();

        while (existingChildren.Count > seeds.Count)
        {
            var lastIndex = existingChildren.Count - 1;
            UnityEngine.Object.DestroyImmediate(existingChildren[lastIndex], true);
            existingChildren.RemoveAt(lastIndex);
        }

        var result = new TChild[seeds.Count];
        for (var index = 0; index < seeds.Count; index++)
        {
            TChild childAsset;
            if (index < existingChildren.Count)
            {
                childAsset = existingChildren[index];
            }
            else
            {
                childAsset = ScriptableObject.CreateInstance<TChild>();
                AssetDatabase.AddObjectToAsset(childAsset, assetPath);
                existingChildren.Add(childAsset);
            }

            childAsset.name = $"{childPrefix}_{index:D2}";
            apply(childAsset, seeds[index], index);
            EditorUtility.SetDirty(childAsset);
            result[index] = childAsset;
        }

        return result;
    }

    private static StringTable GetOrCreateStringTable(StringTableCollection collection, string localeCode)
    {
        return collection.GetTable(new UnityEngine.Localization.LocaleIdentifier(localeCode)) as StringTable
               ?? collection.AddNewTable(new UnityEngine.Localization.LocaleIdentifier(localeCode)) as StringTable
               ?? throw new InvalidOperationException($"Failed to load or create locale table '{localeCode}' for '{collection.name}'.");
    }

    private static void UpsertNarrativeStringEntry(
        StringTable table,
        string key,
        string incomingValue,
        bool preserveExistingValueWhenIncomingIsEmpty)
    {
        var entry = table.GetEntry(key) ?? table.AddEntry(key, incomingValue ?? string.Empty);
        if (!preserveExistingValueWhenIncomingIsEmpty || !string.IsNullOrWhiteSpace(incomingValue))
        {
            entry.Value = incomingValue ?? string.Empty;
        }
        else if (string.IsNullOrWhiteSpace(entry.Value))
        {
            entry.Value = string.Empty;
        }

        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(table.SharedData);
    }

    private static T CreateAsset<T>(string path, System.Action<T> configure) where T : ScriptableObject
    {
        var existing = TryLoadCanonicalAsset<T>(path);
        if (existing == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || File.Exists(path))
            {
                Debug.LogWarning($"SM sample content detected an invalid or stale asset at {path}. Replacing it with a fresh {typeof(T).Name}.");
                AssetDatabase.DeleteAsset(path);
            }

            existing = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(existing, path);
        }

        configure(existing);
        ClearLegacyTextFields(existing);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void UpsertStringEntry(string tableName, string key, string koValue, string enValue, bool smart = false)
    {
        var collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null)
        {
            collection = LocalizationEditorSettings.CreateStringTableCollection(tableName, LocalizationFoundationBootstrap.StringTableRoot, LocalizationEditorSettings.GetLocales().ToList());
        }

        UpsertStringEntry(collection, "ko", key, koValue, smart);
        UpsertStringEntry(collection, "en", key, enValue, smart);
    }

    private static void UpsertStringEntry(StringTableCollection collection, string localeCode, string key, string value, bool smart)
    {
        var table = collection.GetTable(new UnityEngine.Localization.LocaleIdentifier(localeCode)) as StringTable
                    ?? collection.AddNewTable(new UnityEngine.Localization.LocaleIdentifier(localeCode)) as StringTable;
        if (table == null)
        {
            return;
        }

        var entry = table.GetEntry(key) ?? table.AddEntry(key, value);
        entry.Value = value;
        entry.IsSmart = smart;
        EditorUtility.SetDirty(table);
        EditorUtility.SetDirty(table.SharedData);
    }

    private static void ClearLegacyTextFields(ScriptableObject asset)
    {
        var serializedObject = new SerializedObject(asset);
        var iterator = serializedObject.GetIterator();
        var enterChildren = true;
        while (iterator.Next(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyType != SerializedPropertyType.String)
            {
                continue;
            }

            if (iterator.name is "legacyDisplayName" or "legacyDescription" or "legacyLabel")
            {
                iterator.stringValue = string.Empty;
            }
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string ResolveArchetypeKoName(string id)
    {
        return id switch
        {
            "warden" => "감시자",
            "guardian" => "수호자",
            "bulwark" => "방벽수",
            "slayer" => "학살자",
            "raider" => "약탈자",
            "reaver" => "절단자",
            "hunter" => "사냥꾼",
            "scout" => "정찰병",
            "marksman" => "명사수",
            "priest" => "사제",
            "hexer" => "주술사",
            "shaman" => "주술승",
            "rift_stalker" => "균열 추적자",
            "bastion_penitent" => "참회 방벽수",
            "pale_executor" => "창백한 집행자",
            "mirror_cantor" => "거울 성가대",
            _ => id
        };
    }

    private static string ResolveCharacterKoName(string id)
    {
        return ResolveArchetypeKoName(id);
    }

    private static string ResolveCharacterKoDescription(string id)
    {
        return id switch
        {
            "rift_stalker" => "균열을 따라 약한 후열을 파고드는 정찰형 특화 영웅",
            "bastion_penitent" => "무너진 방벽 교리를 원정대의 전열로 되돌리는 특화 영웅",
            "pale_executor" => "표식이 남긴 틈을 끝까지 추적해 처형하는 특화 영웅",
            "mirror_cantor" => "제어와 정화를 한 박자로 묶어 대열을 보정하는 특화 영웅",
            _ => $"{ResolveCharacterKoName(id)} 기본 캐릭터"
        };
    }

    private static string ResolveCharacterEnName(string id)
    {
        return id switch
        {
            "warden" => "Warden",
            "guardian" => "Guardian",
            "bulwark" => "Bulwark",
            "slayer" => "Slayer",
            "raider" => "Raider",
            "reaver" => "Reaver",
            "hunter" => "Hunter",
            "scout" => "Scout",
            "marksman" => "Marksman",
            "priest" => "Priest",
            "hexer" => "Hexer",
            "shaman" => "Shaman",
            "rift_stalker" => "Rift Stalker",
            "bastion_penitent" => "Bastion Penitent",
            "pale_executor" => "Pale Executor",
            "mirror_cantor" => "Mirror Cantor",
            _ => id
        };
    }

    private static string ResolveCharacterEnDescription(string id)
    {
        return id switch
        {
            "rift_stalker" => "Specialist scout that follows rifts into exposed backlines",
            "bastion_penitent" => "Specialist bulwark that turns broken doctrine into a stable front",
            "pale_executor" => "Specialist finisher that follows marked openings to execution",
            "mirror_cantor" => "Specialist cantor that pairs control cleanup with formation support",
            _ => $"{ResolveCharacterEnName(id)} character identity"
        };
    }
}
