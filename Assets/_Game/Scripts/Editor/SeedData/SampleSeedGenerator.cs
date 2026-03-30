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
        var skills = CreateSkills();
        var traitPools = CreateTraitPools();
        var archetypes = CreateArchetypes(races, classes, traitPools, skills);
        CreateAugments();
        CreateItems();
        CreateAffixes();
        var rewardTables = CreateRewardTables();
        CreateExpedition(rewardTables);

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

        if (HasCanonicalMinimumContent())
        {
            Debug.Log($"SM canonical sample content already present. Root={ResourcesRoot}");
            return;
        }

        Debug.Log($"SM canonical sample content missing or incomplete. Regenerating under {ResourcesRoot}.");
        Generate();
    }

    public static bool HasCanonicalMinimumContent()
    {
        return HasCanonicalAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_max_health.asset", definition => definition.Id, "max_health")
               && HasCanonicalAsset<RaceDefinition>($"{ResourcesRoot}/Races/race_human.asset", definition => definition.Id, "human")
               && HasCanonicalAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_vanguard.asset", definition => definition.Id, "vanguard")
               && HasCanonicalAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_warden.asset", definition => definition.Id, "warden");
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
            $"{ResourcesRoot}/Traits",
            $"{ResourcesRoot}/Skills",
            $"{ResourcesRoot}/Archetypes",
            $"{ResourcesRoot}/Augments",
            $"{ResourcesRoot}/Items",
            $"{ResourcesRoot}/Affixes",
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
        if (asset != null)
        {
            return string.Equals(selector(asset), expectedId, System.StringComparison.Ordinal);
        }

        return HasAssetText(path, $"Id: {expectedId}");
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
            if (AssetDatabase.LoadAssetAtPath<RaceDefinition>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            return CreateAsset<RaceDefinition>(path, a =>
            {
                a.Id = tuple.Item1;
                a.NameKey = ContentLocalizationTables.BuildRaceNameKey(tuple.Item1);
                a.DescriptionKey = ContentLocalizationTables.BuildRaceDescriptionKey(tuple.Item1);
                UpsertStringEntry(ContentLocalizationTables.Races, a.NameKey, tuple.Item3, tuple.Item2);
                UpsertStringEntry(ContentLocalizationTables.Races, a.DescriptionKey, tuple.Item5, tuple.Item4);
            });
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
            if (AssetDatabase.LoadAssetAtPath<ClassDefinition>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            return CreateAsset<ClassDefinition>(path, a =>
            {
                a.Id = tuple.Item1;
                a.NameKey = ContentLocalizationTables.BuildClassNameKey(tuple.Item1);
                a.DescriptionKey = ContentLocalizationTables.BuildClassDescriptionKey(tuple.Item1);
                UpsertStringEntry(ContentLocalizationTables.Classes, a.NameKey, tuple.Item3, tuple.Item2);
                UpsertStringEntry(ContentLocalizationTables.Classes, a.DescriptionKey, tuple.Item5, tuple.Item4);
            });
        });
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
        foreach (var archetypeId in new[] { "warden", "guardian", "slayer", "raider", "hunter", "scout", "priest", "hexer" })
        {
            var path = $"{ResourcesRoot}/Traits/traitpool_{archetypeId}.asset";
            if (AssetDatabase.LoadAssetAtPath<TraitPoolDefinition>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            result[archetypeId] = CreateAsset<TraitPoolDefinition>(path, a =>
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
        }
        return result;
    }

    private static Dictionary<string, UnitArchetypeDefinition> CreateArchetypes(
        Dictionary<string, RaceDefinition> races,
        Dictionary<string, ClassDefinition> classes,
        Dictionary<string, TraitPoolDefinition> traitPools,
        Dictionary<string, SkillDefinitionAsset> skills)
    {
        var result = new Dictionary<string, UnitArchetypeDefinition>();
        result["warden"] = CreateArchetype("warden", "Warden", races["human"], classes["vanguard"], traitPools["warden"], skills["skill_power_strike"], 24, 5, 3, 3, 0, DeploymentAnchorValue.FrontCenter, TeamPostureTypeValue.HoldLine);
        result["guardian"] = CreateArchetype("guardian", "Guardian", races["undead"], classes["vanguard"], traitPools["guardian"], skills["skill_power_strike"], 26, 4, 4, 2, 0, DeploymentAnchorValue.FrontTop, TeamPostureTypeValue.ProtectCarry);
        result["slayer"] = CreateArchetype("slayer", "Slayer", races["human"], classes["duelist"], traitPools["slayer"], skills["skill_power_strike"], 20, 7, 2, 4, 0, DeploymentAnchorValue.FrontBottom, TeamPostureTypeValue.StandardAdvance);
        result["raider"] = CreateArchetype("raider", "Raider", races["beastkin"], classes["duelist"], traitPools["raider"], skills["skill_power_strike"], 19, 8, 1, 5, 0, DeploymentAnchorValue.FrontTop, TeamPostureTypeValue.CollapseWeakSide);
        result["hunter"] = CreateArchetype("hunter", "Hunter", races["human"], classes["ranger"], traitPools["hunter"], skills["skill_precision_shot"], 18, 6, 2, 5, 0, DeploymentAnchorValue.BackTop, TeamPostureTypeValue.StandardAdvance);
        result["scout"] = CreateArchetype("scout", "Scout", races["beastkin"], classes["ranger"], traitPools["scout"], skills["skill_precision_shot"], 17, 5, 2, 6, 0, DeploymentAnchorValue.BackBottom, TeamPostureTypeValue.CollapseWeakSide);
        result["priest"] = CreateArchetype("priest", "Priest", races["human"], classes["mystic"], traitPools["priest"], skills["skill_minor_heal"], 18, 3, 2, 4, 5, DeploymentAnchorValue.BackCenter, TeamPostureTypeValue.ProtectCarry);
        result["hexer"] = CreateArchetype("hexer", "Hexer", races["undead"], classes["mystic"], traitPools["hexer"], skills["skill_minor_heal"], 17, 4, 2, 4, 4, DeploymentAnchorValue.BackCenter, TeamPostureTypeValue.AllInBackline);
        return result;
    }

    private static UnitArchetypeDefinition CreateArchetype(string id, string name, RaceDefinition race, ClassDefinition @class, TraitPoolDefinition pool, SkillDefinitionAsset skill, float hp, float atk, float def, float spd, float heal, DeploymentAnchorValue defaultAnchor, TeamPostureTypeValue preferredPosture)
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
            a.Rewards = new List<RewardEntry>
            {
                MakeRewardEntry("reward.gold.10", RewardType.Gold, 10, "10 Gold", "골드 10"),
                MakeRewardEntry("augment_silver_guard", RewardType.TemporaryAugment, 1, "Guard Instinct", "수비 본능"),
                MakeRewardEntry("reward.reroll.1", RewardType.TraitRerollCurrency, 1, "1 Trait Reroll", "특성 리롤 1"),
            };
            UpsertStringEntry(ContentLocalizationTables.Rewards, a.NameKey, "전투 보상", "Battle Rewards");
        });

        var expedition = CreateAsset<RewardTableDefinition>($"{ResourcesRoot}/Rewards/rewardtable_expedition_end.asset", a =>
        {
            a.Id = "rewardtable_expedition_end";
            a.NameKey = ContentLocalizationTables.BuildRewardTableNameKey(a.Id);
            a.Rewards = new List<RewardEntry>
            {
                MakeRewardEntry("reward.gold.30", RewardType.Gold, 30, "30 Gold", "골드 30"),
                MakeRewardEntry("reward.reroll.2", RewardType.TraitRerollCurrency, 2, "2 Trait Reroll", "특성 리롤 2"),
                MakeRewardEntry("augment_perm_legacy_blade", RewardType.TemporaryAugment, 1, "Legacy Blade", "유산의 검"),
            };
            UpsertStringEntry(ContentLocalizationTables.Rewards, a.NameKey, "원정 종료 보상", "Expedition End Rewards");
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
