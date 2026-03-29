using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.SeedData;

public static class SampleSeedGenerator
{
    public const string ResourcesRoot = "Assets/Resources/_Game/Content/Definitions";
    public const string LegacyRoot = "Assets/_Game/Content/Definitions";

    [MenuItem("SM/Seed/Generate Sample Content")]
    public static void Generate()
    {
        EnsureFolders();

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
        AssetDatabase.Refresh();
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

        if (TryMigrateLegacySampleContent())
        {
            return;
        }

        Debug.Log($"SM canonical sample content missing or incomplete. Regenerating under {ResourcesRoot}.");
        Generate();
    }

    public static bool HasCanonicalMinimumContent()
    {
        return HasAssetText($"{ResourcesRoot}/Stats/stat_max_health.asset", "Id: max_health")
               && HasAssetText($"{ResourcesRoot}/Races/race_human.asset", "Id: human")
               && HasAssetText($"{ResourcesRoot}/Classes/class_vanguard.asset", "Id: vanguard")
               && HasAssetText($"{ResourcesRoot}/Archetypes/archetype_warden.asset", "Id: warden");
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

    private static List<StatDefinition> CreateStats()
    {
        return new List<StatDefinition>
        {
            CreateAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_max_health.asset", a => { a.Id = "max_health"; a.DisplayName = "Max Health"; }),
            CreateAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_attack.asset", a => { a.Id = "attack"; a.DisplayName = "Attack"; }),
            CreateAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_defense.asset", a => { a.Id = "defense"; a.DisplayName = "Defense"; }),
            CreateAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_speed.asset", a => { a.Id = "speed"; a.DisplayName = "Speed"; }),
            CreateAsset<StatDefinition>($"{ResourcesRoot}/Stats/stat_heal_power.asset", a => { a.Id = "heal_power"; a.DisplayName = "Heal Power"; }),
        };
    }

    private static Dictionary<string, RaceDefinition> CreateRaces()
    {
        return new Dictionary<string, RaceDefinition>
        {
            ["human"] = CreateAsset<RaceDefinition>($"{ResourcesRoot}/Races/race_human.asset", a => { a.Id = "human"; a.DisplayName = "Human"; a.Description = "Flexible baseline race."; }),
            ["beastkin"] = CreateAsset<RaceDefinition>($"{ResourcesRoot}/Races/race_beastkin.asset", a => { a.Id = "beastkin"; a.DisplayName = "Beastkin"; a.Description = "Aggressive feral pressure."; }),
            ["undead"] = CreateAsset<RaceDefinition>($"{ResourcesRoot}/Races/race_undead.asset", a => { a.Id = "undead"; a.DisplayName = "Undead"; a.Description = "Attrition-oriented deathless fighters."; }),
        };
    }

    private static Dictionary<string, ClassDefinition> CreateClasses()
    {
        return new Dictionary<string, ClassDefinition>
        {
            ["vanguard"] = CreateAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_vanguard.asset", a => { a.Id = "vanguard"; a.DisplayName = "Vanguard"; }),
            ["duelist"] = CreateAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_duelist.asset", a => { a.Id = "duelist"; a.DisplayName = "Duelist"; }),
            ["ranger"] = CreateAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_ranger.asset", a => { a.Id = "ranger"; a.DisplayName = "Ranger"; }),
            ["mystic"] = CreateAsset<ClassDefinition>($"{ResourcesRoot}/Classes/class_mystic.asset", a => { a.Id = "mystic"; a.DisplayName = "Mystic"; }),
        };
    }

    private static Dictionary<string, SkillDefinitionAsset> CreateSkills()
    {
        return new Dictionary<string, SkillDefinitionAsset>
        {
            ["skill_power_strike"] = CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_power_strike.asset", a => { a.Id = "skill_power_strike"; a.DisplayName = "Power Strike"; a.Kind = SkillKindValue.Strike; a.Power = 3f; a.Range = 1; }),
            ["skill_precision_shot"] = CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_precision_shot.asset", a => { a.Id = "skill_precision_shot"; a.DisplayName = "Precision Shot"; a.Kind = SkillKindValue.Strike; a.Power = 2f; a.Range = 2; }),
            ["skill_minor_heal"] = CreateAsset<SkillDefinitionAsset>($"{ResourcesRoot}/Skills/skill_minor_heal.asset", a => { a.Id = "skill_minor_heal"; a.DisplayName = "Minor Heal"; a.Kind = SkillKindValue.Heal; a.Power = 4f; a.Range = 2; }),
        };
    }

    private static Dictionary<string, TraitPoolDefinition> CreateTraitPools()
    {
        var result = new Dictionary<string, TraitPoolDefinition>();
        foreach (var archetypeId in new[] { "warden", "guardian", "slayer", "raider", "hunter", "scout", "priest", "hexer" })
        {
            result[archetypeId] = CreateAsset<TraitPoolDefinition>($"{ResourcesRoot}/Traits/traitpool_{archetypeId}.asset", a =>
            {
                a.Id = $"traitpool_{archetypeId}";
                a.ArchetypeId = archetypeId;
                a.PositiveTraits = new List<TraitEntry>
                {
                    MakeTrait($"{archetypeId}_positive_brave", "Brave", "attack", 2f),
                    MakeTrait($"{archetypeId}_positive_sturdy", "Sturdy", "defense", 1f),
                    MakeTrait($"{archetypeId}_positive_swift", "Swift", "speed", 1f),
                };
                a.NegativeTraits = new List<TraitEntry>
                {
                    MakeTrait($"{archetypeId}_negative_frail", "Frail", "max_health", -3f),
                    MakeTrait($"{archetypeId}_negative_clumsy", "Clumsy", "attack", -1f),
                    MakeTrait($"{archetypeId}_negative_slow", "Slow", "speed", -1f),
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
        result["warden"] = CreateArchetype("warden", "Warden", races["human"], classes["vanguard"], traitPools["warden"], skills["skill_power_strike"], 24, 5, 3, 3, 0);
        result["guardian"] = CreateArchetype("guardian", "Guardian", races["undead"], classes["vanguard"], traitPools["guardian"], skills["skill_power_strike"], 26, 4, 4, 2, 0);
        result["slayer"] = CreateArchetype("slayer", "Slayer", races["human"], classes["duelist"], traitPools["slayer"], skills["skill_power_strike"], 20, 7, 2, 4, 0);
        result["raider"] = CreateArchetype("raider", "Raider", races["beastkin"], classes["duelist"], traitPools["raider"], skills["skill_power_strike"], 19, 8, 1, 5, 0);
        result["hunter"] = CreateArchetype("hunter", "Hunter", races["human"], classes["ranger"], traitPools["hunter"], skills["skill_precision_shot"], 18, 6, 2, 5, 0);
        result["scout"] = CreateArchetype("scout", "Scout", races["beastkin"], classes["ranger"], traitPools["scout"], skills["skill_precision_shot"], 17, 5, 2, 6, 0);
        result["priest"] = CreateArchetype("priest", "Priest", races["human"], classes["mystic"], traitPools["priest"], skills["skill_minor_heal"], 18, 3, 2, 4, 5);
        result["hexer"] = CreateArchetype("hexer", "Hexer", races["undead"], classes["mystic"], traitPools["hexer"], skills["skill_minor_heal"], 17, 4, 2, 4, 4);
        return result;
    }

    private static UnitArchetypeDefinition CreateArchetype(string id, string name, RaceDefinition race, ClassDefinition @class, TraitPoolDefinition pool, SkillDefinitionAsset skill, float hp, float atk, float def, float spd, float heal)
    {
        return CreateAsset<UnitArchetypeDefinition>($"{ResourcesRoot}/Archetypes/archetype_{id}.asset", a =>
        {
            a.Id = id;
            a.DisplayName = name;
            a.Race = race;
            a.Class = @class;
            a.TraitPool = pool;
            a.Skills = new List<SkillDefinitionAsset> { skill };
            a.BaseMaxHealth = hp;
            a.BaseAttack = atk;
            a.BaseDefense = def;
            a.BaseSpeed = spd;
            a.BaseHealPower = heal;
            a.TacticPreset = BuildTacticPreset(@class.Id, skill);
        });
    }

    private static List<TacticPresetEntry> BuildTacticPreset(string classId, SkillDefinitionAsset skill)
    {
        if (classId == "mystic")
        {
            return new List<TacticPresetEntry>
            {
                new() { Priority = 0, ConditionType = TacticConditionTypeValue.AllyHpBelow, Threshold = 0.6f, ActionType = BattleActionTypeValue.ActiveSkill, TargetSelector = TargetSelectorTypeValue.LowestHpAlly, Skill = skill },
                new() { Priority = 1, ConditionType = TacticConditionTypeValue.EnemyInRange, Threshold = 0f, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.FirstEnemyInRange },
                new() { Priority = 2, ConditionType = TacticConditionTypeValue.Fallback, Threshold = 0f, ActionType = BattleActionTypeValue.WaitDefend, TargetSelector = TargetSelectorTypeValue.Self },
            };
        }

        return new List<TacticPresetEntry>
        {
            new() { Priority = 0, ConditionType = TacticConditionTypeValue.LowestHpEnemy, Threshold = 0f, ActionType = BattleActionTypeValue.ActiveSkill, TargetSelector = TargetSelectorTypeValue.LowestHpEnemy, Skill = skill },
            new() { Priority = 1, ConditionType = TacticConditionTypeValue.EnemyInRange, Threshold = 0f, ActionType = BattleActionTypeValue.BasicAttack, TargetSelector = TargetSelectorTypeValue.FirstEnemyInRange },
            new() { Priority = 2, ConditionType = TacticConditionTypeValue.Fallback, Threshold = 0f, ActionType = BattleActionTypeValue.WaitDefend, TargetSelector = TargetSelectorTypeValue.Self },
        };
    }

    private static void CreateAugments()
    {
        var definitions = new (string id, string name, AugmentRarity rarity, bool permanent, string stat, float value)[]
        {
            ("augment_silver_guard", "Guard Instinct", AugmentRarity.Silver, false, "defense", 1f),
            ("augment_silver_focus", "Focus", AugmentRarity.Silver, false, "attack", 1f),
            ("augment_silver_stride", "Stride", AugmentRarity.Silver, false, "speed", 1f),
            ("augment_gold_bastion", "Bastion", AugmentRarity.Gold, false, "max_health", 4f),
            ("augment_gold_fury", "Fury", AugmentRarity.Gold, false, "attack", 2f),
            ("augment_gold_mending", "Mending", AugmentRarity.Gold, false, "heal_power", 2f),
            ("augment_platinum_overrun", "Overrun", AugmentRarity.Platinum, false, "attack", 3f),
            ("augment_platinum_wall", "Wall", AugmentRarity.Platinum, false, "defense", 3f),
            ("augment_platinum_surge", "Surge", AugmentRarity.Platinum, false, "speed", 2f),
            ("augment_perm_legacy_blade", "Legacy Blade", AugmentRarity.Permanent, true, "attack", 1f),
            ("augment_perm_legacy_hide", "Legacy Hide", AugmentRarity.Permanent, true, "defense", 1f),
            ("augment_perm_legacy_grace", "Legacy Grace", AugmentRarity.Permanent, true, "speed", 1f),
        };

        foreach (var d in definitions)
        {
            CreateAsset<AugmentDefinition>($"{ResourcesRoot}/Augments/{d.id}.asset", a =>
            {
                a.Id = d.id;
                a.DisplayName = d.name;
                a.Rarity = d.rarity;
                a.IsPermanent = d.permanent;
                a.Description = d.name;
                a.Modifiers = new List<SerializableStatModifier>
                {
                    new() { StatId = d.stat, Value = d.value }
                };
            });
        }
    }

    private static void CreateItems()
    {
        var items = new (string id, string name, ItemSlotType slot, string stat, float value)[]
        {
            ("item_iron_sword", "Iron Sword", ItemSlotType.Weapon, "attack", 2f),
            ("item_bone_blade", "Bone Blade", ItemSlotType.Weapon, "attack", 2f),
            ("item_leather_armor", "Leather Armor", ItemSlotType.Armor, "defense", 1f),
            ("item_bone_plate", "Bone Plate", ItemSlotType.Armor, "max_health", 3f),
            ("item_lucky_charm", "Lucky Charm", ItemSlotType.Accessory, "speed", 1f),
            ("item_prayer_bead", "Prayer Bead", ItemSlotType.Accessory, "heal_power", 2f),
        };

        foreach (var d in items)
        {
            CreateAsset<ItemBaseDefinition>($"{ResourcesRoot}/Items/{d.id}.asset", a =>
            {
                a.Id = d.id;
                a.DisplayName = d.name;
                a.SlotType = d.slot;
                a.BaseModifiers = new List<SerializableStatModifier>
                {
                    new() { StatId = d.stat, Value = d.value }
                };
            });
        }
    }

    private static void CreateAffixes()
    {
        var affixes = new (string id, string name, string stat, float value)[]
        {
            ("affix_sturdy", "Sturdy", "defense", 1f),
            ("affix_fierce", "Fierce", "attack", 1f),
            ("affix_quick", "Quick", "speed", 1f),
            ("affix_vital", "Vital", "max_health", 2f),
            ("affix_blessed", "Blessed", "heal_power", 1f),
            ("affix_heavy", "Heavy", "defense", 2f),
            ("affix_sharp", "Sharp", "attack", 2f),
            ("affix_hasty", "Hasty", "speed", 2f),
        };

        foreach (var d in affixes)
        {
            CreateAsset<AffixDefinition>($"{ResourcesRoot}/Affixes/{d.id}.asset", a =>
            {
                a.Id = d.id;
                a.DisplayName = d.name;
                a.Description = d.name;
                a.Modifiers = new List<SerializableStatModifier>
                {
                    new() { StatId = d.stat, Value = d.value }
                };
            });
        }
    }

    private static Dictionary<string, RewardTableDefinition> CreateRewardTables()
    {
        var battle = CreateAsset<RewardTableDefinition>($"{ResourcesRoot}/Rewards/rewardtable_battle.asset", a =>
        {
            a.Id = "rewardtable_battle";
            a.DisplayName = "Battle Rewards";
            a.Rewards = new List<RewardEntry>
            {
                new() { Id = "reward.gold.10", RewardType = RewardType.Gold, Amount = 10, Label = "10 Gold" },
                new() { Id = "augment_silver_guard", RewardType = RewardType.TemporaryAugment, Amount = 1, Label = "Guard Instinct" },
                new() { Id = "reward.reroll.1", RewardType = RewardType.TraitRerollCurrency, Amount = 1, Label = "1 Trait Reroll" },
            };
        });

        var expedition = CreateAsset<RewardTableDefinition>($"{ResourcesRoot}/Rewards/rewardtable_expedition_end.asset", a =>
        {
            a.Id = "rewardtable_expedition_end";
            a.DisplayName = "Expedition End Rewards";
            a.Rewards = new List<RewardEntry>
            {
                new() { Id = "reward.gold.30", RewardType = RewardType.Gold, Amount = 30, Label = "30 Gold" },
                new() { Id = "reward.reroll.2", RewardType = RewardType.TraitRerollCurrency, Amount = 2, Label = "2 Trait Reroll" },
                new() { Id = "augment_perm_legacy_blade", RewardType = RewardType.TemporaryAugment, Amount = 1, Label = "Legacy Blade" },
            };
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
            a.DisplayName = "MVP Demo Expedition";
            a.Nodes = new List<ExpeditionNodeDefinition>
            {
                new() { Id = "node_1", Label = "Fork", RewardTable = rewardTables["rewardtable_battle"] },
                new() { Id = "node_2", Label = "Skirmish", RewardTable = rewardTables["rewardtable_battle"] },
                new() { Id = "node_3", Label = "Cache", RewardTable = rewardTables["rewardtable_battle"] },
                new() { Id = "node_4", Label = "Elite", RewardTable = rewardTables["rewardtable_battle"] },
                new() { Id = "node_5", Label = "Return", RewardTable = rewardTables["rewardtable_expedition_end"] },
            };
        });
    }

    private static TraitEntry MakeTrait(string id, string name, string statId, float value)
    {
        return new TraitEntry
        {
            Id = id,
            DisplayName = name,
            Description = name,
            Modifiers = new List<SerializableStatModifier>
            {
                new() { StatId = statId, Value = value }
            }
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
        EditorUtility.SetDirty(existing);
        return existing;
    }
}
