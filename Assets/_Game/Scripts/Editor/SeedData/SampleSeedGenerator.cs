using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Editor.Bootstrap;
using SM.Content.Definitions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace SM.Editor.SeedData;

public static class SampleSeedGenerator
{
    public const string ResourcesRoot = "Assets/Resources/_Game/Content/Definitions";
    public const string LegacyRoot = "Assets/_Game/Content/Definitions";

    [MenuItem("SM/Seed/Generate Sample Content")]
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
        PatchLaunchFloorArchetypes(races, classes, traitPools, skillCatalog, footprintProfiles, behaviorProfiles, mobilityProfiles);
        CreateAugments();
        CreateItems();
        CreateAffixes();
        PatchLaunchFloorItemsAndSkills(stableTags);
        CreateStatusCatalog();
        CreateTraitTokens();
        CreateRewardSourcesAndDropTables();
        CreateCampaignEncounterCatalog();
        var rewardTables = CreateRewardTables();
        CreateExpedition(rewardTables);
        RepairResidualAuthoring(stableTags);

        AssetDatabase.SaveAssets();
        ReimportCanonicalAssets();
        ForceReserializeCanonicalAssets();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"SM sample content generated under Resources. Root={ResourcesRoot}, Stats={stats.Count}, Races={races.Count}, Classes={classes.Count}, Skills={skills.Count}, Archetypes={archetypes.Count}");
    }

    [MenuItem("SM/Seed/Migrate Legacy Sample Content")]
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

    [MenuItem("SM/Seed/Migrate Legacy Sample Content", true)]
    public static bool ValidateMigrateLegacySampleContent()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    public static void EnsureCanonicalSampleContent()
    {
        EnsureFolders();
        LocalizationFoundationBootstrap.EnsureFoundationAssets();

        if (HasCanonicalMinimumContent())
        {
            if (HasCanonicalAuthoringDrift())
            {
                Debug.Log($"SM canonical sample content drift detected. Regenerating under {ResourcesRoot}.");
                Generate();
                return;
            }

            Debug.Log($"SM canonical sample content already present. Root={ResourcesRoot}");
            return;
        }

        Debug.Log($"SM canonical sample content missing or incomplete. Regenerating under {ResourcesRoot}.");
        Generate();
    }

    public static bool HasCanonicalMinimumContent()
    {
        return HasCanonicalAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_max_health.asset", definition => string.Equals(definition.Id, "max_health", StringComparison.Ordinal))
               && HasCanonicalAsset<RaceDefinition>($"{ResourcesRoot}/Races/race_human.asset", definition => string.Equals(definition.Id, "human", StringComparison.Ordinal))
               && HasCanonicalAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_vanguard.asset", definition => string.Equals(definition.Id, "vanguard", StringComparison.Ordinal))
               && HasCanonicalAsset<FootprintProfileDefinition>($"{ResourcesRoot}/FootprintProfiles/footprint_vanguard.asset", definition => definition.EngagementSlotCount > 0)
               && HasCanonicalAsset<BehaviorProfileDefinition>($"{ResourcesRoot}/BehaviorProfiles/behavior_vanguard.asset", definition => definition.ReevaluationInterval > 0f)
               && HasCanonicalAsset<MobilityProfileDefinition>($"{ResourcesRoot}/MobilityProfiles/mobility_ranger.asset", definition => definition.Distance > 0f)
               && HasCanonicalAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_warden.asset", definition => string.Equals(definition.Id, "warden", StringComparison.Ordinal))
               && HasCanonicalAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_warden.asset", definition => definition.Skills != null && definition.Skills.Count == 4)
               && HasCanonicalAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_warden_utility.asset", definition => string.Equals(definition.Id, "skill_warden_utility", StringComparison.Ordinal))
               && HasCanonicalAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_bulwark_utility.asset", definition => definition.RequiredWeaponTags != null && definition.RequiredWeaponTags.Count > 0 && definition.RequiredWeaponTags.All(tag => tag != null))
               && HasCanonicalAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/support_brutal.asset", definition => string.Equals(definition.Id, "support_brutal", StringComparison.Ordinal))
               && HasCanonicalAsset<AugmentDefinition>($"{ResourcesRoot}/Augments/augment_silver_guard.asset", definition => string.Equals(definition.Id, "augment_silver_guard", StringComparison.Ordinal))
               && HasCanonicalAsset<AugmentDefinition>($"{ResourcesRoot}/Augments/augment_silver_guard.asset", definition => !string.IsNullOrWhiteSpace(definition.FamilyId))
               && HasCanonicalAsset<ItemBaseDefinition>($"{ResourcesRoot}/Items/item_warden_armor.asset", definition => string.Equals(definition.Id, "item_warden_armor", StringComparison.Ordinal))
               && HasCanonicalAsset<AffixDefinition>($"{ResourcesRoot}/Affixes/affix_guarded.asset", definition => string.Equals(definition.Id, "affix_guarded", StringComparison.Ordinal))
               && HasCanonicalAsset<AffixDefinition>($"{ResourcesRoot}/Affixes/affix_precise.asset", definition => definition.CompileTags != null && definition.CompileTags.All(tag => tag != null))
               && HasCanonicalAsset<CampaignChapterDefinition>($"{ResourcesRoot}/CampaignChapters/chapter_ashen_frontier.asset", definition => string.Equals(definition.Id, "chapter_ashen_frontier", StringComparison.Ordinal))
               && HasCanonicalAsset<ExpeditionSiteDefinition>($"{ResourcesRoot}/ExpeditionSites/site_ashen_gate.asset", definition => string.Equals(definition.Id, "site_ashen_gate", StringComparison.Ordinal))
               && HasCanonicalAsset<EncounterDefinition>($"{ResourcesRoot}/Encounters/site_ashen_gate_skirmish_1.asset", definition => string.Equals(definition.Id, "site_ashen_gate_skirmish_1", StringComparison.Ordinal))
               && HasCanonicalAsset<EnemySquadTemplateDefinition>($"{ResourcesRoot}/EnemySquads/site_ashen_gate_skirmish_1_squad.asset", definition => string.Equals(definition.Id, "site_ashen_gate_skirmish_1_squad", StringComparison.Ordinal))
               && HasCanonicalAsset<BossOverlayDefinition>($"{ResourcesRoot}/BossOverlays/boss_overlay_ashen_gate.asset", definition => string.Equals(definition.Id, "boss_overlay_ashen_gate", StringComparison.Ordinal))
               && HasCanonicalAsset<StatusFamilyDefinition>($"{ResourcesRoot}/StatusFamilies/status_family_guarded.asset", definition => string.Equals(definition.Id, "guarded", StringComparison.Ordinal))
               && HasCanonicalAsset<CleanseProfileDefinition>($"{ResourcesRoot}/CleanseProfiles/cleanse_profile_cleanse_basic.asset", definition => string.Equals(definition.Id, "cleanse_basic", StringComparison.Ordinal))
               && HasCanonicalAsset<ControlDiminishingRuleDefinition>($"{ResourcesRoot}/ControlDiminishingRules/control_diminishing_launch_floor.asset", definition => string.Equals(definition.Id, "control_diminishing_launch_floor", StringComparison.Ordinal))
               && HasCanonicalAsset<RewardSourceDefinition>($"{ResourcesRoot}/RewardSources/reward_source_skirmish.asset", definition => string.Equals(definition.Id, "reward_source_skirmish", StringComparison.Ordinal))
               && HasCanonicalAsset<DropTableDefinition>($"{ResourcesRoot}/DropTables/drop_table_skirmish.asset", definition => string.Equals(definition.Id, "drop_table_skirmish", StringComparison.Ordinal))
               && HasCanonicalAsset<LootBundleDefinition>($"{ResourcesRoot}/LootBundles/loot_bundle_extract_bonus.asset", definition => string.Equals(definition.Id, "loot_bundle_extract_bonus", StringComparison.Ordinal))
               && HasCanonicalAsset<RewardTableDefinition>($"{ResourcesRoot}/Rewards/rewardtable_battle.asset", definition => !string.IsNullOrWhiteSpace(definition.DescriptionKey))
               && HasCanonicalAsset<PassiveNodeDefinition>($"{ResourcesRoot}/PassiveNodes/passive_duelist_small_01.asset", definition => definition.CompileTags != null && definition.CompileTags.All(tag => tag != null))
               && HasCanonicalAsset<TraitTokenDefinition>($"{ResourcesRoot}/TraitTokens/trait_reroll_token.asset", definition => string.Equals(definition.Id, "trait_reroll_token", StringComparison.Ordinal));
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
            $"{ResourcesRoot}/FootprintProfiles",
            $"{ResourcesRoot}/BehaviorProfiles",
            $"{ResourcesRoot}/MobilityProfiles",
            $"{ResourcesRoot}/Traits",
            $"{ResourcesRoot}/Skills",
            $"{ResourcesRoot}/Archetypes",
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
        return File.Exists(path) && File.ReadAllText(path).Contains(fragment);
    }

    private static bool HasCanonicalAsset<T>(string path, System.Func<T, string> selector, string expectedId) where T : UnityEngine.Object
    {
        var resourcePath = ToResourcesLoadPath(path);
        var asset = string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<T>(resourcePath);
        asset ??= AssetDatabase.LoadAssetAtPath<T>(path);
        asset ??= AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
        if (asset != null)
        {
            return string.Equals(selector(asset), expectedId, System.StringComparison.Ordinal);
        }

        return HasAssetText(path, $"Id: {expectedId}");
    }

    private static bool HasCanonicalAsset<T>(string path, System.Func<T, bool> predicate) where T : UnityEngine.Object
    {
        var resourcePath = ToResourcesLoadPath(path);
        var asset = string.IsNullOrWhiteSpace(resourcePath) ? null : Resources.Load<T>(resourcePath);
        asset ??= AssetDatabase.LoadAssetAtPath<T>(path);
        asset ??= AssetDatabase.LoadAssetAtPath(path, typeof(T)) as T;
        return asset != null && predicate(asset);
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

    private static void ForceReserializeCanonicalAssets()
    {
        var assetPaths = Directory
            .EnumerateFiles(ResourcesRoot, "*.asset", SearchOption.AllDirectories)
            .Select(ToUnityPath)
            .ToList();
        if (assetPaths.Count == 0)
        {
            return;
        }

        AssetDatabase.ForceReserializeAssets(assetPaths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
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
        foreach (var archetypeId in new[] { "warden", "guardian", "bulwark", "slayer", "raider", "reaver", "hunter", "scout", "marksman", "priest", "hexer", "shaman" })
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
        return result;
    }

    private static UnitArchetypeDefinition CreateArchetype(string id, string name, RaceDefinition race, ClassDefinition @class, TraitPoolDefinition pool, SkillDefinitionAsset skill, FootprintProfileDefinition footprintProfile, BehaviorProfileDefinition behaviorProfile, MobilityProfileDefinition mobilityProfile, float hp, float atk, float def, float spd, float heal, DeploymentAnchorValue defaultAnchor, TeamPostureTypeValue preferredPosture)
    {
        return CreateAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{id}.asset", a =>
        {
            a.Id = id;
            a.NameKey = ContentLocalizationTables.BuildArchetypeNameKey(id);
            a.ScopeKind = ArchetypeScopeValue.Core;
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

    private static void PatchLaunchFloorArchetypes(
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills,
        IReadOnlyDictionary<string, FootprintProfileDefinition> footprintProfiles,
        IReadOnlyDictionary<string, BehaviorProfileDefinition> behaviorProfiles,
        IReadOnlyDictionary<string, MobilityProfileDefinition> mobilityProfiles)
    {
        var definitions = new[]
        {
            new { Id = "warden", RaceId = "human", ClassId = "vanguard", TraitPoolId = "warden", DefaultSkillIds = new[] { "skill_guardian_core", "skill_warden_utility", "skill_vanguard_passive_1", "skill_vanguard_support_1" } },
            new { Id = "guardian", RaceId = "undead", ClassId = "vanguard", TraitPoolId = "guardian", DefaultSkillIds = new[] { "skill_guardian_core", "skill_guardian_utility", "skill_vanguard_passive_2", "skill_vanguard_support_2" } },
            new { Id = "bulwark", RaceId = "beastkin", ClassId = "vanguard", TraitPoolId = "bulwark", DefaultSkillIds = new[] { "skill_bulwark_core", "skill_guardian_core", "skill_warden_utility", "support_guarded" } },
            new { Id = "slayer", RaceId = "human", ClassId = "duelist", TraitPoolId = "slayer", DefaultSkillIds = new[] { "skill_slayer_core", "skill_slayer_utility", "skill_duelist_passive_1", "skill_duelist_support_1" } },
            new { Id = "raider", RaceId = "beastkin", ClassId = "duelist", TraitPoolId = "raider", DefaultSkillIds = new[] { "skill_raider_core", "skill_raider_utility", "skill_duelist_passive_2", "skill_duelist_support_2" } },
            new { Id = "reaver", RaceId = "undead", ClassId = "duelist", TraitPoolId = "reaver", DefaultSkillIds = new[] { "skill_reaver_core", "skill_raider_core", "skill_reaver_utility", "support_executioner" } },
            new { Id = "hunter", RaceId = "human", ClassId = "ranger", TraitPoolId = "hunter", DefaultSkillIds = new[] { "skill_precision_shot", "skill_hunter_utility", "skill_ranger_passive_1", "skill_ranger_support_1" } },
            new { Id = "scout", RaceId = "beastkin", ClassId = "ranger", TraitPoolId = "scout", DefaultSkillIds = new[] { "skill_scout_core", "skill_scout_utility", "skill_ranger_passive_2", "skill_ranger_support_2" } },
            new { Id = "marksman", RaceId = "undead", ClassId = "ranger", TraitPoolId = "marksman", DefaultSkillIds = new[] { "skill_marksman_core", "skill_hunter_utility", "skill_marksman_utility", "support_longshot" } },
            new { Id = "priest", RaceId = "human", ClassId = "mystic", TraitPoolId = "priest", DefaultSkillIds = new[] { "skill_priest_core", "skill_minor_heal", "skill_mystic_passive_1", "skill_mystic_support_1" } },
            new { Id = "hexer", RaceId = "undead", ClassId = "mystic", TraitPoolId = "hexer", DefaultSkillIds = new[] { "skill_hexer_core", "skill_hexer_utility", "skill_mystic_passive_2", "skill_mystic_support_2" } },
            new { Id = "shaman", RaceId = "beastkin", ClassId = "mystic", TraitPoolId = "shaman", DefaultSkillIds = new[] { "skill_shaman_core", "skill_priest_core", "skill_shaman_utility", "support_siphon" } },
        };

        foreach (var definition in definitions)
        {
            var path = $"{ResourcesRoot}/Archetypes/archetype_{definition.Id}.asset";
            var asset = LoadDefinition<UnitArchetypeDefinition>(path);
            if (asset == null)
            {
                continue;
            }

            asset.ScopeKind = ArchetypeScopeValue.Core;
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

    private static void CreateAugments()
    {
        var definitions = new (string id, string enName, string koName, string enDesc, string koDesc, AugmentRarity rarity, bool permanent, string stat, float value)[]
        {
            ("augment_silver_guard", "Guard Instinct", "수비 본능", "Improves defensive stability.", "방어 안정성을 높입니다.", AugmentRarity.Silver, false, "armor", 1f),
            ("augment_silver_focus", "Focus", "집중", "Sharpens offensive focus.", "공격 집중도를 높입니다.", AugmentRarity.Silver, false, "phys_power", 1f),
            ("augment_silver_stride", "Stride", "질주", "Improves battle tempo.", "전투 템포를 높입니다.", AugmentRarity.Silver, false, "attack_speed", 1f),
            ("augment_gold_bastion", "Bastion", "보루", "Boosts frontline endurance.", "전열 생존력을 강화합니다.", AugmentRarity.Gold, false, "max_health", 4f),
            ("augment_gold_fury", "Fury", "격노", "Boosts offensive output.", "공격 출력을 강화합니다.", AugmentRarity.Gold, false, "phys_power", 2f),
            ("augment_gold_mending", "Mending", "치유술", "Boosts healing throughput.", "회복 효율을 강화합니다.", AugmentRarity.Gold, false, "heal_power", 2f),
            ("augment_platinum_overrun", "Overrun", "돌파", "Heavily boosts attack.", "공격력을 크게 강화합니다.", AugmentRarity.Platinum, false, "phys_power", 3f),
            ("augment_platinum_wall", "Wall", "철벽", "Heavily boosts defense.", "방어력을 크게 강화합니다.", AugmentRarity.Platinum, false, "armor", 3f),
            ("augment_platinum_surge", "Surge", "급류", "Heavily boosts speed.", "속도를 크게 강화합니다.", AugmentRarity.Platinum, false, "attack_speed", 2f),
            ("augment_perm_legacy_blade", "Legacy Blade", "유산의 검", "Permanent attack bonus.", "영구 공격 보너스.", AugmentRarity.Permanent, true, "phys_power", 1f),
            ("augment_perm_legacy_hide", "Legacy Hide", "유산의 가죽", "Permanent defense bonus.", "영구 방어 보너스.", AugmentRarity.Permanent, true, "armor", 1f),
            ("augment_perm_legacy_grace", "Legacy Grace", "유산의 은총", "Permanent speed bonus.", "영구 속도 보너스.", AugmentRarity.Permanent, true, "attack_speed", 1f),
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
                    "phys_power" => AffixCategoryValue.OffenseFlat,
                    "armor" => AffixCategoryValue.DefenseFlat,
                    "max_health" => AffixCategoryValue.DefenseScaling,
                    _ => AffixCategoryValue.Utility
                };
                a.AllowedSlotTypes = d.stat switch
                {
                    "phys_power" => new List<ItemSlotType> { ItemSlotType.Weapon },
                    "armor" => new List<ItemSlotType> { ItemSlotType.Armor, ItemSlotType.Accessory },
                    "max_health" => new List<ItemSlotType> { ItemSlotType.Armor, ItemSlotType.Accessory },
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
                MakeRewardEntry("reward.reroll.1", RewardType.TraitRerollCurrency, 1, "1 Trait Reroll", "특성 리롤 1"),
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
                MakeRewardEntry("reward.reroll.2", RewardType.TraitRerollCurrency, 2, "2 Trait Reroll", "특성 리롤 2"),
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
    }

    private static void RepairResidualAuthoring(IReadOnlyDictionary<string, StableTagDefinition> tags)
    {
        RepairReferenceDefinitionAuthoring();
        RepairItemTagReferences(tags);
        RepairAffixTagReferences();
        RepairPassiveNodeTagReferences();
        RepairSkillTagReferences(tags);
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
        if (archetypes.Any(asset => asset.Skills == null || asset.Skills.Count != 4 || asset.Skills.Any(skill => skill == null)))
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

        var items = LoadDefinitions<ItemBaseDefinition>($"{ResourcesRoot}/Items");
        if (items.Any(item =>
                HasBrokenTagRefs(item.CompileTags)
                || HasBrokenTagRefs(item.RuleModifierTags)
                || HasBrokenTagRefs(item.AllowedClassTags)
                || HasBrokenTagRefs(item.AllowedArchetypeTags)
                || HasBrokenTagRefs(item.UniqueRuleTags)
                || (item.SlotType == ItemSlotType.Weapon && string.IsNullOrWhiteSpace(item.WeaponFamilyTag))
                || string.IsNullOrWhiteSpace(item.CraftCurrencyTag)))
        {
            return true;
        }

        var affixes = LoadDefinitions<AffixDefinition>($"{ResourcesRoot}/Affixes");
        if (affixes.Any(affix => HasBrokenTagRefs(affix.CompileTags) || HasBrokenTagRefs(affix.RuleModifierTags)))
        {
            return true;
        }

        var passiveNodes = LoadDefinitions<PassiveNodeDefinition>($"{ResourcesRoot}/PassiveNodes");
        if (passiveNodes.Any(node =>
                HasBrokenTagRefs(node.CompileTags)
                || HasBrokenTagRefs(node.RuleModifierTags)
                || HasBrokenTagRefs(node.MutualExclusionTags)))
        {
            return true;
        }

        var skills = LoadDefinitions<SkillDefinitionAsset>($"{ResourcesRoot}/Skills");
        return skills.Any(skill =>
            HasBrokenTagRefs(skill.CompileTags)
            || HasBrokenTagRefs(skill.RuleModifierTags)
            || HasBrokenTagRefs(skill.SupportAllowedTags)
            || HasBrokenTagRefs(skill.SupportBlockedTags)
            || HasBrokenTagRefs(skill.RequiredWeaponTags)
            || HasBrokenTagRefs(skill.RequiredClassTags));
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
            (Id: "trait_reroll_token", Reward: RewardType.TraitRerollCurrency, En: "Trait Reroll Token", Ko: "특성 리롤 토큰"),
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

        CreateDropTable("drop_table_skirmish", "reward_source_skirmish", new[]
        {
            MakeLootEntry("gold_skirmish", RewardType.Gold, 8, RarityBracketValue.Common, true, 1),
            MakeLootEntry("ember_dust_skirmish", RewardType.EmberDust, 2, RarityBracketValue.Common, true, 1),
            MakeLootEntry("item_iron_sword", RewardType.Item, 1, RarityBracketValue.Advanced, false, 3),
            MakeLootEntry("skill_power_strike", RewardType.SkillShard, 1, RarityBracketValue.Advanced, false, 2),
        });

        CreateDropTable("drop_table_elite", "reward_source_elite", new[]
        {
            MakeLootEntry("gold_elite", RewardType.Gold, 12, RarityBracketValue.Advanced, true, 1),
            MakeLootEntry("ember_dust_elite", RewardType.EmberDust, 4, RarityBracketValue.Advanced, true, 1),
            MakeLootEntry("item_bone_blade", RewardType.Item, 1, RarityBracketValue.Elite, false, 3),
            MakeLootEntry("skill_precision_shot", RewardType.SkillManual, 1, RarityBracketValue.Elite, false, 2),
        });

        CreateDropTable("drop_table_boss", "reward_source_boss", new[]
        {
            MakeLootEntry("boss_sigil_drop", RewardType.BossSigil, 1, RarityBracketValue.Boss, true, 1),
            MakeLootEntry("echo_crystal_boss", RewardType.EchoCrystal, 2, RarityBracketValue.Elite, true, 1),
            MakeLootEntry("item_prayer_bead", RewardType.Item, 1, RarityBracketValue.Boss, false, 2),
            MakeLootEntry("skill_minor_heal", RewardType.SkillManual, 1, RarityBracketValue.Boss, false, 2),
        });

        CreateDropTable("drop_table_extract", "reward_source_extract", new[]
        {
            MakeLootEntry("gold_extract", RewardType.Gold, 16, RarityBracketValue.Common, true, 1),
            MakeLootEntry("ember_dust_extract", RewardType.EmberDust, 5, RarityBracketValue.Advanced, true, 1),
            MakeLootEntry("trait_reroll_token", RewardType.TraitRerollCurrency, 1, RarityBracketValue.Advanced, false, 2),
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

    private static void CreateCampaignEncounterCatalog()
    {
        var sites = new[]
        {
            new { ChapterId = "chapter_ashen_frontier", ChapterOrder = 1, ChapterName = "Ashen Frontier", ChapterNameKo = "잿빛 변경", ChapterDesc = "Open the frontier routes.", ChapterDescKo = "변경의 첫 루트를 연다.", SiteId = "site_ashen_gate", SiteOrder = 1, SiteName = "Ashen Gate", SiteNameKo = "잿문", SiteDesc = "Vanguard outpost route.", SiteDescKo = "전열 진영과 충돌하는 루트.", Faction = "faction_ashen_vanguard", SkirmishA = new[] { "warden", "hunter", "hexer", "raider" }, SkirmishB = new[] { "guardian", "scout", "hexer", "hunter" }, Elite = new[] { "bulwark", "hunter", "hexer", "guardian" }, BossCaptain = "bulwark", BossEscorts = new[] { "hunter", "hexer" }, OverlayId = "boss_overlay_ashen_gate" },
            new { ChapterId = "chapter_ashen_frontier", ChapterOrder = 1, ChapterName = "Ashen Frontier", ChapterNameKo = "잿빛 변경", ChapterDesc = "Open the frontier routes.", ChapterDescKo = "변경의 첫 루트를 연다.", SiteId = "site_cinder_watch", SiteOrder = 2, SiteName = "Cinder Watch", SiteNameKo = "잿불 망대", SiteDesc = "Ranged harassment route.", SiteDescKo = "원거리 압박이 많은 루트.", Faction = "faction_cinder_watch", SkirmishA = new[] { "hunter", "scout", "raider", "priest" }, SkirmishB = new[] { "marksman", "scout", "guardian", "hexer" }, Elite = new[] { "marksman", "bulwark", "scout", "hexer" }, BossCaptain = "marksman", BossEscorts = new[] { "scout", "priest" }, OverlayId = "boss_overlay_cinder_watch" },
            new { ChapterId = "chapter_warren_depths", ChapterOrder = 2, ChapterName = "Warren Depths", ChapterNameKo = "망실 굴지", ChapterDesc = "Break the hidden warrens.", ChapterDescKo = "숨은 굴을 돌파한다.", SiteId = "site_forgotten_warren", SiteOrder = 1, SiteName = "Forgotten Warren", SiteNameKo = "망실 굴", SiteDesc = "Attrition-heavy tunnels.", SiteDescKo = "소모전이 긴 터널.", Faction = "faction_warren_pack", SkirmishA = new[] { "raider", "scout", "shaman", "hunter" }, SkirmishB = new[] { "reaver", "scout", "shaman", "hunter" }, Elite = new[] { "reaver", "raider", "scout", "shaman" }, BossCaptain = "reaver", BossEscorts = new[] { "scout", "shaman" }, OverlayId = "boss_overlay_forgotten_warren" },
            new { ChapterId = "chapter_warren_depths", ChapterOrder = 2, ChapterName = "Warren Depths", ChapterNameKo = "망실 굴지", ChapterDesc = "Break the hidden warrens.", ChapterDescKo = "숨은 굴을 돌파한다.", SiteId = "site_twisted_den", SiteOrder = 2, SiteName = "Twisted Den", SiteNameKo = "뒤틀린 소굴", SiteDesc = "Ambush-heavy den.", SiteDescKo = "기습이 많은 소굴.", Faction = "faction_twisted_den", SkirmishA = new[] { "slayer", "raider", "scout", "shaman" }, SkirmishB = new[] { "slayer", "hunter", "scout", "priest" }, Elite = new[] { "slayer", "reaver", "scout", "priest" }, BossCaptain = "slayer", BossEscorts = new[] { "scout", "priest" }, OverlayId = "boss_overlay_twisted_den" },
            new { ChapterId = "chapter_ruined_crypts", ChapterOrder = 3, ChapterName = "Ruined Crypts", ChapterNameKo = "폐허 묘실", ChapterDesc = "Seal the crypt lords.", ChapterDescKo = "묘실의 군주를 봉인한다.", SiteId = "site_ruined_crypt", SiteOrder = 1, SiteName = "Ruined Crypt", SiteNameKo = "폐허 묘실", SiteDesc = "Undead elite route.", SiteDescKo = "언데드 정예 루트.", Faction = "faction_bone_host", SkirmishA = new[] { "guardian", "hexer", "priest", "hunter" }, SkirmishB = new[] { "bulwark", "hexer", "priest", "marksman" }, Elite = new[] { "bulwark", "hexer", "priest", "marksman" }, BossCaptain = "hexer", BossEscorts = new[] { "priest", "guardian" }, OverlayId = "boss_overlay_ruined_crypt" },
            new { ChapterId = "chapter_ruined_crypts", ChapterOrder = 3, ChapterName = "Ruined Crypts", ChapterNameKo = "폐허 묘실", ChapterDesc = "Seal the crypt lords.", ChapterDescKo = "묘실의 군주를 봉인한다.", SiteId = "site_grave_sanctum", SiteOrder = 2, SiteName = "Grave Sanctum", SiteNameKo = "무덤 성소", SiteDesc = "Final ritual route.", SiteDescKo = "최종 의식 루트.", Faction = "faction_grave_sanctum", SkirmishA = new[] { "guardian", "hexer", "shaman", "marksman" }, SkirmishB = new[] { "bulwark", "hexer", "shaman", "hunter" }, Elite = new[] { "bulwark", "guardian", "hexer", "shaman" }, BossCaptain = "shaman", BossEscorts = new[] { "guardian", "hexer" }, OverlayId = "boss_overlay_grave_sanctum" },
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

            CreateEncounter($"{site.SiteId}_skirmish_1", site.SiteId, site.Faction, EncounterKindValue.Skirmish, $"{site.SiteId}_skirmish_1_squad", string.Empty, "reward_source_skirmish", ThreatTierValue.Tier1, 1, 1, "chapter_entry");
            CreateEncounter($"{site.SiteId}_skirmish_2", site.SiteId, site.Faction, EncounterKindValue.Skirmish, $"{site.SiteId}_skirmish_2_squad", string.Empty, "reward_source_skirmish", ThreatTierValue.Tier1, 1, 1, "chapter_entry");
            CreateEncounter($"{site.SiteId}_elite_1", site.SiteId, site.Faction, EncounterKindValue.Elite, $"{site.SiteId}_elite_1_squad", string.Empty, "reward_source_elite", ThreatTierValue.Tier2, 2, 2, "site_mid");
            CreateEncounter($"{site.SiteId}_boss_1", site.SiteId, site.Faction, EncounterKindValue.Boss, $"{site.SiteId}_boss_1_squad", site.OverlayId, "reward_source_boss", ThreatTierValue.Tier3, 3, 3, "site_boss");
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

    private static LootBundleEntryDefinition MakeLootEntry(string id, RewardType type, int amount, RarityBracketValue rarity, bool guaranteed, int weight)
    {
        return new LootBundleEntryDefinition
        {
            Id = id,
            RewardType = type,
            Amount = amount,
            RarityBracket = rarity,
            IsGuaranteed = guaranteed,
            Weight = weight
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

    private static void CreateEncounter(string id, string siteId, string factionId, EncounterKindValue kind, string squadId, string overlayId, string rewardSourceId, ThreatTierValue threatTier, int threatCost, int threatSkulls, string difficultyBand)
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
            asset.RewardDropTags = new List<string> { rewardSourceId, factionId, kind.ToString().ToLowerInvariant() };
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
        }
    }

    private static T CreateAsset<T>(string path, System.Action<T> configure) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
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
            "slayer" => "학살자",
            "raider" => "약탈자",
            "hunter" => "사냥꾼",
            "scout" => "정찰병",
            "priest" => "사제",
            "hexer" => "주술사",
            _ => id
        };
    }
}
