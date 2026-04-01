using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;

namespace SM.Unity;

public sealed record RuntimeCombatParsedContent(
    IReadOnlyList<StatDefinition> Stats,
    IReadOnlyList<RaceDefinition> Races,
    IReadOnlyList<ClassDefinition> Classes,
    IReadOnlyList<TraitPoolDefinition> TraitPools,
    IReadOnlyList<UnitArchetypeDefinition> Archetypes,
    IReadOnlyList<SkillDefinitionAsset> Skills,
    IReadOnlyList<ItemBaseDefinition> Items,
    IReadOnlyList<AffixDefinition> Affixes,
    IReadOnlyList<AugmentDefinition> Augments,
    IReadOnlyList<StableTagDefinition> StableTags,
    IReadOnlyList<TeamTacticDefinition> TeamTactics,
    IReadOnlyList<RoleInstructionDefinition> RoleInstructions,
    IReadOnlyList<PassiveBoardDefinition> PassiveBoards,
    IReadOnlyList<PassiveNodeDefinition> PassiveNodes,
    IReadOnlyList<SynergyDefinition> Synergies,
    IReadOnlyList<SynergyTierDefinition> SynergyTiers,
    IReadOnlyList<ExpeditionDefinition> Expeditions,
    IReadOnlyList<RewardTableDefinition> RewardTables,
    IReadOnlyList<CampaignChapterDefinition> CampaignChapters,
    IReadOnlyList<ExpeditionSiteDefinition> ExpeditionSites,
    IReadOnlyList<EncounterDefinition> Encounters,
    IReadOnlyList<EnemySquadTemplateDefinition> EnemySquads,
    IReadOnlyList<BossOverlayDefinition> BossOverlays,
    IReadOnlyList<StatusFamilyDefinition> StatusFamilies,
    IReadOnlyList<CleanseProfileDefinition> CleanseProfiles,
    IReadOnlyList<ControlDiminishingRuleDefinition> ControlDiminishingRules,
    IReadOnlyList<RewardSourceDefinition> RewardSources,
    IReadOnlyList<DropTableDefinition> DropTables,
    IReadOnlyList<LootBundleDefinition> LootBundles,
    IReadOnlyList<TraitTokenDefinition> TraitTokens,
    IReadOnlyDictionary<int, string> AssetPaths)
{
    public IReadOnlyList<ScriptableObject> AllAssets =>
        Stats.Cast<ScriptableObject>()
            .Concat(Races)
            .Concat(Classes)
            .Concat(TraitPools)
            .Concat(Archetypes)
            .Concat(Skills)
            .Concat(Items)
            .Concat(Affixes)
            .Concat(Augments)
            .Concat(StableTags)
            .Concat(TeamTactics)
            .Concat(RoleInstructions)
            .Concat(PassiveBoards)
            .Concat(PassiveNodes)
            .Concat(Synergies)
            .Concat(SynergyTiers)
            .Concat(Expeditions)
            .Concat(RewardTables)
            .Concat(CampaignChapters)
            .Concat(ExpeditionSites)
            .Concat(Encounters)
            .Concat(EnemySquads)
            .Concat(BossOverlays)
            .Concat(StatusFamilies)
            .Concat(CleanseProfiles)
            .Concat(ControlDiminishingRules)
            .Concat(RewardSources)
            .Concat(DropTables)
            .Concat(LootBundles)
            .Concat(TraitTokens)
            .ToList();
}

public static class RuntimeCombatContentFileParser
{
    private const string RootPath = "Assets/Resources/_Game/Content/Definitions";
    private static readonly Regex GuidRegex = new(@"guid:\s*([0-9a-fA-F]+)", RegexOptions.Compiled);

    public static bool TryLoad(out RuntimeCombatParsedContent parsed, out string error)
    {
        try
        {
            if (!Directory.Exists(RootPath))
            {
                parsed = null!;
                error = $"전투 content root를 찾을 수 없습니다. Root={RootPath}";
                return false;
            }

            var guidToPath = BuildGuidMap(RootPath);
            var stats = LoadStats(guidToPath);
            var races = LoadRaces(guidToPath);
            var classes = LoadClasses(guidToPath);
            var skills = LoadSkills(guidToPath);
            var traitPools = LoadTraitPools(guidToPath);
            var stableTags = LoadStableTags();
            var items = LoadItems(guidToPath, stableTags, skills);
            var affixes = LoadAffixes(guidToPath, stableTags);
            var augments = LoadAugments(guidToPath, stableTags);
            var teamTactics = LoadTeamTactics(guidToPath, stableTags);
            var roleInstructions = LoadRoleInstructions(guidToPath, stableTags);
            var passiveNodes = LoadPassiveNodes(guidToPath, stableTags);
            var passiveBoards = LoadPassiveBoards(guidToPath, passiveNodes);
            var synergyTiers = LoadSynergyTiers();
            var synergies = LoadSynergies(guidToPath, synergyTiers);
            var expeditions = LoadExpeditions(guidToPath);
            var rewardTables = LoadRewardTables();
            var archetypes = LoadArchetypes(guidToPath, races, classes, traitPools, skills);
            var campaignChapters = LoadCampaignChapters();
            var expeditionSites = LoadExpeditionSites();
            var encounters = LoadEncounters();
            var enemySquads = LoadEnemySquads();
            var bossOverlays = LoadBossOverlays();
            var statusFamilies = LoadStatusFamilies();
            var cleanseProfiles = LoadCleanseProfiles();
            var controlDiminishingRules = LoadControlDiminishingRules();
            var rewardSources = LoadRewardSources();
            var dropTables = LoadDropTables();
            var lootBundles = LoadLootBundles();
            var traitTokens = LoadTraitTokens();
            var assetPaths = BuildAssetPaths(
                stats.Values,
                races.Values,
                classes.Values,
                traitPools.Values,
                archetypes,
                skills.Values,
                items,
                affixes,
                augments,
                stableTags.Values,
                teamTactics.Values,
                roleInstructions.Values,
                passiveBoards.Values,
                passiveNodes.Values,
                synergies.Values,
                synergyTiers.Values,
                expeditions.Values,
                rewardTables.Values,
                campaignChapters,
                expeditionSites,
                encounters,
                enemySquads,
                bossOverlays,
                statusFamilies.Values,
                cleanseProfiles.Values,
                controlDiminishingRules.Values,
                rewardSources,
                dropTables,
                lootBundles,
                traitTokens.Values);

            parsed = new RuntimeCombatParsedContent(
                stats.Values.ToList(),
                races.Values.ToList(),
                classes.Values.ToList(),
                traitPools.Values.ToList(),
                archetypes,
                skills.Values.ToList(),
                items,
                affixes,
                augments,
                stableTags.Values.ToList(),
                teamTactics.Values.ToList(),
                roleInstructions.Values.ToList(),
                passiveBoards.Values.ToList(),
                passiveNodes.Values.ToList(),
                synergies.Values.ToList(),
                synergyTiers.Values.ToList(),
                expeditions.Values.ToList(),
                rewardTables.Values.ToList(),
                campaignChapters,
                expeditionSites,
                encounters,
                enemySquads,
                bossOverlays,
                statusFamilies.Values.ToList(),
                cleanseProfiles.Values.ToList(),
                controlDiminishingRules.Values.ToList(),
                rewardSources,
                dropTables,
                lootBundles,
                traitTokens.Values.ToList(),
                assetPaths);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            parsed = null!;
            error = ex.Message;
            return false;
        }
    }

    private static Dictionary<string, string> BuildGuidMap(string rootPath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var metaPath in Directory.EnumerateFiles(rootPath, "*.meta", SearchOption.AllDirectories))
        {
            var guid = ExtractValue(File.ReadAllLines(metaPath), "guid:");
            if (string.IsNullOrWhiteSpace(guid))
            {
                continue;
            }

            result[guid] = ToUnityPath(metaPath[..^5]);
        }

        return result;
    }

    private static Dictionary<int, string> BuildAssetPaths(params IEnumerable<ScriptableObject>[] groups)
    {
        var result = new Dictionary<int, string>();
        foreach (var group in groups)
        {
            foreach (var asset in group.Where(asset => asset != null))
            {
                var path = ResolveAssetPath(asset);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    result[asset.GetInstanceID()] = path;
                }
            }
        }

        return result;
    }

    private static string ResolveAssetPath(UnityEngine.Object asset)
    {
        var folder = asset switch
        {
            StatDefinition => "Stats",
            RaceDefinition => "Races",
            ClassDefinition => "Classes",
            TraitPoolDefinition => "Traits",
            UnitArchetypeDefinition => "Archetypes",
            SkillDefinitionAsset => "Skills",
            ItemBaseDefinition => "Items",
            AffixDefinition => "Affixes",
            AugmentDefinition => "Augments",
            StableTagDefinition => "StableTags",
            TeamTacticDefinition => "TeamTactics",
            RoleInstructionDefinition => "RoleInstructions",
            PassiveBoardDefinition => "PassiveBoards",
            PassiveNodeDefinition => "PassiveNodes",
            SynergyDefinition => "Synergies",
            SynergyTierDefinition => "Synergies",
            ExpeditionDefinition => "Expeditions",
            RewardTableDefinition => "Rewards",
            CampaignChapterDefinition => "CampaignChapters",
            ExpeditionSiteDefinition => "ExpeditionSites",
            EncounterDefinition => "Encounters",
            EnemySquadTemplateDefinition => "EnemySquads",
            BossOverlayDefinition => "BossOverlays",
            StatusFamilyDefinition => "StatusFamilies",
            CleanseProfileDefinition => "CleanseProfiles",
            ControlDiminishingRuleDefinition => "ControlDiminishingRules",
            RewardSourceDefinition => "RewardSources",
            DropTableDefinition => "DropTables",
            LootBundleDefinition => "LootBundles",
            TraitTokenDefinition => "TraitTokens",
            _ => string.Empty,
        };

        return string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(asset.name)
            ? string.Empty
            : ToUnityPath(Path.Combine(RootPath, folder, asset.name + ".asset"));
    }

    private static Dictionary<string, StatDefinition> LoadStats(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Stats", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<StatDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildStatNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildStatDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, StableTagDefinition> LoadStableTags()
    {
        return LoadAssets("StableTags", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<StableTagDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            ApplyFallbackIdentity(definition, path);
            return definition;
        });
    }

    private static Dictionary<string, TeamTacticDefinition> LoadTeamTactics(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return LoadAssets("TeamTactics", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<TeamTacticDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.Posture = (TeamPostureTypeValue)ExtractInt(lines, "Posture:");
            definition.CombatPace = ExtractFloat(lines, "CombatPace:");
            definition.FocusModeBias = ExtractFloat(lines, "FocusModeBias:");
            definition.FrontSpacingBias = ExtractFloat(lines, "FrontSpacingBias:");
            definition.BackSpacingBias = ExtractFloat(lines, "BackSpacingBias:");
            definition.ProtectCarryBias = ExtractFloat(lines, "ProtectCarryBias:");
            definition.TargetSwitchPenalty = ExtractFloat(lines, "TargetSwitchPenalty:");
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, RoleInstructionDefinition> LoadRoleInstructions(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return LoadAssets("RoleInstructions", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<RoleInstructionDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.Anchor = (DeploymentAnchorValue)ExtractInt(lines, "Anchor:");
            definition.RoleTag = ExtractValue(lines, "RoleTag:");
            definition.ProtectCarryBias = ExtractFloat(lines, "ProtectCarryBias:");
            definition.BacklinePressureBias = ExtractFloat(lines, "BacklinePressureBias:");
            definition.RetreatBias = ExtractFloat(lines, "RetreatBias:");
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, PassiveNodeDefinition> LoadPassiveNodes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return LoadAssets("PassiveNodes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<PassiveNodeDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.BoardId = ExtractValue(lines, "BoardId:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.NodeKind = (PassiveNodeKindValue)ExtractInt(lines, "NodeKind:");
            definition.PrerequisiteNodeIds = ParseStringList(lines, "PrerequisiteNodeIds:");
            definition.MutualExclusionTags = ParseReferenceList(lines, "MutualExclusionTags:", guidToPath, stableTags);
            definition.BoardDepth = ExtractInt(lines, "BoardDepth:");
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, PassiveBoardDefinition> LoadPassiveBoards(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, PassiveNodeDefinition> passiveNodes)
    {
        return LoadAssets("PassiveBoards", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<PassiveBoardDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.ClassId = ExtractValue(lines, "ClassId:");
            definition.Nodes = ParseReferenceList(lines, "Nodes:", guidToPath, passiveNodes);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, SynergyTierDefinition> LoadSynergyTiers()
    {
        return LoadAssets("Synergies", path =>
        {
            if (!Path.GetFileName(path).StartsWith("synergytier_", StringComparison.Ordinal))
            {
                return null!;
            }

            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<SynergyTierDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Threshold = ExtractInt(lines, "Threshold:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplySynergyTierFallbacks(definition, path);
            if (definition.Threshold == 3)
            {
                return null!;
            }

            return definition;
        }).Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, SynergyDefinition> LoadSynergies(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, SynergyTierDefinition> tiers)
    {
        return LoadAssets("Synergies", path =>
        {
            if (!Path.GetFileName(path).StartsWith("synergy_", StringComparison.Ordinal))
            {
                return null!;
            }

            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<SynergyDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.CountedTagId = ExtractValue(lines, "CountedTagId:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.Tiers = ParseReferenceList(lines, "Tiers:", guidToPath, tiers);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            ApplyFallbackIdentity(definition, path);
            ApplySynergyFallbacks(definition);
            return definition;
        }, guidToPath).Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static Dictionary<string, ExpeditionDefinition> LoadExpeditions(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Expeditions", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ExpeditionDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Nodes = ParseExpeditionNodes(lines, guidToPath);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, RewardTableDefinition> LoadRewardTables()
    {
        return LoadAssets("Rewards", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<RewardTableDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Rewards = ParseRewardEntries(lines);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            ApplyFallbackIdentity(definition, path);
            ApplyRewardTableFallbacks(definition);
            return definition;
        });
    }

    private static Dictionary<string, StatusFamilyDefinition> LoadStatusFamilies()
    {
        return LoadAssets("StatusFamilies", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<StatusFamilyDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Group = (StatusGroupValue)ExtractInt(lines, "Group:");
            definition.IsHardControl = ExtractBool(lines, "IsHardControl:");
            definition.UsesControlDiminishing = ExtractBool(lines, "UsesControlDiminishing:");
            definition.AffectedByTenacity = ExtractBool(lines, "AffectedByTenacity:");
            definition.TenacityScale = ExtractFloat(lines, "TenacityScale:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.IsRuleModifierOnly = ExtractBool(lines, "IsRuleModifierOnly:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.DefaultStackCap = ExtractInt(lines, "DefaultStackCap:");
            definition.DefaultStackPolicy = (StatusStackPolicyValue)ExtractInt(lines, "DefaultStackPolicy:");
            definition.DefaultRefreshPolicy = (StatusRefreshPolicyValue)ExtractInt(lines, "DefaultRefreshPolicy:");
            definition.DefaultProcAttributionPolicy = (StatusProcAttributionPolicyValue)ExtractInt(lines, "DefaultProcAttributionPolicy:");
            definition.DefaultOwnershipPolicy = (StatusOwnershipPolicyValue)ExtractInt(lines, "DefaultOwnershipPolicy:");
            definition.IsAiRelevant = ExtractBool(lines, "IsAiRelevant:");
            definition.VisualPriority = ExtractInt(lines, "VisualPriority:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.CompileTags = ParseStringList(lines, "CompileTags:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyStatusFamilyFallbacks(definition);
            return definition;
        });
    }

    private static Dictionary<string, CleanseProfileDefinition> LoadCleanseProfiles()
    {
        return LoadAssets("CleanseProfiles", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<CleanseProfileDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.RemovesStatusIds = ParseStringList(lines, "RemovesStatusIds:");
            definition.RemovesOneHardControl = ExtractBool(lines, "RemovesOneHardControl:");
            definition.GrantsUnstoppable = ExtractBool(lines, "GrantsUnstoppable:");
            definition.GrantedUnstoppableDurationSeconds = ExtractFloat(lines, "GrantedUnstoppableDurationSeconds:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyCleanseProfileFallbacks(definition);
            return definition;
        });
    }

    private static Dictionary<string, ControlDiminishingRuleDefinition> LoadControlDiminishingRules()
    {
        return LoadAssets("ControlDiminishingRules", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ControlDiminishingRuleDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.ControlResistMultiplier = ExtractFloat(lines, "ControlResistMultiplier:");
            definition.WindowSeconds = ExtractFloat(lines, "WindowSeconds:");
            definition.FullTenacityStatusIds = ParseStringList(lines, "FullTenacityStatusIds:");
            definition.PartialTenacityStatusIds = ParseStringList(lines, "PartialTenacityStatusIds:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyControlRuleFallbacks(definition);
            return definition;
        });
    }

    private static Dictionary<string, TraitTokenDefinition> LoadTraitTokens()
    {
        return LoadAssets("TraitTokens", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<TraitTokenDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.RewardType = (RewardType)ExtractInt(lines, "RewardType:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyTraitTokenFallbacks(definition);
            return definition;
        });
    }

    private static Dictionary<string, RaceDefinition> LoadRaces(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Races", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<RaceDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildRaceNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildRaceDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, ClassDefinition> LoadClasses(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Classes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ClassDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildClassNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildClassDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, SkillDefinitionAsset> LoadSkills(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Skills", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildSkillNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildSkillDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.TemplateType = (SkillTemplateTypeValue)ExtractInt(lines, "TemplateType:");
            definition.Kind = (SkillKindValue)ExtractInt(lines, "Kind:");
            definition.SlotKind = (SkillSlotKindValue)ExtractInt(lines, "SlotKind:");
            definition.DamageType = (DamageTypeValue)ExtractInt(lines, "DamageType:");
            definition.Delivery = (SkillDeliveryValue)ExtractInt(lines, "Delivery:");
            definition.TargetRule = (SkillTargetRuleValue)ExtractInt(lines, "TargetRule:");
            definition.Power = ExtractFloat(lines, "Power:");
            definition.Range = ExtractFloat(lines, "Range:");
            definition.RangeMin = ExtractFloat(lines, "RangeMin:");
            definition.RangeMax = ExtractFloat(lines, "RangeMax:");
            definition.Radius = ExtractFloat(lines, "Radius:");
            definition.Width = ExtractFloat(lines, "Width:");
            definition.ArcDegrees = ExtractFloat(lines, "ArcDegrees:");
            definition.PowerFlat = ExtractFloat(lines, "PowerFlat:");
            definition.PhysCoeff = ExtractFloat(lines, "PhysCoeff:");
            definition.MagCoeff = ExtractFloat(lines, "MagCoeff:");
            definition.HealCoeff = ExtractFloat(lines, "HealCoeff:");
            definition.HealthCoeff = ExtractFloat(lines, "HealthCoeff:");
            definition.CanCrit = ExtractBool(lines, "CanCrit:");
            definition.ActivationModel = (ActivationModel)ExtractInt(lines, "ActivationModel:");
            definition.Lane = (ActionLane)ExtractInt(lines, "Lane:");
            definition.LockRule = (ActionLockRule)ExtractInt(lines, "LockRule:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.ManaCost = ExtractFloat(lines, "ManaCost:");
            definition.ResourceCost = ExtractFloat(lines, "ResourceCost:");
            definition.BaseCooldownSeconds = ExtractFloat(lines, "BaseCooldownSeconds:");
            definition.CooldownSeconds = ExtractFloat(lines, "CooldownSeconds:");
            definition.CastWindupSeconds = ExtractFloat(lines, "CastWindupSeconds:");
            definition.RecoverySeconds = ExtractFloat(lines, "RecoverySeconds:");
            definition.PowerBudget = ExtractFloat(lines, "PowerBudget:");
            definition.InterruptRefundScalar = ExtractFloat(lines, "InterruptRefundScalar:");
            definition.AiIntents = ParsePackedEnumList<SkillAiIntentValue>(ExtractValue(lines, "AiIntents:"));
            definition.AiScoreHints = ParseSkillAiScoreHints(lines, "AiScoreHints:");
            definition.AnimationHookId = ExtractValue(lines, "AnimationHookId:");
            definition.VfxHookId = ExtractValue(lines, "VfxHookId:");
            definition.SfxHookId = ExtractValue(lines, "SfxHookId:");
            definition.LearnSource = (SkillLearnSourceValue)ExtractInt(lines, "LearnSource:");
            definition.EffectFamilyId = ExtractValue(lines, "EffectFamilyId:");
            definition.MutuallyExclusiveGroupId = ExtractValue(lines, "MutuallyExclusiveGroupId:");
            definition.RecruitNativeTags = ParseStableTagList(lines, "RecruitNativeTags:", guidToPath);
            definition.RecruitPlanTags = ParseStableTagList(lines, "RecruitPlanTags:", guidToPath);
            definition.RecruitScoutTags = ParseStableTagList(lines, "RecruitScoutTags:", guidToPath);
            definition.TargetRuleData = ParseTargetRule(lines, "TargetRuleData:");
            definition.SummonProfile = ParseSummonProfile(lines, "SummonProfile:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.CompileTags = ParseStableTagList(lines, "CompileTags:", guidToPath);
            definition.RuleModifierTags = ParseStableTagList(lines, "RuleModifierTags:", guidToPath);
            definition.SupportAllowedTags = ParseStableTagList(lines, "SupportAllowedTags:", guidToPath);
            definition.SupportBlockedTags = ParseStableTagList(lines, "SupportBlockedTags:", guidToPath);
            definition.RequiredWeaponTags = ParseStableTagList(lines, "RequiredWeaponTags:", guidToPath);
            definition.RequiredClassTags = ParseStableTagList(lines, "RequiredClassTags:", guidToPath);
            definition.AppliedStatuses = ParseStatusApplicationRules(lines, "AppliedStatuses:");
            definition.CleanseProfileId = ExtractValue(lines, "CleanseProfileId:");
            ApplyFallbackIdentity(definition, path);
            ApplySkillFallbacks(definition);
            return definition;
        }, guidToPath);
    }

    private static Dictionary<string, TraitPoolDefinition> LoadTraitPools(IReadOnlyDictionary<string, string> guidToPath)
    {
        return LoadAssets("Traits", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<TraitPoolDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.ArchetypeId = ExtractValue(lines, "ArchetypeId:");
            definition.PositiveTraits = ParseTraitEntries(lines, "PositiveTraits:", "NegativeTraits:");
            definition.NegativeTraits = ParseTraitEntries(lines, "NegativeTraits:", null);
            ApplyFallbackIdentity(definition, path);
            ApplyTraitPoolFallbacks(definition);
            return definition;
        }, guidToPath);
    }

    private static IReadOnlyList<ItemBaseDefinition> LoadItems(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        return LoadAssets("Items", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ItemBaseDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildItemNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildItemDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.SlotType = (ItemSlotType)ExtractInt(lines, "SlotType:");
            definition.IdentityKind = (ItemIdentityValue)ExtractInt(lines, "IdentityKind:");
            definition.ItemFamilyTag = ExtractValue(lines, "ItemFamilyTag:");
            definition.WeaponFamilyTag = ExtractValue(lines, "WeaponFamilyTag:");
            definition.RarityTier = (ItemRarityTierValue)ExtractInt(lines, "RarityTier:");
            definition.AffixPoolTag = ExtractValue(lines, "AffixPoolTag:");
            definition.CraftCategory = ExtractValue(lines, "CraftCategory:");
            definition.BudgetBand = ExtractValue(lines, "BudgetBand:");
            definition.CraftCurrencyTag = ExtractValue(lines, "CraftCurrencyTag:");
            definition.GrantedSkills = ParseReferenceList(lines, "GrantedSkills:", guidToPath, skills);
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.AllowedClassTags = ParseReferenceList(lines, "AllowedClassTags:", guidToPath, stableTags);
            definition.AllowedArchetypeTags = ParseReferenceList(lines, "AllowedArchetypeTags:", guidToPath, stableTags);
            definition.UniqueRuleTags = ParseReferenceList(lines, "UniqueRuleTags:", guidToPath, stableTags);
            definition.AllowedCraftOperations = ParsePackedEnumList<CraftOperationKindValue>(ExtractValue(lines, "AllowedCraftOperations:"));
            definition.BaseModifiers = ParseModifiers(lines, "BaseModifiers:");
            return definition;
        }, guidToPath).Values.ToList();
    }

    private static IReadOnlyList<AffixDefinition> LoadAffixes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return LoadAssets("Affixes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<AffixDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = $"content.affix.{ContentLocalizationTables.NormalizeId(definition.Id)}.name";
            definition.DescriptionKey = $"content.affix.{ContentLocalizationTables.NormalizeId(definition.Id)}.desc";
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            definition.Category = (AffixCategoryValue)ExtractInt(lines, "Category:");
            definition.Tier = (AffixTierValue)ExtractInt(lines, "Tier:");
            definition.AffixFamily = (AffixFamilyValue)ExtractInt(lines, "AffixFamily:");
            definition.EffectType = (AffixEffectTypeValue)ExtractInt(lines, "EffectType:");
            definition.ValueMin = ExtractFloat(lines, "ValueMin:");
            definition.ValueMax = ExtractFloat(lines, "ValueMax:");
            definition.AllowedSlotTypes = ParsePackedEnumList<ItemSlotType>(ExtractValue(lines, "AllowedSlotTypes:"));
            definition.CompileTags = ParseReferenceList(lines, "CompileTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.RequiredTags = ParseReferenceList(lines, "RequiredTags:", guidToPath, stableTags);
            definition.ExcludedTags = ParseReferenceList(lines, "ExcludedTags:", guidToPath, stableTags);
            definition.ItemLevelMin = ExtractInt(lines, "ItemLevelMin:");
            definition.SpawnWeight = ExtractFloat(lines, "SpawnWeight:");
            definition.ExclusiveGroupId = ExtractValue(lines, "ExclusiveGroupId:");
            definition.BudgetScore = ExtractFloat(lines, "BudgetScore:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.TextTemplateKey = ExtractValue(lines, "TextTemplateKey:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            ApplyFallbackIdentity(definition, path);
            ApplyAffixFallbacks(definition);
            return definition;
        }, guidToPath).Values.ToList();
    }

    private static IReadOnlyList<AugmentDefinition> LoadAugments(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return LoadAssets("Augments", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<AugmentDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildAugmentNameKey(definition.Id);
            definition.DescriptionKey = ContentLocalizationTables.BuildAugmentDescriptionKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "Description:"));
            definition.Rarity = ExtractInt(lines, "Rarity:") switch
            {
                1 => ContentRarity.Rare,
                2 => ContentRarity.Epic,
                3 => ContentRarity.Epic,
                _ => ContentRarity.Common,
            };
            definition.IsPermanent = ExtractInt(lines, "IsPermanent:") != 0;
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.Category = (AugmentCategoryValue)ExtractInt(lines, "Category:");
            definition.FamilyId = ExtractValue(lines, "FamilyId:");
            definition.Tier = ExtractInt(lines, "Tier:");
            definition.OfferWeight = ExtractFloat(lines, "OfferWeight:");
            definition.OfferBucket = (AugmentOfferBucketValue)ExtractInt(lines, "OfferBucket:");
            definition.RiskRewardClass = (AugmentRiskRewardClassValue)ExtractInt(lines, "RiskRewardClass:");
            definition.BudgetScore = ExtractFloat(lines, "BudgetScore:");
            definition.SuppressIfPermanentEquipped = ExtractBool(lines, "SuppressIfPermanentEquipped:");
            definition.Tags = ParseReferenceList(lines, "Tags:", guidToPath, stableTags);
            definition.BuildBiasTags = ParseReferenceList(lines, "BuildBiasTags:", guidToPath, stableTags);
            definition.ProtectionTags = ParseReferenceList(lines, "ProtectionTags:", guidToPath, stableTags);
            definition.MutualExclusionTags = ParseReferenceList(lines, "MutualExclusionTags:", guidToPath, stableTags);
            definition.RequiresTags = ParseReferenceList(lines, "RequiresTags:", guidToPath, stableTags);
            definition.RuleModifierTags = ParseReferenceList(lines, "RuleModifierTags:", guidToPath, stableTags);
            definition.EligibleModes = (AugmentEligibleModeValue)ExtractInt(lines, "EligibleModes:");
            definition.AuthorityLayer = (AuthorityLayer)ExtractInt(lines, "AuthorityLayer:");
            definition.RosterSlotDelta = ExtractInt(lines, "RosterSlotDelta:");
            definition.Effects = ParseEffectDescriptors(lines, "Effects:");
            definition.Modifiers = ParseModifiers(lines, "Modifiers:");
            ApplyFallbackIdentity(definition, path);
            ApplyAugmentFallbacks(definition);
            return definition;
        }, guidToPath).Values.ToList();
    }

    private static IReadOnlyList<UnitArchetypeDefinition> LoadArchetypes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        return LoadAssets("Archetypes", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<UnitArchetypeDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ContentLocalizationTables.BuildArchetypeNameKey(definition.Id);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "DisplayName:"));
            definition.Race = ResolveReference(lines, "Race:", guidToPath, races);
            definition.Class = ResolveReference(lines, "Class:", guidToPath, classes);
            definition.TraitPool = ResolveReference(lines, "TraitPool:", guidToPath, traitPools);
            definition.ScopeKind = (ArchetypeScopeValue)ExtractInt(lines, "ScopeKind:");
            definition.RoleFamilyTag = ExtractValue(lines, "RoleFamilyTag:");
            definition.PrimaryWeaponFamilyTag = ExtractValue(lines, "PrimaryWeaponFamilyTag:");
            definition.Skills = ParseGuidList(lines, "Skills:")
                .Select(guid => ResolveGuid(guid, guidToPath, skills))
                .Where(skill => skill != null)
                .Select(skill => skill!)
                .ToList();
            definition.TacticPreset = ParseTacticPreset(lines, guidToPath, skills);
            definition.DefaultAnchor = (DeploymentAnchorValue)ExtractInt(lines, "DefaultAnchor:");
            definition.PreferredTeamPosture = (TeamPostureTypeValue)ExtractInt(lines, "PreferredTeamPosture:");
            definition.RoleTag = ExtractValue(lines, "RoleTag:");
            definition.BaseMaxHealth = ExtractFloat(lines, "BaseMaxHealth:");
            definition.RoleFamilyTag = ExtractValue(lines, "RoleFamilyTag:");
            definition.PrimaryWeaponFamilyTag = ExtractValue(lines, "PrimaryWeaponFamilyTag:");
            definition.RecruitTier = (RecruitTier)ExtractInt(lines, "RecruitTier:");
            definition.BudgetCard = ParseBudgetCard(lines, "BudgetCard:") ?? definition.BudgetCard;
            definition.IsRecruitable = ExtractBool(lines, "IsRecruitable:");
            definition.IsSummonOnly = ExtractBool(lines, "IsSummonOnly:");
            definition.IsEventOnly = ExtractBool(lines, "IsEventOnly:");
            definition.IsBossOnly = ExtractBool(lines, "IsBossOnly:");
            definition.IsUnreleased = ExtractBool(lines, "IsUnreleased:");
            definition.IsTestOnly = ExtractBool(lines, "IsTestOnly:");
            definition.RecruitPlanTags = ParseStableTagList(lines, "RecruitPlanTags:", guidToPath);
            definition.ScoutBiasTags = ParseStableTagList(lines, "ScoutBiasTags:", guidToPath);
            definition.SupportModifierBiasTags = ParseStableTagList(lines, "SupportModifierBiasTags:", guidToPath);
            definition.LockedAttackProfileId = ExtractValue(lines, "LockedAttackProfileId:");
            definition.LockedAttackProfileTag = ExtractValue(lines, "LockedAttackProfileTag:");
            definition.LockedSignatureActiveSkill = ResolveReferenceFromLine(ExtractLine(lines, "LockedSignatureActiveSkill:"), guidToPath, skills);
            definition.LockedSignaturePassiveSkill = ResolveReferenceFromLine(ExtractLine(lines, "LockedSignaturePassiveSkill:"), guidToPath, skills);
            definition.FlexUtilitySkillPool = ParseReferenceList(lines, "FlexUtilitySkillPool:", guidToPath, skills);
            definition.FlexSupportSkillPool = ParseReferenceList(lines, "FlexSupportSkillPool:", guidToPath, skills);
            definition.RecruitFlexActivePool = ParseReferenceList(lines, "RecruitFlexActivePool:", guidToPath, skills);
            definition.RecruitFlexPassivePool = ParseReferenceList(lines, "RecruitFlexPassivePool:", guidToPath, skills);
            definition.RecruitBannedPairings = ParseRecruitBannedPairings(lines, "RecruitBannedPairings:");
            definition.Loadout = ParseUnitLoadout(lines, guidToPath, skills);
            definition.BaseAttack = ExtractFloat(lines, "BaseAttack:");
            definition.BaseDefense = ExtractFloat(lines, "BaseDefense:");
            definition.BaseSpeed = ExtractFloat(lines, "BaseSpeed:");
            definition.BaseHealPower = ExtractFloat(lines, "BaseHealPower:");
            definition.BaseMoveSpeed = ExtractFloat(lines, "BaseMoveSpeed:");
            definition.BaseAttackRange = ExtractFloat(lines, "BaseAttackRange:");
            definition.BaseMaxEnergy = ExtractFloat(lines, "BaseMaxEnergy:");
            definition.BaseStartingEnergy = ExtractFloat(lines, "BaseStartingEnergy:");
            definition.BaseSkillHaste = ExtractFloat(lines, "BaseSkillHaste:");
            definition.BaseManaMax = ExtractFloat(lines, "BaseManaMax:");
            definition.BaseManaGainOnAttack = ExtractFloat(lines, "BaseManaGainOnAttack:");
            definition.BaseManaGainOnHit = ExtractFloat(lines, "BaseManaGainOnHit:");
            definition.BaseCooldownRecovery = ExtractFloat(lines, "BaseCooldownRecovery:");
            definition.BaseCritChance = ExtractFloat(lines, "BaseCritChance:");
            definition.BaseCritMultiplier = ExtractFloat(lines, "BaseCritMultiplier:");
            definition.BasePhysPen = ExtractFloat(lines, "BasePhysPen:");
            definition.BaseMagPen = ExtractFloat(lines, "BaseMagPen:");
            definition.BaseAggroRadius = ExtractFloat(lines, "BaseAggroRadius:");
            definition.BasePreferredDistance = ExtractFloat(lines, "BasePreferredDistance:");
            definition.BaseProtectRadius = ExtractFloat(lines, "BaseProtectRadius:");
            definition.BaseAttackWindup = ExtractFloat(lines, "BaseAttackWindup:");
            definition.BaseCastWindup = ExtractFloat(lines, "BaseCastWindup:");
            definition.BaseProjectileSpeed = ExtractFloat(lines, "BaseProjectileSpeed:");
            definition.BaseCollisionRadius = ExtractFloat(lines, "BaseCollisionRadius:");
            definition.BaseRepositionCooldown = ExtractFloat(lines, "BaseRepositionCooldown:");
            definition.BaseAttackCooldown = ExtractFloat(lines, "BaseAttackCooldown:");
            definition.BaseLeashDistance = ExtractFloat(lines, "BaseLeashDistance:");
            definition.BaseTargetSwitchDelay = ExtractFloat(lines, "BaseTargetSwitchDelay:");
            ApplyFallbackIdentity(definition, path);
            ApplyArchetypeFallbacks(definition, races, classes, traitPools, skills);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<CampaignChapterDefinition> LoadCampaignChapters()
    {
        return LoadAssets("CampaignChapters", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<CampaignChapterDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.StoryOrder = ExtractInt(lines, "StoryOrder:");
            definition.SiteIds = ParseStringList(lines, "SiteIds:");
            definition.UnlocksEndlessOnClear = ExtractBool(lines, "UnlocksEndlessOnClear:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyCampaignChapterFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<ExpeditionSiteDefinition> LoadExpeditionSites()
    {
        return LoadAssets("ExpeditionSites", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<ExpeditionSiteDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.ChapterId = ExtractValue(lines, "ChapterId:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.SiteOrder = ExtractInt(lines, "SiteOrder:");
            definition.FactionId = ExtractValue(lines, "FactionId:");
            definition.EncounterIds = ParseStringList(lines, "EncounterIds:");
            definition.ExtractRewardSourceId = ExtractValue(lines, "ExtractRewardSourceId:");
            definition.ThreatTier = (ThreatTierValue)ExtractInt(lines, "ThreatTier:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyExpeditionSiteFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<EncounterDefinition> LoadEncounters()
    {
        return LoadAssets("Encounters", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<EncounterDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Kind = (EncounterKindValue)ExtractInt(lines, "Kind:");
            definition.SiteId = ExtractValue(lines, "SiteId:");
            definition.EnemySquadTemplateId = ExtractValue(lines, "EnemySquadTemplateId:");
            definition.BossOverlayId = ExtractValue(lines, "BossOverlayId:");
            definition.RewardSourceId = ExtractValue(lines, "RewardSourceId:");
            definition.FactionId = ExtractValue(lines, "FactionId:");
            definition.ThreatTier = (ThreatTierValue)ExtractInt(lines, "ThreatTier:");
            definition.ThreatCost = ExtractInt(lines, "ThreatCost:");
            definition.ThreatSkulls = ExtractInt(lines, "ThreatSkulls:");
            definition.DifficultyBand = ExtractValue(lines, "DifficultyBand:");
            definition.RewardDropTags = ParseStringList(lines, "RewardDropTags:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyEncounterFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<EnemySquadTemplateDefinition> LoadEnemySquads()
    {
        return LoadAssets("EnemySquads", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<EnemySquadTemplateDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.FactionId = ExtractValue(lines, "FactionId:");
            definition.EnemyPosture = (TeamPostureTypeValue)ExtractInt(lines, "EnemyPosture:");
            definition.ThreatTier = (ThreatTierValue)ExtractInt(lines, "ThreatTier:");
            definition.ThreatCost = ExtractInt(lines, "ThreatCost:");
            definition.RewardDropTags = ParseStringList(lines, "RewardDropTags:");
            definition.Members = ParseEnemySquadMembers(lines);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyEnemySquadFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<BossOverlayDefinition> LoadBossOverlays()
    {
        return LoadAssets("BossOverlays", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<BossOverlayDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.PhaseTrigger = (BossPhaseTriggerValue)ExtractInt(lines, "PhaseTrigger:");
            definition.ThreatCost = ExtractInt(lines, "ThreatCost:");
            definition.SignatureAuraTag = ExtractValue(lines, "SignatureAuraTag:");
            definition.SignatureUtilityTag = ExtractValue(lines, "SignatureUtilityTag:");
            definition.RewardDropTags = ParseStringList(lines, "RewardDropTags:");
            definition.AppliedStatuses = ParseStatusApplicationRules(lines, "AppliedStatuses:");
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<RewardSourceDefinition> LoadRewardSources()
    {
        return LoadAssets("RewardSources", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<RewardSourceDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.Kind = (RewardSourceKindValue)ExtractInt(lines, "Kind:");
            definition.DropTableId = ExtractValue(lines, "DropTableId:");
            definition.UsesRewardCards = ExtractBool(lines, "UsesRewardCards:");
            definition.AllowedRarityBrackets = ParsePackedEnumList<RarityBracketValue>(ExtractValue(lines, "AllowedRarityBrackets:"));
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyRewardSourceFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<DropTableDefinition> LoadDropTables()
    {
        return LoadAssets("DropTables", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<DropTableDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.RewardSourceId = ExtractValue(lines, "RewardSourceId:");
            definition.Entries = ParseLootEntries(lines);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyDropTableFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static IReadOnlyList<LootBundleDefinition> LoadLootBundles()
    {
        return LoadAssets("LootBundles", path =>
        {
            var lines = File.ReadAllLines(path);
            var definition = ScriptableObject.CreateInstance<LootBundleDefinition>();
            definition.Id = ExtractValue(lines, "Id:");
            definition.NameKey = ExtractValue(lines, "NameKey:");
            definition.DescriptionKey = ExtractValue(lines, "DescriptionKey:");
            definition.RewardSourceId = ExtractValue(lines, "RewardSourceId:");
            definition.Entries = ParseLootEntries(lines);
            SetLegacyField(definition, "legacyDisplayName", ExtractValue(lines, "legacyDisplayName:"));
            SetLegacyField(definition, "legacyDescription", ExtractValue(lines, "legacyDescription:"));
            ApplyFallbackIdentity(definition, path);
            ApplyLootBundleFallbacks(definition);
            return definition;
        }).Values.ToList();
    }

    private static Dictionary<string, T> LoadAssets<T>(
        string folderName,
        Func<string, T> loader,
        IReadOnlyDictionary<string, string>? guidToPath = null)
        where T : UnityEngine.Object
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        var folderPath = Path.Combine(RootPath, folderName);
        if (!Directory.Exists(folderPath))
        {
            return result;
        }

        foreach (var assetPath in Directory.EnumerateFiles(folderPath, "*.asset", SearchOption.TopDirectoryOnly))
        {
            var definition = loader(assetPath);
            if (definition == null)
            {
                continue;
            }

            definition.name = Path.GetFileNameWithoutExtension(assetPath);
            ApplyFallbackIdentity(definition, assetPath);
            var id = ExtractDefinitionId(definition);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            result[id] = definition;
            if (guidToPath != null)
            {
                var metaGuid = ExtractValue(File.ReadAllLines(assetPath + ".meta"), "guid:");
                if (!string.IsNullOrWhiteSpace(metaGuid) && !guidToPath.ContainsKey(metaGuid))
                {
                    ((Dictionary<string, string>)guidToPath)[metaGuid] = ToUnityPath(assetPath);
                }
            }
        }

        return result;
    }

    private static string ExtractDefinitionId(UnityEngine.Object definition)
    {
        return definition switch
        {
            StatDefinition stat => stat.Id,
            RaceDefinition race => race.Id,
            ClassDefinition @class => @class.Id,
            SkillDefinitionAsset skill => skill.Id,
            TraitPoolDefinition traitPool => traitPool.Id,
            ItemBaseDefinition item => item.Id,
            AffixDefinition affix => affix.Id,
            AugmentDefinition augment => augment.Id,
            StableTagDefinition stableTag => stableTag.Id,
            TeamTacticDefinition teamTactic => teamTactic.Id,
            RoleInstructionDefinition roleInstruction => roleInstruction.Id,
            PassiveBoardDefinition passiveBoard => passiveBoard.Id,
            PassiveNodeDefinition passiveNode => passiveNode.Id,
            SynergyDefinition synergy => synergy.Id,
            SynergyTierDefinition synergyTier => synergyTier.Id,
            ExpeditionDefinition expedition => expedition.Id,
            RewardTableDefinition rewardTable => rewardTable.Id,
            UnitArchetypeDefinition archetype => archetype.Id,
            CampaignChapterDefinition chapter => chapter.Id,
            ExpeditionSiteDefinition site => site.Id,
            EncounterDefinition encounter => encounter.Id,
            EnemySquadTemplateDefinition squad => squad.Id,
            BossOverlayDefinition overlay => overlay.Id,
            StatusFamilyDefinition statusFamily => statusFamily.Id,
            CleanseProfileDefinition cleanseProfile => cleanseProfile.Id,
            ControlDiminishingRuleDefinition controlRule => controlRule.Id,
            RewardSourceDefinition rewardSource => rewardSource.Id,
            DropTableDefinition dropTable => dropTable.Id,
            LootBundleDefinition lootBundle => lootBundle.Id,
            TraitTokenDefinition traitToken => traitToken.Id,
            _ => string.Empty
        };
    }

    private static List<TraitEntry> ParseTraitEntries(string[] lines, string sectionHeader, string? endSectionHeader)
    {
        var result = new List<TraitEntry>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!string.IsNullOrWhiteSpace(endSectionHeader) && string.Equals(trimmed, endSectionHeader, StringComparison.Ordinal))
            {
                break;
            }

            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                continue;
            }

            var trait = new TraitEntry
            {
                Id = trimmed["- Id:".Length..].Trim()
            };
            trait.NameKey = ContentLocalizationTables.BuildTraitNameKey(string.Empty, trait.Id);
            trait.DescriptionKey = ContentLocalizationTables.BuildTraitDescriptionKey(string.Empty, trait.Id);

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(endSectionHeader) && string.Equals(trimmed, endSectionHeader, StringComparison.Ordinal))
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("DisplayName:", StringComparison.Ordinal))
                {
                    SetLegacyField(trait, "legacyDisplayName", trimmed["DisplayName:".Length..].Trim());
                    continue;
                }

                if (trimmed.StartsWith("Description:", StringComparison.Ordinal))
                {
                    SetLegacyField(trait, "legacyDescription", trimmed["Description:".Length..].Trim());
                    continue;
                }

                if (string.Equals(trimmed, "Modifiers:", StringComparison.Ordinal))
                {
                    trait.Modifiers = ParseNestedModifiers(lines, ref index);
                }
            }

            result.Add(trait);
        }

        return result;
    }

    private static List<TacticPresetEntry> ParseTacticPreset(
        string[] lines,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        var result = new List<TacticPresetEntry>();
        var index = FindLineIndex(lines, "TacticPreset:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Priority:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var entry = new TacticPresetEntry
            {
                Priority = ParseInt(trimmed["- Priority:".Length..].Trim())
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Priority:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("ConditionType:", StringComparison.Ordinal))
                {
                    entry.ConditionType = (TacticConditionTypeValue)ParseInt(trimmed["ConditionType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Threshold:", StringComparison.Ordinal))
                {
                    entry.Threshold = ParseFloat(trimmed["Threshold:".Length..].Trim());
                }
                else if (trimmed.StartsWith("ActionType:", StringComparison.Ordinal))
                {
                    entry.ActionType = (BattleActionTypeValue)ParseInt(trimmed["ActionType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("TargetSelector:", StringComparison.Ordinal))
                {
                    entry.TargetSelector = (TargetSelectorTypeValue)ParseInt(trimmed["TargetSelector:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Skill:", StringComparison.Ordinal))
                {
                    var guid = ExtractGuid(trimmed);
                    entry.Skill = string.IsNullOrWhiteSpace(guid) ? null : ResolveGuid(guid, guidToPath, skills);
                }
            }

            result.Add(entry);
        }

        return result;
    }

    private static List<SerializableStatModifier> ParseModifiers(string[] lines, string sectionHeader)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return new List<SerializableStatModifier>();
        }

        return ParseNestedModifiers(lines, ref index);
    }

    private static List<SerializableStatModifier> ParseNestedModifiers(string[] lines, ref int index)
    {
        var result = new List<SerializableStatModifier>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- StatId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 4 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                continue;
            }

            var modifier = new SerializableStatModifier
            {
                StatId = trimmed["- StatId:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- StatId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 4 && trimmed.EndsWith(":", StringComparison.Ordinal))
                    || trimmed.StartsWith("- Id:", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Op:", StringComparison.Ordinal))
                {
                    modifier.Op = (ModifierOp)ParseInt(trimmed["Op:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Value:", StringComparison.Ordinal))
                {
                    modifier.Value = ParseFloat(trimmed["Value:".Length..].Trim());
                }
            }

            result.Add(modifier);
        }

        return result;
    }

    private static List<string> ParseGuidList(string[] lines, string sectionHeader)
    {
        var result = new List<string>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- {", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var guid = ExtractGuid(trimmed);
            if (!string.IsNullOrWhiteSpace(guid))
            {
                result.Add(guid);
            }
        }

        return result;
    }

    private static List<string> ParseStringList(string[] lines, string sectionHeader)
    {
        var result = new List<string>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var value = trimmed[1..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static List<EnemySquadMemberDefinition> ParseEnemySquadMembers(string[] lines)
    {
        var result = new List<EnemySquadMemberDefinition>();
        var index = FindLineIndex(lines, "Members:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var member = new EnemySquadMemberDefinition
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("NameKey:", StringComparison.Ordinal))
                {
                    member.NameKey = trimmed["NameKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("ArchetypeId:", StringComparison.Ordinal))
                {
                    member.ArchetypeId = trimmed["ArchetypeId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("Anchor:", StringComparison.Ordinal))
                {
                    member.Anchor = (DeploymentAnchorValue)ParseInt(trimmed["Anchor:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Role:", StringComparison.Ordinal))
                {
                    member.Role = (EnemySquadMemberRoleValue)ParseInt(trimmed["Role:".Length..].Trim());
                }
                else if (trimmed.StartsWith("PositiveTraitId:", StringComparison.Ordinal))
                {
                    member.PositiveTraitId = trimmed["PositiveTraitId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("NegativeTraitId:", StringComparison.Ordinal))
                {
                    member.NegativeTraitId = trimmed["NegativeTraitId:".Length..].Trim();
                }
                else if (string.Equals(trimmed, "RuleModifierTags: []", StringComparison.Ordinal))
                {
                    member.RuleModifierTags = new List<string>();
                }
                else if (string.Equals(trimmed, "RuleModifierTags:", StringComparison.Ordinal))
                {
                    member.RuleModifierTags = ParseIndentedStringList(lines, ref index, 4);
                }
                else if (trimmed.StartsWith("legacyDisplayName:", StringComparison.Ordinal))
                {
                    SetLegacyField(member, "legacyDisplayName", trimmed["legacyDisplayName:".Length..].Trim());
                }
            }

            result.Add(member);
        }

        return result;
    }

    private static List<StatusApplicationRule> ParseStatusApplicationRules(string[] lines, string sectionHeader)
    {
        var result = new List<StatusApplicationRule>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var rule = new StatusApplicationRule
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("StatusId:", StringComparison.Ordinal))
                {
                    rule.StatusId = trimmed["StatusId:".Length..].Trim();
                }
                else if (trimmed.StartsWith("DurationSeconds:", StringComparison.Ordinal))
                {
                    rule.DurationSeconds = ParseFloat(trimmed["DurationSeconds:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Magnitude:", StringComparison.Ordinal))
                {
                    rule.Magnitude = ParseFloat(trimmed["Magnitude:".Length..].Trim());
                }
                else if (trimmed.StartsWith("MaxStacks:", StringComparison.Ordinal))
                {
                    rule.MaxStacks = ParseInt(trimmed["MaxStacks:".Length..].Trim());
                }
                else if (trimmed.StartsWith("RefreshDurationOnReapply:", StringComparison.Ordinal))
                {
                    rule.RefreshDurationOnReapply = ParseBool(trimmed["RefreshDurationOnReapply:".Length..].Trim());
                }
            }

            result.Add(rule);
        }

        return result;
    }

    private static List<LootBundleEntryDefinition> ParseLootEntries(string[] lines)
    {
        var result = new List<LootBundleEntryDefinition>();
        var index = FindLineIndex(lines, "Entries:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var entry = new LootBundleEntryDefinition
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("RewardType:", StringComparison.Ordinal))
                {
                    entry.RewardType = (RewardType)ParseInt(trimmed["RewardType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Amount:", StringComparison.Ordinal))
                {
                    entry.Amount = ParseInt(trimmed["Amount:".Length..].Trim());
                }
                else if (trimmed.StartsWith("RarityBracket:", StringComparison.Ordinal))
                {
                    entry.RarityBracket = (RarityBracketValue)ParseInt(trimmed["RarityBracket:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Weight:", StringComparison.Ordinal))
                {
                    entry.Weight = ParseInt(trimmed["Weight:".Length..].Trim());
                }
                else if (trimmed.StartsWith("IsGuaranteed:", StringComparison.Ordinal))
                {
                    entry.IsGuaranteed = ParseBool(trimmed["IsGuaranteed:".Length..].Trim());
                }
            }

            result.Add(entry);
        }

        return result;
    }

    private static List<RewardEntry> ParseRewardEntries(string[] lines)
    {
        var result = new List<RewardEntry>();
        var index = FindLineIndex(lines, "Rewards:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var entry = new RewardEntry
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("RewardType:", StringComparison.Ordinal))
                {
                    entry.RewardType = (RewardType)ParseInt(trimmed["RewardType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Amount:", StringComparison.Ordinal))
                {
                    entry.Amount = ParseInt(trimmed["Amount:".Length..].Trim());
                }
                else if (trimmed.StartsWith("LabelKey:", StringComparison.Ordinal))
                {
                    entry.LabelKey = trimmed["LabelKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("legacyLabel:", StringComparison.Ordinal))
                {
                    SetLegacyField(entry, "legacyLabel", trimmed["legacyLabel:".Length..].Trim());
                }
            }

            result.Add(entry);
        }

        return result;
    }

    private static List<ExpeditionNodeDefinition> ParseExpeditionNodes(string[] lines, IReadOnlyDictionary<string, string> guidToPath)
    {
        var result = new List<ExpeditionNodeDefinition>();
        var index = FindLineIndex(lines, "Nodes:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Id:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var node = new ExpeditionNodeDefinition
            {
                Id = trimmed["- Id:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Id:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("LabelKey:", StringComparison.Ordinal))
                {
                    node.LabelKey = trimmed["LabelKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("DescriptionKey:", StringComparison.Ordinal))
                {
                    node.DescriptionKey = trimmed["DescriptionKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("RewardSummaryKey:", StringComparison.Ordinal))
                {
                    node.RewardSummaryKey = trimmed["RewardSummaryKey:".Length..].Trim();
                }
                else if (trimmed.StartsWith("RewardTable:", StringComparison.Ordinal))
                {
                    var tableGuid = ExtractGuid(trimmed);
                    if (!string.IsNullOrWhiteSpace(tableGuid) && guidToPath.TryGetValue(tableGuid, out var rewardTablePath))
                    {
                        node.RewardTable = ScriptableObject.CreateInstance<RewardTableDefinition>();
                        node.RewardTable.Id = Path.GetFileNameWithoutExtension(rewardTablePath)["rewardtable_".Length..];
                        node.RewardTable.name = Path.GetFileNameWithoutExtension(rewardTablePath);
                    }
                }
                else if (trimmed.StartsWith("legacyLabel:", StringComparison.Ordinal))
                {
                    SetLegacyField(node, "legacyLabel", trimmed["legacyLabel:".Length..].Trim());
                }
            }

            result.Add(node);
        }

        return result;
    }

    private static BudgetCard? ParseBudgetCard(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return null;
        }

        var card = new BudgetCard();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("Domain:", StringComparison.Ordinal))
            {
                card.Domain = (BudgetDomain)ParseInt(trimmed["Domain:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Rarity:", StringComparison.Ordinal))
            {
                card.Rarity = (ContentRarity)ParseInt(trimmed["Rarity:".Length..].Trim());
            }
            else if (trimmed.StartsWith("PowerBand:", StringComparison.Ordinal))
            {
                card.PowerBand = (PowerBand)ParseInt(trimmed["PowerBand:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RoleProfile:", StringComparison.Ordinal))
            {
                card.RoleProfile = (CombatRoleBudgetProfile)ParseInt(trimmed["RoleProfile:".Length..].Trim());
            }
            else if (string.Equals(trimmed, "Vector:", StringComparison.Ordinal))
            {
                card.Vector = ParseBudgetVector(lines, ref index, indent);
            }
            else if (trimmed.StartsWith("KeywordCount:", StringComparison.Ordinal))
            {
                card.KeywordCount = ParseInt(trimmed["KeywordCount:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ConditionClauseCount:", StringComparison.Ordinal))
            {
                card.ConditionClauseCount = ParseInt(trimmed["ConditionClauseCount:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RuleExceptionCount:", StringComparison.Ordinal))
            {
                card.RuleExceptionCount = ParseInt(trimmed["RuleExceptionCount:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DeclaredThreatPatterns:", StringComparison.Ordinal))
            {
                var packed = trimmed["DeclaredThreatPatterns:".Length..].Trim();
                card.DeclaredThreatPatterns = string.IsNullOrWhiteSpace(packed)
                    ? Array.Empty<ThreatPattern>()
                    : ParsePackedEnumList<ThreatPattern>(packed).ToArray();
            }
            else if (trimmed.StartsWith("DeclaredCounterTools:", StringComparison.Ordinal))
            {
                card.DeclaredCounterTools = ParseCounterToolContributions(lines, ref index, indent).ToArray();
            }
            else if (trimmed.StartsWith("DeclaredFeatureFlags:", StringComparison.Ordinal))
            {
                card.DeclaredFeatureFlags = (ContentFeatureFlag)ParseInt(trimmed["DeclaredFeatureFlags:".Length..].Trim());
            }
        }

        card.Vector ??= new BudgetVector();
        card.DeclaredThreatPatterns ??= Array.Empty<ThreatPattern>();
        card.DeclaredCounterTools ??= Array.Empty<CounterToolContribution>();
        return card;
    }

    private static BudgetVector ParseBudgetVector(string[] lines, ref int index, int sectionIndent)
    {
        var vector = new BudgetVector();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("SustainedDamage:", StringComparison.Ordinal))
            {
                vector.SustainedDamage = ParseInt(trimmed["SustainedDamage:".Length..].Trim());
            }
            else if (trimmed.StartsWith("BurstDamage:", StringComparison.Ordinal))
            {
                vector.BurstDamage = ParseInt(trimmed["BurstDamage:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Durability:", StringComparison.Ordinal))
            {
                vector.Durability = ParseInt(trimmed["Durability:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Control:", StringComparison.Ordinal))
            {
                vector.Control = ParseInt(trimmed["Control:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Mobility:", StringComparison.Ordinal))
            {
                vector.Mobility = ParseInt(trimmed["Mobility:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Support:", StringComparison.Ordinal))
            {
                vector.Support = ParseInt(trimmed["Support:".Length..].Trim());
            }
            else if (trimmed.StartsWith("CounterCoverage:", StringComparison.Ordinal))
            {
                vector.CounterCoverage = ParseInt(trimmed["CounterCoverage:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Reliability:", StringComparison.Ordinal))
            {
                vector.Reliability = ParseInt(trimmed["Reliability:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Economy:", StringComparison.Ordinal))
            {
                vector.Economy = ParseInt(trimmed["Economy:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DrawbackCredit:", StringComparison.Ordinal))
            {
                vector.DrawbackCredit = ParseInt(trimmed["DrawbackCredit:".Length..].Trim());
            }
        }

        return vector;
    }

    private static List<CounterToolContribution> ParseCounterToolContributions(string[] lines, ref int index, int sectionIndent)
    {
        var result = new List<CounterToolContribution>();
        if (lines[index].TrimEnd().EndsWith("[]", StringComparison.Ordinal))
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (!trimmed.StartsWith("- Tool:", StringComparison.Ordinal))
            {
                continue;
            }

            var contribution = new CounterToolContribution
            {
                Tool = (CounterTool)ParseInt(trimmed["- Tool:".Length..].Trim())
            };

            for (index++; index < lines.Length; index++)
            {
                line = lines[index];
                trimmed = line.Trim();
                indent = GetIndent(line);
                if (trimmed.StartsWith("- Tool:", StringComparison.Ordinal)
                    || (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Strength:", StringComparison.Ordinal))
                {
                    contribution.Strength = (CounterCoverageStrength)ParseInt(trimmed["Strength:".Length..].Trim());
                }
            }

            result.Add(contribution);
        }

        return result;
    }

    private static TargetRule ParseTargetRule(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        return index < 0 ? new TargetRule() : ParseTargetRule(lines, ref index, sectionIndent);
    }

    private static TargetRule ParseTargetRule(string[] lines, ref int index, int sectionIndent)
    {
        var rule = new TargetRule();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("Domain:", StringComparison.Ordinal))
            {
                rule.Domain = (TargetDomain)ParseInt(trimmed["Domain:".Length..].Trim());
            }
            else if (trimmed.StartsWith("PrimarySelector:", StringComparison.Ordinal))
            {
                rule.PrimarySelector = (TargetSelector)ParseInt(trimmed["PrimarySelector:".Length..].Trim());
            }
            else if (trimmed.StartsWith("FallbackPolicy:", StringComparison.Ordinal))
            {
                rule.FallbackPolicy = (TargetFallbackPolicy)ParseInt(trimmed["FallbackPolicy:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Filters:", StringComparison.Ordinal))
            {
                rule.Filters = (TargetFilterFlags)ParseInt(trimmed["Filters:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ReevaluateIntervalSeconds:", StringComparison.Ordinal))
            {
                rule.ReevaluateIntervalSeconds = ParseFloat(trimmed["ReevaluateIntervalSeconds:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MinimumCommitSeconds:", StringComparison.Ordinal))
            {
                rule.MinimumCommitSeconds = ParseFloat(trimmed["MinimumCommitSeconds:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaxAcquireRange:", StringComparison.Ordinal))
            {
                rule.MaxAcquireRange = ParseFloat(trimmed["MaxAcquireRange:".Length..].Trim());
            }
            else if (trimmed.StartsWith("PreferredMinTargets:", StringComparison.Ordinal))
            {
                rule.PreferredMinTargets = ParseInt(trimmed["PreferredMinTargets:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ClusterRadius:", StringComparison.Ordinal))
            {
                rule.ClusterRadius = ParseFloat(trimmed["ClusterRadius:".Length..].Trim());
            }
            else if (trimmed.StartsWith("LockTargetAtCastStart:", StringComparison.Ordinal))
            {
                rule.LockTargetAtCastStart = ParseBool(trimmed["LockTargetAtCastStart:".Length..].Trim());
            }
            else if (trimmed.StartsWith("RetargetLockMode:", StringComparison.Ordinal))
            {
                rule.RetargetLockMode = (RetargetLockMode)ParseInt(trimmed["RetargetLockMode:".Length..].Trim());
            }
        }

        return rule;
    }

    private static SummonProfile? ParseSummonProfile(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return null;
        }

        return ParseSummonProfile(lines, ref index, sectionIndent);
    }

    private static SummonProfile ParseSummonProfile(string[] lines, ref int index, int sectionIndent)
    {
        var profile = new SummonProfile();
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (indent <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("EntityKind:", StringComparison.Ordinal))
            {
                profile.EntityKind = (CombatEntityKind)ParseInt(trimmed["EntityKind:".Length..].Trim());
            }
            else if (trimmed.StartsWith("BehaviorKind:", StringComparison.Ordinal))
            {
                profile.BehaviorKind = (SummonBehaviorKind)ParseInt(trimmed["BehaviorKind:".Length..].Trim());
            }
            else if (trimmed.StartsWith("Eligibility:", StringComparison.Ordinal))
            {
                profile.Eligibility = (CombatantEligibilityFlags)ParseInt(trimmed["Eligibility:".Length..].Trim());
            }
            else if (trimmed.StartsWith("CreditPolicy:", StringComparison.Ordinal))
            {
                profile.CreditPolicy = (CombatCreditFlags)ParseInt(trimmed["CreditPolicy:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaxConcurrentPerSource:", StringComparison.Ordinal))
            {
                profile.MaxConcurrentPerSource = ParseInt(trimmed["MaxConcurrentPerSource:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaxConcurrentPerOwner:", StringComparison.Ordinal))
            {
                profile.MaxConcurrentPerOwner = ParseInt(trimmed["MaxConcurrentPerOwner:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DespawnOnOwnerDeath:", StringComparison.Ordinal))
            {
                profile.DespawnOnOwnerDeath = ParseBool(trimmed["DespawnOnOwnerDeath:".Length..].Trim());
            }
            else if (trimmed.StartsWith("OwnerDeathDespawnDelaySeconds:", StringComparison.Ordinal))
            {
                profile.OwnerDeathDespawnDelaySeconds = ParseFloat(trimmed["OwnerDeathDespawnDelaySeconds:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritOwnerTarget:", StringComparison.Ordinal))
            {
                profile.InheritOwnerTarget = ParseBool(trimmed["InheritOwnerTarget:".Length..].Trim());
            }
            else if (trimmed.StartsWith("IsPersistent:", StringComparison.Ordinal))
            {
                profile.IsPersistent = ParseBool(trimmed["IsPersistent:".Length..].Trim());
            }
            else if (string.Equals(trimmed, "Inheritance:", StringComparison.Ordinal))
            {
                profile.Inheritance = ParseStatInheritanceProfile(lines, ref index, indent);
            }
        }

        return profile;
    }

    private static StatInheritanceProfile ParseStatInheritanceProfile(string[] lines, ref int index, int sectionIndent)
    {
        var profile = StatInheritanceProfile.DefaultOwnedSummon;
        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("OffenseBonusScalar:", StringComparison.Ordinal))
            {
                profile.OffenseBonusScalar = ParseFloat(trimmed["OffenseBonusScalar:".Length..].Trim());
            }
            else if (trimmed.StartsWith("DefenseBonusScalar:", StringComparison.Ordinal))
            {
                profile.DefenseBonusScalar = ParseFloat(trimmed["DefenseBonusScalar:".Length..].Trim());
            }
            else if (trimmed.StartsWith("UtilityBonusScalar:", StringComparison.Ordinal))
            {
                profile.UtilityBonusScalar = ParseFloat(trimmed["UtilityBonusScalar:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritCritChance:", StringComparison.Ordinal))
            {
                profile.InheritCritChance = ParseBool(trimmed["InheritCritChance:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritDodge:", StringComparison.Ordinal))
            {
                profile.InheritDodge = ParseBool(trimmed["InheritDodge:".Length..].Trim());
            }
            else if (trimmed.StartsWith("InheritBlock:", StringComparison.Ordinal))
            {
                profile.InheritBlock = ParseBool(trimmed["InheritBlock:".Length..].Trim());
            }
        }

        return profile;
    }

    private static List<EffectDescriptor> ParseEffectDescriptors(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var result = new List<EffectDescriptor>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                break;
            }

            if (!trimmed.StartsWith("- Layer:", StringComparison.Ordinal))
            {
                continue;
            }

            var effect = new EffectDescriptor
            {
                Layer = (AuthorityLayer)ParseInt(trimmed["- Layer:".Length..].Trim())
            };

            for (index++; index < lines.Length; index++)
            {
                line = lines[index];
                trimmed = line.Trim();
                indent = GetIndent(line);
                if (trimmed.StartsWith("- Layer:", StringComparison.Ordinal)
                    || (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("Scope:", StringComparison.Ordinal))
                {
                    effect.Scope = (EffectScope)ParseInt(trimmed["Scope:".Length..].Trim());
                }
                else if (trimmed.StartsWith("Capabilities:", StringComparison.Ordinal))
                {
                    effect.Capabilities = (EffectCapability)ParseInt(trimmed["Capabilities:".Length..].Trim());
                }
                else if (trimmed.StartsWith("AllowMirroredOwnedSummonKill:", StringComparison.Ordinal))
                {
                    effect.AllowMirroredOwnedSummonKill = ParseBool(trimmed["AllowMirroredOwnedSummonKill:".Length..].Trim());
                }
                else if (trimmed.StartsWith("AllowsPersistentSummonChain:", StringComparison.Ordinal))
                {
                    effect.AllowsPersistentSummonChain = ParseBool(trimmed["AllowsPersistentSummonChain:".Length..].Trim());
                }
                else if (trimmed.StartsWith("LoadoutTopologyDelta:", StringComparison.Ordinal))
                {
                    effect.LoadoutTopologyDelta = ParseInt(trimmed["LoadoutTopologyDelta:".Length..].Trim());
                }
            }

            result.Add(effect);
        }

        return result;
    }

    private static T? ResolveReferenceFromLine<T>(
        string line,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        var guid = ExtractGuid(line);
        return ResolveGuid(guid, guidToPath, definitions);
    }

    private static List<T> ParseReferenceList<T>(
        string[] lines,
        string sectionHeader,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        return ParseGuidList(lines, sectionHeader)
            .Select(guid => ResolveGuid(guid, guidToPath, definitions))
            .Where(asset => asset != null)
            .Select(asset => asset!)
            .ToList();
    }

    private static List<RecruitBannedPairingDefinition> ParseRecruitBannedPairings(string[] lines, string sectionHeader)
    {
        var result = new List<RecruitBannedPairingDefinition>();
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- FlexActiveId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var definition = new RecruitBannedPairingDefinition
            {
                FlexActiveId = trimmed["- FlexActiveId:".Length..].Trim()
            };

            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- FlexActiveId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("FlexPassiveId:", StringComparison.Ordinal))
                {
                    definition.FlexPassiveId = trimmed["FlexPassiveId:".Length..].Trim();
                }
            }

            result.Add(definition);
        }

        return result;
    }

    private static List<StableTagDefinition> ParseStableTagList(
        string[] lines,
        string sectionHeader,
        IReadOnlyDictionary<string, string>? guidToPath = null)
    {
        var tags = LoadStableTags();
        return guidToPath == null
            ? new List<StableTagDefinition>()
            : ParseReferenceList(lines, sectionHeader, guidToPath, tags);
    }

    private static SkillAiScoreHints ParseSkillAiScoreHints(string[] lines, string sectionHeader, int sectionIndent = 2)
    {
        var index = FindLineIndex(lines, sectionHeader);
        var hints = new SkillAiScoreHints();
        if (index < 0)
        {
            return hints;
        }

        for (index++; index < lines.Length; index++)
        {
            var line = lines[index];
            var trimmed = line.Trim();
            var indent = GetIndent(line);
            if (indent <= sectionIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
            {
                index--;
                break;
            }

            if (trimmed.StartsWith("BurstBias:", StringComparison.Ordinal))
            {
                hints.BurstBias = ParseFloat(trimmed["BurstBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ProtectBias:", StringComparison.Ordinal))
            {
                hints.ProtectBias = ParseFloat(trimmed["ProtectBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaintainRangeBias:", StringComparison.Ordinal))
            {
                hints.MaintainRangeBias = ParseFloat(trimmed["MaintainRangeBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ExecuteBias:", StringComparison.Ordinal))
            {
                hints.ExecuteBias = ParseFloat(trimmed["ExecuteBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("ControlBias:", StringComparison.Ordinal))
            {
                hints.ControlBias = ParseFloat(trimmed["ControlBias:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MinimumTargetHealthRatio:", StringComparison.Ordinal))
            {
                hints.MinimumTargetHealthRatio = ParseFloat(trimmed["MinimumTargetHealthRatio:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaximumTargetHealthRatio:", StringComparison.Ordinal))
            {
                hints.MaximumTargetHealthRatio = ParseFloat(trimmed["MaximumTargetHealthRatio:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MinimumDistance:", StringComparison.Ordinal))
            {
                hints.MinimumDistance = ParseFloat(trimmed["MinimumDistance:".Length..].Trim());
            }
            else if (trimmed.StartsWith("MaximumDistance:", StringComparison.Ordinal))
            {
                hints.MaximumDistance = ParseFloat(trimmed["MaximumDistance:".Length..].Trim());
            }
        }

        return hints;
    }

    private static UnitLoadoutDefinition ParseUnitLoadout(
        string[] lines,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        var loadoutLines = ExtractSection(lines, "Loadout:", 2);
        if (loadoutLines.Length == 0)
        {
            return new UnitLoadoutDefinition();
        }

        return new UnitLoadoutDefinition
        {
            BasicAttack = ParseBasicAttackDefinition(loadoutLines),
            SignatureActive = ResolveReferenceFromLine(ExtractLine(loadoutLines, "SignatureActive:"), guidToPath, skills),
            FlexActive = ResolveReferenceFromLine(ExtractLine(loadoutLines, "FlexActive:"), guidToPath, skills),
            SignaturePassive = ParsePassiveDefinition(loadoutLines, "SignaturePassive:"),
            FlexPassive = ParsePassiveDefinition(loadoutLines, "FlexPassive:"),
            MobilityReaction = ParseMobilityDefinition(loadoutLines, "MobilityReaction:")
        };
    }

    private static BasicAttackDefinition ParseBasicAttackDefinition(string[] lines)
    {
        var basicAttackLines = ExtractSection(lines, "BasicAttack:", 4);
        if (basicAttackLines.Length == 0)
        {
            return new BasicAttackDefinition();
        }

        return new BasicAttackDefinition
        {
            Id = ExtractValue(basicAttackLines, "Id:"),
            NameKey = ExtractValue(basicAttackLines, "NameKey:"),
            DamageType = (DamageTypeValue)ExtractInt(basicAttackLines, "DamageType:"),
            Lane = (ActionLane)ExtractInt(basicAttackLines, "Lane:"),
            LockRule = (ActionLockRule)ExtractInt(basicAttackLines, "LockRule:"),
            TargetRule = ParseTargetRule(basicAttackLines, "TargetRule:", 6),
            Effects = ParseEffectDescriptors(basicAttackLines, "Effects:", 6)
        };
    }

    private static PassiveDefinition ParsePassiveDefinition(string[] lines, string sectionHeader)
    {
        var passiveLines = ExtractSection(lines, sectionHeader, 4);
        if (passiveLines.Length == 0)
        {
            return new PassiveDefinition();
        }

        return new PassiveDefinition
        {
            Id = ExtractValue(passiveLines, "Id:"),
            NameKey = ExtractValue(passiveLines, "NameKey:"),
            DescriptionKey = ExtractValue(passiveLines, "DescriptionKey:"),
            EffectFamilyId = ExtractValue(passiveLines, "EffectFamilyId:"),
            MutuallyExclusiveGroupId = ExtractValue(passiveLines, "MutuallyExclusiveGroupId:"),
            BudgetCard = ParseBudgetCard(passiveLines, "BudgetCard:", 6) ?? new BudgetCard { Domain = BudgetDomain.Passive },
            RecruitNativeTags = ParseStableTagList(passiveLines, "RecruitNativeTags:"),
            RecruitPlanTags = ParseStableTagList(passiveLines, "RecruitPlanTags:"),
            RecruitScoutTags = ParseStableTagList(passiveLines, "RecruitScoutTags:"),
            ActivationModel = (ActivationModel)ExtractInt(passiveLines, "ActivationModel:"),
            Lane = (ActionLane)ExtractInt(passiveLines, "Lane:"),
            LockRule = (ActionLockRule)ExtractInt(passiveLines, "LockRule:"),
            Effects = ParseEffectDescriptors(passiveLines, "Effects:", 6),
            AllowMirroredOwnedSummonKill = ExtractBool(passiveLines, "AllowMirroredOwnedSummonKill:")
        };
    }

    private static MobilityDefinition ParseMobilityDefinition(string[] lines, string sectionHeader)
    {
        var mobilityLines = ExtractSection(lines, sectionHeader, 4);
        if (mobilityLines.Length == 0)
        {
            return new MobilityDefinition();
        }

        return new MobilityDefinition
        {
            Id = ExtractValue(mobilityLines, "Id:"),
            NameKey = ExtractValue(mobilityLines, "NameKey:"),
            DescriptionKey = ExtractValue(mobilityLines, "DescriptionKey:"),
            BudgetCard = ParseBudgetCard(mobilityLines, "BudgetCard:", 6) ?? new BudgetCard { Domain = BudgetDomain.Mobility },
            ActivationModel = (ActivationModel)ExtractInt(mobilityLines, "ActivationModel:"),
            Lane = (ActionLane)ExtractInt(mobilityLines, "Lane:"),
            LockRule = (ActionLockRule)ExtractInt(mobilityLines, "LockRule:"),
            TargetRule = ParseTargetRule(mobilityLines, "TargetRule:", 6),
            Effects = ParseEffectDescriptors(mobilityLines, "Effects:", 6)
        };
    }

    private static string[] ExtractSection(string[] lines, string sectionHeader, int sectionIndent)
    {
        var index = FindLineIndex(lines, sectionHeader);
        if (index < 0)
        {
            return Array.Empty<string>();
        }

        var collected = new List<string> { lines[index] };
        for (var lineIndex = index + 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var trimmed = line.Trim();
            if (GetIndent(line) <= sectionIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }

            collected.Add(line);
        }

        return collected.ToArray();
    }

    private static List<string> ParseIndentedStringList(string[] lines, ref int index, int breakIndent)
    {
        var result = new List<string>();
        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= breakIndent && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    index--;
                    break;
                }

                continue;
            }

            var value = trimmed[1..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static void ApplyFallbackIdentity(UnityEngine.Object definition, string assetPath)
    {
        switch (definition)
        {
            case StatDefinition stat:
                stat.Id = CoalesceId(stat.Id, DeriveId(assetPath, "stat_"));
                stat.NameKey = Coalesce(stat.NameKey, ContentLocalizationTables.BuildStatNameKey(stat.Id));
                stat.DescriptionKey = Coalesce(stat.DescriptionKey, ContentLocalizationTables.BuildStatDescriptionKey(stat.Id));
                break;
            case RaceDefinition race:
                race.Id = CoalesceId(race.Id, DeriveId(assetPath, "race_"));
                race.NameKey = Coalesce(race.NameKey, ContentLocalizationTables.BuildRaceNameKey(race.Id));
                race.DescriptionKey = Coalesce(race.DescriptionKey, ContentLocalizationTables.BuildRaceDescriptionKey(race.Id));
                break;
            case ClassDefinition @class:
                @class.Id = CoalesceId(@class.Id, DeriveId(assetPath, "class_"));
                @class.NameKey = Coalesce(@class.NameKey, ContentLocalizationTables.BuildClassNameKey(@class.Id));
                @class.DescriptionKey = Coalesce(@class.DescriptionKey, ContentLocalizationTables.BuildClassDescriptionKey(@class.Id));
                break;
            case SkillDefinitionAsset skill:
                skill.Id = CoalesceId(skill.Id, DeriveId(assetPath));
                skill.NameKey = Coalesce(skill.NameKey, ContentLocalizationTables.BuildSkillNameKey(skill.Id));
                skill.DescriptionKey = Coalesce(skill.DescriptionKey, ContentLocalizationTables.BuildSkillDescriptionKey(skill.Id));
                break;
            case TraitPoolDefinition traitPool:
                traitPool.Id = CoalesceId(traitPool.Id, DeriveId(assetPath));
                traitPool.ArchetypeId = CoalesceId(traitPool.ArchetypeId, DeriveId(assetPath, "traitpool_"));
                break;
            case ItemBaseDefinition item:
                item.Id = CoalesceId(item.Id, DeriveId(assetPath));
                item.NameKey = Coalesce(item.NameKey, ContentLocalizationTables.BuildItemNameKey(item.Id));
                item.DescriptionKey = Coalesce(item.DescriptionKey, ContentLocalizationTables.BuildItemDescriptionKey(item.Id));
                break;
            case AffixDefinition affix:
                affix.Id = CoalesceId(affix.Id, DeriveId(assetPath));
                affix.NameKey = Coalesce(affix.NameKey, ContentLocalizationTables.BuildAffixNameKey(affix.Id));
                affix.DescriptionKey = Coalesce(affix.DescriptionKey, ContentLocalizationTables.BuildAffixDescriptionKey(affix.Id));
                break;
            case AugmentDefinition augment:
                augment.Id = CoalesceId(augment.Id, DeriveId(assetPath));
                augment.NameKey = Coalesce(augment.NameKey, ContentLocalizationTables.BuildAugmentNameKey(augment.Id));
                augment.DescriptionKey = Coalesce(augment.DescriptionKey, ContentLocalizationTables.BuildAugmentDescriptionKey(augment.Id));
                break;
            case StableTagDefinition stableTag:
                stableTag.Id = CoalesceId(stableTag.Id, DeriveId(assetPath, "tag_"));
                stableTag.NameKey = Coalesce(stableTag.NameKey, $"content.tag.{ContentLocalizationTables.NormalizeId(stableTag.Id)}.name");
                break;
            case TeamTacticDefinition teamTactic:
                teamTactic.Id = CoalesceId(teamTactic.Id, DeriveId(assetPath));
                teamTactic.NameKey = Coalesce(teamTactic.NameKey, ContentLocalizationTables.BuildTeamTacticNameKey(teamTactic.Id));
                break;
            case RoleInstructionDefinition roleInstruction:
                roleInstruction.Id = CoalesceId(roleInstruction.Id, DeriveId(assetPath));
                roleInstruction.NameKey = Coalesce(roleInstruction.NameKey, ContentLocalizationTables.BuildRoleNameKey(roleInstruction.Id));
                roleInstruction.RoleTag = Coalesce(roleInstruction.RoleTag, roleInstruction.Id);
                break;
            case PassiveBoardDefinition passiveBoard:
                passiveBoard.Id = CoalesceId(passiveBoard.Id, DeriveId(assetPath));
                passiveBoard.NameKey = Coalesce(passiveBoard.NameKey, ContentLocalizationTables.BuildPassiveBoardNameKey(passiveBoard.Id));
                passiveBoard.DescriptionKey = Coalesce(passiveBoard.DescriptionKey, ContentLocalizationTables.BuildPassiveBoardDescriptionKey(passiveBoard.Id));
                break;
            case PassiveNodeDefinition passiveNode:
                passiveNode.Id = CoalesceId(passiveNode.Id, DeriveId(assetPath));
                passiveNode.NameKey = Coalesce(passiveNode.NameKey, ContentLocalizationTables.BuildPassiveNodeNameKey(passiveNode.Id));
                passiveNode.DescriptionKey = Coalesce(passiveNode.DescriptionKey, ContentLocalizationTables.BuildPassiveNodeDescriptionKey(passiveNode.Id));
                break;
            case SynergyDefinition synergy:
                synergy.Id = CoalesceId(synergy.Id, DeriveId(assetPath));
                synergy.NameKey = Coalesce(synergy.NameKey, ContentLocalizationTables.BuildSynergyNameKey(synergy.Id));
                synergy.DescriptionKey = Coalesce(synergy.DescriptionKey, ContentLocalizationTables.BuildSynergyDescriptionKey(synergy.Id));
                break;
            case SynergyTierDefinition synergyTier:
                synergyTier.Id = CoalesceId(synergyTier.Id, DeriveId(assetPath));
                synergyTier.NameKey = Coalesce(synergyTier.NameKey, ContentLocalizationTables.BuildSynergyNameKey(synergyTier.Id));
                synergyTier.DescriptionKey = Coalesce(synergyTier.DescriptionKey, ContentLocalizationTables.BuildSynergyDescriptionKey(synergyTier.Id));
                break;
            case ExpeditionDefinition expedition:
                expedition.Id = CoalesceId(expedition.Id, DeriveId(assetPath));
                expedition.NameKey = Coalesce(expedition.NameKey, ContentLocalizationTables.BuildExpeditionNameKey(expedition.Id));
                expedition.DescriptionKey = Coalesce(expedition.DescriptionKey, ContentLocalizationTables.BuildExpeditionDescriptionKey(expedition.Id));
                break;
            case RewardTableDefinition rewardTable:
                rewardTable.Id = CoalesceId(rewardTable.Id, DeriveId(assetPath));
                rewardTable.NameKey = Coalesce(rewardTable.NameKey, ContentLocalizationTables.BuildRewardTableNameKey(rewardTable.Id));
                rewardTable.DescriptionKey = Coalesce(rewardTable.DescriptionKey, $"content.reward_table.{ContentLocalizationTables.NormalizeId(rewardTable.Id)}.desc");
                break;
            case UnitArchetypeDefinition archetype:
                archetype.Id = CoalesceId(archetype.Id, DeriveId(assetPath, "archetype_"));
                archetype.NameKey = Coalesce(archetype.NameKey, ContentLocalizationTables.BuildArchetypeNameKey(archetype.Id));
                break;
            case CampaignChapterDefinition chapter:
                chapter.Id = CoalesceId(chapter.Id, DeriveId(assetPath));
                chapter.NameKey = Coalesce(chapter.NameKey, ContentLocalizationTables.BuildCampaignChapterNameKey(chapter.Id));
                chapter.DescriptionKey = Coalesce(chapter.DescriptionKey, ContentLocalizationTables.BuildCampaignChapterDescriptionKey(chapter.Id));
                break;
            case ExpeditionSiteDefinition site:
                site.Id = CoalesceId(site.Id, DeriveId(assetPath));
                site.NameKey = Coalesce(site.NameKey, ContentLocalizationTables.BuildExpeditionSiteNameKey(site.Id));
                site.DescriptionKey = Coalesce(site.DescriptionKey, ContentLocalizationTables.BuildExpeditionSiteDescriptionKey(site.Id));
                break;
            case EncounterDefinition encounter:
                encounter.Id = CoalesceId(encounter.Id, DeriveId(assetPath));
                encounter.NameKey = Coalesce(encounter.NameKey, ContentLocalizationTables.BuildEncounterNameKey(encounter.Id));
                encounter.DescriptionKey = Coalesce(encounter.DescriptionKey, ContentLocalizationTables.BuildEncounterDescriptionKey(encounter.Id));
                break;
            case EnemySquadTemplateDefinition squad:
                squad.Id = CoalesceId(squad.Id, DeriveId(assetPath));
                squad.NameKey = Coalesce(squad.NameKey, ContentLocalizationTables.BuildEnemySquadNameKey(squad.Id));
                squad.DescriptionKey = Coalesce(squad.DescriptionKey, ContentLocalizationTables.BuildEnemySquadDescriptionKey(squad.Id));
                break;
            case BossOverlayDefinition overlay:
                overlay.Id = CoalesceId(overlay.Id, DeriveId(assetPath));
                overlay.NameKey = Coalesce(overlay.NameKey, ContentLocalizationTables.BuildBossOverlayNameKey(overlay.Id));
                overlay.DescriptionKey = Coalesce(overlay.DescriptionKey, ContentLocalizationTables.BuildBossOverlayDescriptionKey(overlay.Id));
                break;
            case StatusFamilyDefinition statusFamily:
                statusFamily.Id = CoalesceId(statusFamily.Id, DeriveId(assetPath, "status_family_"));
                statusFamily.NameKey = Coalesce(statusFamily.NameKey, ContentLocalizationTables.BuildStatusNameKey(statusFamily.Id));
                statusFamily.DescriptionKey = Coalesce(statusFamily.DescriptionKey, ContentLocalizationTables.BuildStatusDescriptionKey(statusFamily.Id));
                break;
            case CleanseProfileDefinition cleanseProfile:
                cleanseProfile.Id = CoalesceId(cleanseProfile.Id, DeriveId(assetPath, "cleanse_profile_"));
                cleanseProfile.NameKey = Coalesce(cleanseProfile.NameKey, ContentLocalizationTables.BuildCleanseProfileNameKey(cleanseProfile.Id));
                cleanseProfile.DescriptionKey = Coalesce(cleanseProfile.DescriptionKey, ContentLocalizationTables.BuildCleanseProfileDescriptionKey(cleanseProfile.Id));
                break;
            case ControlDiminishingRuleDefinition controlRule:
                controlRule.Id = CoalesceId(controlRule.Id, DeriveId(assetPath));
                controlRule.NameKey = Coalesce(controlRule.NameKey, ContentLocalizationTables.BuildControlDiminishingNameKey(controlRule.Id));
                controlRule.DescriptionKey = Coalesce(controlRule.DescriptionKey, ContentLocalizationTables.BuildControlDiminishingDescriptionKey(controlRule.Id));
                break;
            case RewardSourceDefinition rewardSource:
                rewardSource.Id = CoalesceId(rewardSource.Id, DeriveId(assetPath));
                rewardSource.NameKey = Coalesce(rewardSource.NameKey, ContentLocalizationTables.BuildRewardSourceNameKey(rewardSource.Id));
                rewardSource.DescriptionKey = Coalesce(rewardSource.DescriptionKey, ContentLocalizationTables.BuildRewardSourceDescriptionKey(rewardSource.Id));
                break;
            case DropTableDefinition dropTable:
                dropTable.Id = CoalesceId(dropTable.Id, DeriveId(assetPath));
                dropTable.NameKey = Coalesce(dropTable.NameKey, ContentLocalizationTables.BuildDropTableNameKey(dropTable.Id));
                dropTable.DescriptionKey = Coalesce(dropTable.DescriptionKey, ContentLocalizationTables.BuildDropTableDescriptionKey(dropTable.Id));
                break;
            case LootBundleDefinition lootBundle:
                lootBundle.Id = CoalesceId(lootBundle.Id, DeriveId(assetPath));
                lootBundle.NameKey = Coalesce(lootBundle.NameKey, ContentLocalizationTables.BuildLootBundleNameKey(lootBundle.Id));
                lootBundle.DescriptionKey = Coalesce(lootBundle.DescriptionKey, ContentLocalizationTables.BuildLootBundleDescriptionKey(lootBundle.Id));
                break;
            case TraitTokenDefinition traitToken:
                traitToken.Id = CoalesceId(traitToken.Id, DeriveId(assetPath));
                traitToken.NameKey = Coalesce(traitToken.NameKey, ContentLocalizationTables.BuildTraitTokenNameKey(traitToken.Id));
                traitToken.DescriptionKey = Coalesce(traitToken.DescriptionKey, ContentLocalizationTables.BuildTraitTokenDescriptionKey(traitToken.Id));
                break;
        }
    }

    private static string DeriveId(string assetPath, string prefix = "")
    {
        var fileName = Path.GetFileNameWithoutExtension(assetPath);
        if (!string.IsNullOrWhiteSpace(prefix) && fileName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return fileName[prefix.Length..];
        }

        return fileName;
    }

    private static string CoalesceId(string current, string fallback)
    {
        return string.IsNullOrWhiteSpace(current) ? fallback : current;
    }

    private static string Coalesce(string current, string fallback)
    {
        return string.IsNullOrWhiteSpace(current) || current.Contains(".unknown.", StringComparison.Ordinal)
            ? fallback
            : current;
    }

    private static void ApplySkillFallbacks(SkillDefinitionAsset definition)
    {
        definition.AuthorityLayer = AuthorityLayer.Skill;
        if (definition.SlotKind == 0)
        {
            definition.SlotKind = string.Equals(definition.Id, "skill_minor_heal", StringComparison.Ordinal)
                ? SkillSlotKindValue.UtilityActive
                : definition.Id.StartsWith("support_", StringComparison.Ordinal)
                ? SkillSlotKindValue.Support
                : definition.Id.Contains("_passive_", StringComparison.Ordinal)
                    ? SkillSlotKindValue.Passive
                    : definition.Id.Contains("_utility", StringComparison.Ordinal)
                        ? SkillSlotKindValue.UtilityActive
                        : SkillSlotKindValue.CoreActive;
        }

        if (!string.IsNullOrWhiteSpace(definition.CleanseProfileId))
        {
            var tags = LoadStableTags();
            if (tags.TryGetValue("cleanse", out var cleanseTag)
                && definition.CompileTags.All(tag => tag == null || !string.Equals(tag.Id, cleanseTag.Id, StringComparison.Ordinal)))
            {
                definition.CompileTags.Add(cleanseTag);
            }
        }

        if (definition.Id.StartsWith("support_", StringComparison.Ordinal) && definition.SupportAllowedTags.Count == 0)
        {
            var tags = LoadStableTags();
            var preferredTagId = definition.Id switch
            {
                "support_guarded" or "support_anchored" => "frontline",
                "support_longshot" or "support_hunter_mark" or "support_piercing" => "backline",
                _ => "support",
            };
            if (tags.TryGetValue(preferredTagId, out var allowedTag))
            {
                definition.SupportAllowedTags.Add(allowedTag);
            }
        }

        if (definition.BudgetCard == null || definition.BudgetCard.Vector == null || definition.BudgetCard.Vector.FinalScore == 0)
        {
            var band = definition.SlotKind switch
            {
                SkillSlotKindValue.UtilityActive when string.Equals(definition.Id, "skill_minor_heal", StringComparison.Ordinal) => PowerBand.Standard,
                SkillSlotKindValue.CoreActive => PowerBand.Standard,
                _ => PowerBand.Minor,
            };
            var target = LoopCContentGovernance.PowerBandTargets[band].Target;
            var counters = ResolveSkillCounters(definition);
            var vector = definition.Kind switch
            {
                SkillKindValue.Heal => MakeBudgetVector(0, 0, 0, 0, 0, target / 2, counters.Length > 0 ? 1 : 0, target / 2 - (counters.Length > 0 ? 1 : 0)),
                _ when definition.DamageType == DamageTypeValue.Magical => MakeBudgetVector(target / 4, target / 3, 0, Math.Max(1, target / 4), 0, 0, counters.Length > 0 ? 1 : 0, Math.Max(1, target - (target / 4 + target / 3 + Math.Max(1, target / 4) + (counters.Length > 0 ? 1 : 0)))),
                _ when definition.Delivery == SkillDeliveryValue.Projectile || definition.Delivery == SkillDeliveryValue.Ranged => MakeBudgetVector(target / 2, target / 6, 0, 0, 1, 0, counters.Length > 0 ? 1 : 0, target - (target / 2 + target / 6 + 1 + (counters.Length > 0 ? 1 : 0))),
                _ => MakeBudgetVector(target / 3, target / 3, 0, 0, Math.Max(1, target / 6), 0, counters.Length > 0 ? 1 : 0, Math.Max(1, target - (target / 3 + target / 3 + Math.Max(1, target / 6) + (counters.Length > 0 ? 1 : 0)))),
            };
            AdjustBudgetFinalScore(vector, target);
            definition.BudgetCard = BuildBudgetCard(BudgetDomain.Skill, ContentRarity.Common, band, null, vector, 2, 1, 0, Array.Empty<ThreatPattern>(), counters);
        }
        else if (definition.BudgetCard.Rarity == ContentRarity.Common)
        {
            definition.BudgetCard.KeywordCount = Math.Min(definition.BudgetCard.KeywordCount, 2);
            definition.BudgetCard.ConditionClauseCount = Math.Min(definition.BudgetCard.ConditionClauseCount, 1);
            definition.BudgetCard.RuleExceptionCount = 0;
        }
    }

    private static void ApplyAffixFallbacks(AffixDefinition definition)
    {
        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            return;
        }

        var vector = definition.Category switch
        {
            AffixCategoryValue.OffenseFlat => MakeBudgetVector(3, 2, 0, 0, 0, 0, 0, 1),
            AffixCategoryValue.DefenseFlat => MakeBudgetVector(0, 0, 5, 0, 0, 0, 0, 1),
            AffixCategoryValue.DefenseScaling => MakeBudgetVector(0, 0, 4, 0, 0, 1, 0, 1),
            _ => MakeBudgetVector(0, 0, 1, 0, 1, 3, 0, 1),
        };
        AdjustBudgetFinalScore(vector, LoopCContentGovernance.AffixBudgetTargets[ContentRarity.Common].Target);
        definition.BudgetCard = BuildBudgetCard(
            BudgetDomain.Affix,
            ContentRarity.Common,
            PowerBand.Standard,
            null,
            vector,
            1,
            0,
            0,
            Array.Empty<ThreatPattern>(),
            Array.Empty<CounterToolContribution>());
    }

    private static void ApplyAugmentFallbacks(AugmentDefinition definition)
    {
        definition.FamilyId = Coalesce(definition.FamilyId, definition.Id);
        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            return;
        }

        var target = LoopCContentGovernance.AugmentBudgetTargets[definition.Rarity].Target;
        var band = definition.Rarity switch
        {
            ContentRarity.Common => PowerBand.Major,
            ContentRarity.Rare => PowerBand.Signature,
            _ => PowerBand.Keystone,
        };
        var statId = definition.Modifiers.FirstOrDefault()?.StatId ?? string.Empty;
        var vector = statId switch
        {
            "armor" or "max_health" or "resist" => MakeBudgetVector(0, 0, target / 2, 0, 0, target / 6, 0, target / 3),
            "heal_power" => MakeBudgetVector(0, 0, target / 6, 0, 0, target / 2, 0, target / 3),
            "attack_speed" => MakeBudgetVector(target / 3, target / 6, 0, 0, target / 6, 0, 0, target / 3),
            _ => MakeBudgetVector(target / 3, target / 3, 0, 0, 0, 0, 0, target / 3),
        };
        AdjustBudgetFinalScore(vector, target);
        definition.BudgetCard = BuildBudgetCard(
            BudgetDomain.Augment,
            definition.Rarity,
            band,
            null,
            vector,
            definition.Rarity == ContentRarity.Common ? 2 : definition.Rarity == ContentRarity.Rare ? 3 : 4,
            definition.Rarity == ContentRarity.Common ? 1 : 2,
            definition.Rarity == ContentRarity.Epic ? 1 : 0,
            Array.Empty<ThreatPattern>(),
            Array.Empty<CounterToolContribution>());
    }

    private static void ApplyStatusFamilyFallbacks(StatusFamilyDefinition definition)
    {
        definition.DefaultStackCap = Math.Max(definition.DefaultStackCap, 1);
        definition.VisualPriority = Math.Max(definition.VisualPriority, 0);
        definition.IsAiRelevant = true;
        definition.Group = definition.Id switch
        {
            "bleed" or "burn" or "wound" => StatusGroupValue.Attrition,
            "marked" or "exposed" or "sunder" => StatusGroupValue.TacticalMark,
            "barrier" or "guarded" or "unstoppable" => StatusGroupValue.DefensiveBoon,
            _ => definition.Group,
        };
        definition.IsHardControl = definition.Id is "root" or "silence" or "stun";
        definition.UsesControlDiminishing = definition.IsHardControl;

        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            return;
        }

        var isMinor = definition.IsHardControl || string.Equals(definition.Id, "root", StringComparison.Ordinal) || string.Equals(definition.Id, "silence", StringComparison.Ordinal);
        var band = isMinor ? PowerBand.Minor : PowerBand.Micro;
        var counters = definition.Id switch
        {
            "sunder" => new[] { MakeCounter(CounterTool.ArmorShred, CounterCoverageStrength.Standard) },
            "exposed" => new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Standard) },
            "wound" => new[] { MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Standard) },
            "unstoppable" => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard) },
            _ => Array.Empty<CounterToolContribution>(),
        };
        var threats = definition.Id switch
        {
            "guarded" => new[] { ThreatPattern.GuardBulwark },
            "barrier" => new[] { ThreatPattern.SustainBall },
            "marked" => new[] { ThreatPattern.DiveBackline },
            _ => Array.Empty<ThreatPattern>(),
        };
        var vector = definition.Group switch
        {
            StatusGroupValue.Control => MakeBudgetVector(0, 0, 0, isMinor ? 6 : 3, 0, 0, counters.Length > 0 ? 2 : 0, 0),
            StatusGroupValue.Attrition => MakeBudgetVector(4, 0, 0, 0, 0, 0, counters.Length > 0 ? 2 : 0, 0),
            StatusGroupValue.TacticalMark => MakeBudgetVector(0, 0, 0, 1, 0, 1, counters.Length > 0 ? 3 : 0, 0),
            StatusGroupValue.DefensiveBoon => MakeBudgetVector(0, 0, 2, 0, 0, 2, counters.Length > 0 ? 2 : 0, 0),
            _ => MakeBudgetVector(0, 0, 0, 0, 0, 0, 0, 0),
        };
        AdjustBudgetFinalScore(vector, LoopCContentGovernance.PowerBandTargets[band].Target);
        definition.BudgetCard = BuildBudgetCard(BudgetDomain.Status, ContentRarity.Common, band, null, vector, isMinor ? 2 : 1, 0, 0, threats, counters);
    }

    private static void ApplyCleanseProfileFallbacks(CleanseProfileDefinition definition)
    {
        if (definition.RemovesStatusIds.Count > 0 || definition.GrantsUnstoppable || definition.RemovesOneHardControl)
        {
            return;
        }

        switch (definition.Id)
        {
            case "cleanse_basic":
                definition.RemovesStatusIds = new List<string> { "slow", "root" };
                break;
            case "cleanse_control":
                definition.RemovesStatusIds = new List<string> { "stun", "silence", "root" };
                definition.RemovesOneHardControl = true;
                break;
            case "break_and_unstoppable":
                definition.RemovesStatusIds = new List<string> { "slow", "root", "stun" };
                definition.RemovesOneHardControl = true;
                definition.GrantsUnstoppable = true;
                definition.GrantedUnstoppableDurationSeconds = 1.5f;
                break;
        }
    }

    private static void ApplyControlRuleFallbacks(ControlDiminishingRuleDefinition definition)
    {
        if (definition.WindowSeconds <= 0f)
        {
            definition.WindowSeconds = 1.5f;
        }

        if (definition.ControlResistMultiplier <= 0f)
        {
            definition.ControlResistMultiplier = 0.5f;
        }
    }

    private static void ApplyTraitTokenFallbacks(TraitTokenDefinition definition)
    {
        definition.RewardType = definition.Id switch
        {
            "trait_lock_token" => RewardType.TraitLockToken,
            "trait_purge_token" => RewardType.TraitPurgeToken,
            "trait_reroll_token" => RewardType.TraitRerollCurrency,
            _ => definition.RewardType,
        };
    }

    private static void ApplyRewardTableFallbacks(RewardTableDefinition definition)
    {
        if (definition.Rewards.Count > 0)
        {
            return;
        }

        var rewardId = string.Equals(definition.Id, "rewardtable_expedition_end", StringComparison.Ordinal)
            ? "reward.gold.30"
            : "reward.gold.10";
        var amount = string.Equals(rewardId, "reward.gold.30", StringComparison.Ordinal) ? 30 : 10;
        definition.Rewards = new List<RewardEntry>
        {
            new() { Id = rewardId, LabelKey = ContentLocalizationTables.BuildRewardLabelKey(rewardId), RewardType = RewardType.Gold, Amount = amount },
        };
    }

    private static void ApplyRewardSourceFallbacks(RewardSourceDefinition definition)
    {
        definition.DropTableId = Coalesce(definition.DropTableId, definition.Id.Replace("reward_source_", "drop_table_", StringComparison.Ordinal));
    }

    private static void ApplyDropTableFallbacks(DropTableDefinition definition)
    {
        definition.RewardSourceId = Coalesce(definition.RewardSourceId, definition.Id.Replace("drop_table_", "reward_source_", StringComparison.Ordinal));
    }

    private static void ApplyCampaignChapterFallbacks(CampaignChapterDefinition definition)
    {
        if (definition.StoryOrder <= 0)
        {
            definition.StoryOrder = definition.Id switch
            {
                "chapter_ashen_frontier" => 1,
                "chapter_ruined_crypts" => 2,
                "chapter_warren_depths" => 3,
                _ => 1,
            };
        }

        if (definition.SiteIds.Distinct(StringComparer.Ordinal).Count() != 2)
        {
            definition.SiteIds = definition.Id switch
            {
                "chapter_ashen_frontier" => new List<string> { "site_ashen_gate", "site_cinder_watch" },
                "chapter_warren_depths" => new List<string> { "site_forgotten_warren", "site_twisted_den" },
                "chapter_ruined_crypts" => new List<string> { "site_ruined_crypt", "site_grave_sanctum" },
                _ => definition.SiteIds,
            };
        }

        if (definition.StoryOrder == 3)
        {
            definition.UnlocksEndlessOnClear = true;
        }
    }

    private static void ApplyExpeditionSiteFallbacks(ExpeditionSiteDefinition definition)
    {
        if (TryGetCampaignSiteFallbackSpec(definition.Id, out var spec))
        {
            definition.ChapterId = CoalesceId(definition.ChapterId, spec.ChapterId);
            definition.FactionId = Coalesce(definition.FactionId, spec.FactionId);
            if (definition.EncounterIds.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                definition.EncounterIds = BuildSiteEncounterIds(spec.SiteId);
            }

            definition.ExtractRewardSourceId = Coalesce(definition.ExtractRewardSourceId, "reward_source_extract");
            definition.ThreatTier = definition.ThreatTier == 0 ? spec.ThreatTier : definition.ThreatTier;
        }

        definition.SiteOrder = Math.Max(definition.SiteOrder, definition.Id switch
        {
            "site_ashen_gate" => 1,
            "site_cinder_watch" => 2,
            "site_forgotten_warren" => 3,
            "site_grave_sanctum" => 4,
            "site_ruined_crypt" => 5,
            "site_twisted_den" => 6,
            _ => 1,
        });
    }

    private static void ApplyEncounterFallbacks(EncounterDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.SiteId) && TryGetSiteIdFromEncounterId(definition.Id, out var siteId))
        {
            definition.SiteId = siteId;
        }

        if (string.IsNullOrWhiteSpace(definition.RewardSourceId))
        {
            definition.RewardSourceId = definition.Id.Contains("_boss_", StringComparison.Ordinal)
                ? "reward_source_boss"
                : definition.Id.Contains("_elite_", StringComparison.Ordinal)
                    ? "reward_source_elite"
                    : "reward_source_skirmish";
        }

        if (TryGetEncounterKindFromId(definition.Id, out var kind))
        {
            definition.Kind = definition.Kind == 0 && !definition.Id.Contains("_skirmish_", StringComparison.Ordinal) ? kind : definition.Kind;
            definition.DifficultyBand = Coalesce(definition.DifficultyBand, kind switch
            {
                EncounterKindValue.Boss => "site_boss",
                EncounterKindValue.Elite => "site_mid",
                _ => "chapter_entry",
            });

            if (definition.ThreatCost <= 0)
            {
                definition.ThreatCost = kind switch
                {
                    EncounterKindValue.Boss => 3,
                    EncounterKindValue.Elite => 2,
                    _ => 1,
                };
            }

            if (definition.ThreatSkulls <= 0)
            {
                definition.ThreatSkulls = definition.ThreatCost;
            }

            if (definition.ThreatTier == 0)
            {
                definition.ThreatTier = kind switch
                {
                    EncounterKindValue.Boss => ThreatTierValue.Tier3,
                    EncounterKindValue.Elite => ThreatTierValue.Tier2,
                    _ => ThreatTierValue.Tier1,
                };
            }

            definition.EnemySquadTemplateId = CoalesceId(definition.EnemySquadTemplateId, $"{definition.Id}_squad");
            if (definition.RewardDropTags.Count == 0)
            {
                definition.RewardDropTags = kind switch
                {
                    EncounterKindValue.Boss => new List<string> { "boss", "combat", "reward" },
                    EncounterKindValue.Elite => new List<string> { "elite", "combat", "reward" },
                    _ => new List<string> { "skirmish", "combat", "reward" },
                };
            }
        }

        if (TryGetCampaignSiteFallbackSpec(definition.SiteId, out var spec))
        {
            definition.FactionId = Coalesce(definition.FactionId, spec.FactionId);
            if (definition.Kind == EncounterKindValue.Boss)
            {
                definition.BossOverlayId = CoalesceId(definition.BossOverlayId, spec.BossOverlayId);
            }
        }
    }

    private static void ApplyEnemySquadFallbacks(EnemySquadTemplateDefinition definition)
    {
        if (!TryGetSiteIdFromSquadId(definition.Id, out var siteId) || !TryGetCampaignSiteFallbackSpec(siteId, out var spec))
        {
            return;
        }

        definition.FactionId = Coalesce(definition.FactionId, spec.FactionId);
        if (definition.RewardDropTags.Count == 0)
        {
            definition.RewardDropTags = definition.Id.Contains("_boss_", StringComparison.Ordinal)
                ? new List<string> { "boss", "combat", "reward" }
                : definition.Id.Contains("_elite_", StringComparison.Ordinal)
                    ? new List<string> { "elite", "combat", "reward" }
                    : new List<string> { "skirmish", "combat", "reward" };
        }

        if (definition.Members.Count == 0)
        {
            definition.Members = BuildEnemySquadMembers(spec, definition.Id);
        }
    }

    private static void ApplyLootBundleFallbacks(LootBundleDefinition definition)
    {
        definition.RewardSourceId = Coalesce(definition.RewardSourceId, "reward_source_extract");
    }

    private static void ApplyTraitPoolFallbacks(TraitPoolDefinition definition)
    {
        definition.ArchetypeId = CoalesceId(definition.ArchetypeId, definition.Id.Replace("traitpool_", string.Empty, StringComparison.Ordinal));
        if (definition.PositiveTraits.Count == 3 && definition.NegativeTraits.Count == 3)
        {
            return;
        }

        definition.PositiveTraits = new List<TraitEntry>
        {
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_positive_brave", "phys_power", 2f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_positive_sturdy", "armor", 1f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_positive_swift", "attack_speed", 1f),
        };
        definition.NegativeTraits = new List<TraitEntry>
        {
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_negative_frail", "max_health", -3f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_negative_clumsy", "phys_power", -1f),
            BuildFallbackTrait(definition.ArchetypeId, $"{definition.ArchetypeId}_negative_slow", "attack_speed", -1f),
        };
    }

    private static void ApplySynergyTierFallbacks(SynergyTierDefinition definition, string assetPath)
    {
        if (definition.Threshold <= 0)
        {
            var fileName = Path.GetFileNameWithoutExtension(assetPath);
            var lastUnderscore = fileName.LastIndexOf('_');
            if (lastUnderscore >= 0 && int.TryParse(fileName[(lastUnderscore + 1)..], out var threshold))
            {
                definition.Threshold = threshold;
            }
        }

        if (definition.Threshold == 3)
        {
            return;
        }

        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            if (definition.Threshold == 4)
            {
                definition.BudgetCard.PowerBand = PowerBand.Major;
            }

            return;
        }

        var target = definition.Threshold == 4 ? 18 : 12;
        var vector = definition.Threshold == 4
            ? MakeBudgetVector(6, 2, 4, 2, 0, 2, 0, 2)
            : MakeBudgetVector(3, 1, 3, 1, 0, 2, 0, 2);
        AdjustBudgetFinalScore(vector, target);
        definition.BudgetCard = BuildBudgetCard(BudgetDomain.SynergyBreakpoint, ContentRarity.Common, definition.Threshold == 4 ? PowerBand.Major : PowerBand.Standard, null, vector, 2, 1, 0, Array.Empty<ThreatPattern>(), Array.Empty<CounterToolContribution>());
    }

    private static void ApplySynergyFallbacks(SynergyDefinition definition)
    {
        definition.CountedTagId = Coalesce(definition.CountedTagId, definition.Id.Replace("synergy_", string.Empty, StringComparison.Ordinal));
        definition.Tiers = definition.Tiers
            .Where(tier => tier != null && (tier.Threshold == 2 || tier.Threshold == 4))
            .OrderBy(tier => tier.Threshold)
            .ToList();
    }

    private static void ApplyArchetypeFallbacks(
        UnitArchetypeDefinition definition,
        IReadOnlyDictionary<string, RaceDefinition> races,
        IReadOnlyDictionary<string, ClassDefinition> classes,
        IReadOnlyDictionary<string, TraitPoolDefinition> traitPools,
        IReadOnlyDictionary<string, SkillDefinitionAsset> skills)
    {
        var spec = GetArchetypeFallbackSpec(definition.Id);
        if (spec == null)
        {
            return;
        }

        if (definition.Race == null && races.TryGetValue(spec.RaceId, out var race))
        {
            definition.Race = race;
        }

        if (definition.Class == null && classes.TryGetValue(spec.ClassId, out var @class))
        {
            definition.Class = @class;
        }

        if (definition.TraitPool == null && traitPools.TryGetValue(spec.TraitPoolId, out var traitPool))
        {
            definition.TraitPool = traitPool;
        }

        definition.ScopeKind = ArchetypeScopeValue.Core;
        definition.RoleFamilyTag = Coalesce(definition.RoleFamilyTag, spec.RoleFamilyTag);
        definition.PrimaryWeaponFamilyTag = Coalesce(definition.PrimaryWeaponFamilyTag, spec.WeaponFamilyTag);
        definition.RoleTag = Coalesce(definition.RoleTag is "auto" ? string.Empty : definition.RoleTag, spec.RoleTag);
        definition.RecruitTier = spec.RecruitTier;

        if (definition.Skills == null || definition.Skills.Count == 0)
        {
            definition.Skills = spec.DefaultSkillIds
                .Where(skills.ContainsKey)
                .Select(id => skills[id])
                .ToList();
        }

        if (definition.Loadout != null)
        {
            definition.Loadout.SignatureActive ??= definition.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.CoreActive);
            definition.Loadout.FlexActive ??= definition.Skills.FirstOrDefault(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive);
        }

        if (definition.RecruitFlexActivePool == null || definition.RecruitFlexActivePool.Count == 0)
        {
            definition.RecruitFlexActivePool = definition.Skills
                .Where(skill => skill != null && skill.SlotKind == SkillSlotKindValue.UtilityActive)
                .ToList();
        }

        if (definition.RecruitFlexPassivePool == null || definition.RecruitFlexPassivePool.Count == 0)
        {
            definition.RecruitFlexPassivePool = definition.Skills
                .Where(skill => skill != null && skill.SlotKind == SkillSlotKindValue.Support)
                .ToList();
        }

        if (definition.TacticPreset == null || definition.TacticPreset.Count == 0)
        {
            var tacticSkill = definition.Skills.FirstOrDefault(skill => skill != null);
            if (tacticSkill != null)
            {
                definition.TacticPreset = BuildFallbackTacticPreset(spec.ClassId, tacticSkill);
            }
        }

        if (definition.BudgetCard == null || definition.BudgetCard.Vector == null || definition.BudgetCard.Vector.FinalScore == 0)
        {
            var rarity = LoopCContentGovernance.FromRecruitTier(definition.RecruitTier);
            var roleProfile = spec.ClassId switch
            {
                "vanguard" => CombatRoleBudgetProfile.Vanguard,
                "ranger" => CombatRoleBudgetProfile.Ranger,
                "mystic" when string.Equals(definition.Id, "priest", StringComparison.Ordinal) => CombatRoleBudgetProfile.Support,
                "mystic" => CombatRoleBudgetProfile.Arcanist,
                "duelist" when string.Equals(definition.Id, "raider", StringComparison.Ordinal) || string.Equals(definition.Id, "reaver", StringComparison.Ordinal) => CombatRoleBudgetProfile.Bruiser,
                "duelist" => CombatRoleBudgetProfile.Duelist,
                _ => CombatRoleBudgetProfile.Vanguard,
            };
            var target = LoopCContentGovernance.UnitBudgetTargets[rarity].Target;
            var vector = roleProfile switch
            {
                CombatRoleBudgetProfile.Vanguard => MakeBudgetVector(10, 4, 36, 24, 4, 6, 8, 8),
                CombatRoleBudgetProfile.Bruiser => MakeBudgetVector(24, 12, 28, 6, 14, 2, 6, 8),
                CombatRoleBudgetProfile.Duelist => MakeBudgetVector(16, 28, 10, 4, 22, 2, 6, 12),
                CombatRoleBudgetProfile.Ranger => MakeBudgetVector(28, 8, 8, 4, 12, 4, 8, 28),
                CombatRoleBudgetProfile.Arcanist => MakeBudgetVector(8, 30, 6, 20, 8, 6, 8, 14),
                CombatRoleBudgetProfile.Support => MakeBudgetVector(6, 6, 18, 8, 6, 32, 6, 18),
                _ => MakeBudgetVector(14, 12, 18, 8, 8, 8, 8, 24),
            };
            AdjustBudgetFinalScore(vector, target);
            var counters = roleProfile switch
            {
                CombatRoleBudgetProfile.Vanguard => new[] { MakeCounter(CounterTool.InterceptPeel, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Bruiser => new[] { MakeCounter(CounterTool.GuardBreakMultiHit, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Duelist => new[] { MakeCounter(CounterTool.ArmorShred, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Ranger => new[] { MakeCounter(CounterTool.TrackingArea, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Arcanist => new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Standard), MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Light) },
                CombatRoleBudgetProfile.Support when rarity == ContentRarity.Common => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard) },
                CombatRoleBudgetProfile.Support => new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Standard), MakeCounter(CounterTool.AntiHealShatter, CounterCoverageStrength.Light) },
                _ => Array.Empty<CounterToolContribution>(),
            };
            var threats = roleProfile switch
            {
                CombatRoleBudgetProfile.Vanguard => new[] { ThreatPattern.ArmorFrontline },
                CombatRoleBudgetProfile.Bruiser or CombatRoleBudgetProfile.Duelist => new[] { ThreatPattern.DiveBackline },
                CombatRoleBudgetProfile.Ranger => new[] { ThreatPattern.EvasiveSkirmish },
                CombatRoleBudgetProfile.Arcanist => new[] { ThreatPattern.ControlChain },
                CombatRoleBudgetProfile.Support => new[] { ThreatPattern.SustainBall },
                _ => Array.Empty<ThreatPattern>(),
            };
            definition.BudgetCard = BuildBudgetCard(BudgetDomain.UnitBlueprint, rarity, PowerBand.Standard, roleProfile, vector, rarity == ContentRarity.Common ? 2 : rarity == ContentRarity.Rare ? 3 : 4, rarity == ContentRarity.Common ? 1 : 2, rarity == ContentRarity.Epic ? 1 : 0, threats, counters);
        }
    }

    private static ArchetypeFallbackSpec? GetArchetypeFallbackSpec(string archetypeId)
    {
        return archetypeId switch
        {
            "warden" => new ArchetypeFallbackSpec("human", "vanguard", "traitpool_warden", "vanguard", "shield", "anchor", RecruitTier.Common, new[] { "skill_guardian_core", "skill_warden_utility", "skill_vanguard_passive_1", "skill_vanguard_support_1" }),
            "guardian" => new ArchetypeFallbackSpec("undead", "vanguard", "traitpool_guardian", "vanguard", "shield", "anchor", RecruitTier.Rare, new[] { "skill_guardian_core", "skill_guardian_utility", "skill_vanguard_passive_2", "skill_vanguard_support_2" }),
            "bulwark" => new ArchetypeFallbackSpec("beastkin", "vanguard", "traitpool_bulwark", "vanguard", "shield", "anchor", RecruitTier.Rare, new[] { "skill_bulwark_core", "skill_guardian_core", "skill_warden_utility", "support_guarded" }),
            "slayer" => new ArchetypeFallbackSpec("human", "duelist", "traitpool_slayer", "striker", "blade", "bruiser", RecruitTier.Common, new[] { "skill_slayer_core", "skill_slayer_utility", "skill_duelist_passive_1", "skill_duelist_support_1" }),
            "raider" => new ArchetypeFallbackSpec("beastkin", "duelist", "traitpool_raider", "striker", "blade", "bruiser", RecruitTier.Rare, new[] { "skill_raider_core", "skill_raider_utility", "skill_duelist_passive_2", "skill_duelist_support_2" }),
            "reaver" => new ArchetypeFallbackSpec("undead", "duelist", "traitpool_reaver", "striker", "blade", "bruiser", RecruitTier.Rare, new[] { "skill_reaver_core", "skill_raider_core", "skill_reaver_utility", "support_executioner" }),
            "hunter" => new ArchetypeFallbackSpec("human", "ranger", "traitpool_hunter", "ranger", "bow", "carry", RecruitTier.Common, new[] { "skill_precision_shot", "skill_hunter_utility", "skill_ranger_passive_1", "skill_ranger_support_1" }),
            "scout" => new ArchetypeFallbackSpec("beastkin", "ranger", "traitpool_scout", "ranger", "bow", "carry", RecruitTier.Common, new[] { "skill_scout_core", "skill_scout_utility", "skill_ranger_passive_2", "skill_ranger_support_2" }),
            "marksman" => new ArchetypeFallbackSpec("undead", "ranger", "traitpool_marksman", "ranger", "bow", "carry", RecruitTier.Rare, new[] { "skill_marksman_core", "skill_hunter_utility", "skill_marksman_utility", "support_longshot" }),
            "priest" => new ArchetypeFallbackSpec("human", "mystic", "traitpool_priest", "mystic", "focus", "support", RecruitTier.Common, new[] { "skill_priest_core", "skill_minor_heal", "skill_mystic_passive_1", "skill_mystic_support_1" }),
            "hexer" => new ArchetypeFallbackSpec("undead", "mystic", "traitpool_hexer", "mystic", "focus", "support", RecruitTier.Epic, new[] { "skill_hexer_core", "skill_hexer_utility", "skill_mystic_passive_2", "skill_mystic_support_2" }),
            "shaman" => new ArchetypeFallbackSpec("beastkin", "mystic", "traitpool_shaman", "mystic", "focus", "support", RecruitTier.Epic, new[] { "skill_shaman_core", "skill_priest_core", "skill_shaman_utility", "support_siphon" }),
            _ => null,
        };
    }

    private static List<TacticPresetEntry> BuildFallbackTacticPreset(string classId, SkillDefinitionAsset skill)
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

    private static CounterToolContribution[] ResolveSkillCounters(SkillDefinitionAsset definition)
    {
        if (definition.Id.Contains("shaman_utility", StringComparison.Ordinal) || definition.Id.Contains("lingering", StringComparison.Ordinal))
        {
            return new[] { MakeCounter(CounterTool.CleaveWaveclear, CounterCoverageStrength.Light) };
        }

        if (definition.Id.Contains("hunter_mark", StringComparison.Ordinal) || definition.Id.Contains("longshot", StringComparison.Ordinal) || definition.Id.Contains("piercing", StringComparison.Ordinal))
        {
            return new[] { MakeCounter(CounterTool.TrackingArea, CounterCoverageStrength.Light) };
        }

        if (definition.Id.Contains("hexer", StringComparison.Ordinal) || definition.AppliedStatuses.Any(status => string.Equals(status.StatusId, "silence", StringComparison.Ordinal)))
        {
            return new[] { MakeCounter(CounterTool.Exposure, CounterCoverageStrength.Light) };
        }

        if (definition.Id.Contains("purifying", StringComparison.Ordinal) || !string.IsNullOrWhiteSpace(definition.CleanseProfileId))
        {
            return new[] { MakeCounter(CounterTool.TenacityStability, CounterCoverageStrength.Light) };
        }

        return Array.Empty<CounterToolContribution>();
    }

    private static BudgetCard BuildBudgetCard(
        BudgetDomain domain,
        ContentRarity rarity,
        PowerBand powerBand,
        CombatRoleBudgetProfile? roleProfile,
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

    private static TraitEntry BuildFallbackTrait(string archetypeId, string id, string statId, float value)
    {
        return new TraitEntry
        {
            Id = id,
            NameKey = ContentLocalizationTables.BuildTraitNameKey(archetypeId, id),
            DescriptionKey = ContentLocalizationTables.BuildTraitDescriptionKey(archetypeId, id),
            Modifiers = new List<SerializableStatModifier>
            {
                new() { StatId = statId, Value = value },
            },
        };
    }

    private static List<string> BuildSiteEncounterIds(string siteId)
    {
        return new List<string>
        {
            $"{siteId}_skirmish_1",
            $"{siteId}_skirmish_2",
            $"{siteId}_elite_1",
            $"{siteId}_boss_1",
        };
    }

    private static bool TryGetSiteIdFromEncounterId(string encounterId, out string siteId)
    {
        foreach (var marker in new[] { "_skirmish_", "_elite_", "_boss_" })
        {
            var index = encounterId.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0)
            {
                siteId = encounterId[..index];
                return true;
            }
        }

        siteId = string.Empty;
        return false;
    }

    private static bool TryGetSiteIdFromSquadId(string squadId, out string siteId)
    {
        if (squadId.EndsWith("_squad", StringComparison.Ordinal))
        {
            return TryGetSiteIdFromEncounterId(squadId[..^"_squad".Length], out siteId);
        }

        siteId = string.Empty;
        return false;
    }

    private static bool TryGetEncounterKindFromId(string encounterId, out EncounterKindValue kind)
    {
        if (encounterId.Contains("_boss_", StringComparison.Ordinal))
        {
            kind = EncounterKindValue.Boss;
            return true;
        }

        if (encounterId.Contains("_elite_", StringComparison.Ordinal))
        {
            kind = EncounterKindValue.Elite;
            return true;
        }

        if (encounterId.Contains("_skirmish_", StringComparison.Ordinal))
        {
            kind = EncounterKindValue.Skirmish;
            return true;
        }

        kind = EncounterKindValue.Skirmish;
        return false;
    }

    private static List<EnemySquadMemberDefinition> BuildEnemySquadMembers(CampaignSiteFallbackSpec spec, string squadId)
    {
        var archetypes = squadId.Contains("_boss_", StringComparison.Ordinal)
            ? new[] { spec.BossCaptain, spec.BossEscorts[0], spec.BossEscorts[1] }
            : squadId.Contains("_elite_", StringComparison.Ordinal)
                ? spec.Elite.ToArray()
                : squadId.Contains("_skirmish_2", StringComparison.Ordinal)
                    ? spec.SkirmishB.ToArray()
                    : spec.SkirmishA.ToArray();
        var anchors = new[]
        {
            DeploymentAnchorValue.FrontCenter,
            DeploymentAnchorValue.FrontTop,
            DeploymentAnchorValue.FrontBottom,
            DeploymentAnchorValue.BackCenter,
        };
        var members = new List<EnemySquadMemberDefinition>(archetypes.Length);
        for (var i = 0; i < archetypes.Length; i++)
        {
            members.Add(new EnemySquadMemberDefinition
            {
                Id = $"{squadId}.member.{i + 1}",
                NameKey = ContentLocalizationTables.BuildEnemySquadNameKey($"{squadId}.member.{i + 1}"),
                ArchetypeId = archetypes[i],
                Anchor = anchors[Math.Min(i, anchors.Length - 1)],
                Role = squadId.Contains("_boss_", StringComparison.Ordinal)
                    ? i == 0 ? EnemySquadMemberRoleValue.Captain : EnemySquadMemberRoleValue.Escort
                    : EnemySquadMemberRoleValue.Unit,
            });
        }

        return members;
    }

    private static bool TryGetCampaignSiteFallbackSpec(string siteId, out CampaignSiteFallbackSpec spec)
    {
        spec = siteId switch
        {
            "site_ashen_gate" => new CampaignSiteFallbackSpec(
                "site_ashen_gate",
                "chapter_ashen_frontier",
                1,
                "faction_ashen_vanguard",
                ThreatTierValue.Tier1,
                "boss_overlay_ashen_gate",
                new[] { "warden", "hunter", "hexer", "raider" },
                new[] { "guardian", "scout", "hexer", "hunter" },
                new[] { "bulwark", "hunter", "hexer", "guardian" },
                "bulwark",
                new[] { "hunter", "hexer" }),
            "site_cinder_watch" => new CampaignSiteFallbackSpec(
                "site_cinder_watch",
                "chapter_ashen_frontier",
                2,
                "faction_cinder_watch",
                ThreatTierValue.Tier1,
                "boss_overlay_cinder_watch",
                new[] { "hunter", "scout", "raider", "priest" },
                new[] { "marksman", "scout", "guardian", "hexer" },
                new[] { "marksman", "bulwark", "scout", "hexer" },
                "marksman",
                new[] { "scout", "priest" }),
            "site_forgotten_warren" => new CampaignSiteFallbackSpec(
                "site_forgotten_warren",
                "chapter_warren_depths",
                1,
                "faction_warren_pack",
                ThreatTierValue.Tier2,
                "boss_overlay_forgotten_warren",
                new[] { "raider", "scout", "shaman", "hunter" },
                new[] { "reaver", "scout", "shaman", "hunter" },
                new[] { "reaver", "raider", "scout", "shaman" },
                "reaver",
                new[] { "scout", "shaman" }),
            "site_twisted_den" => new CampaignSiteFallbackSpec(
                "site_twisted_den",
                "chapter_warren_depths",
                2,
                "faction_twisted_den",
                ThreatTierValue.Tier2,
                "boss_overlay_twisted_den",
                new[] { "slayer", "raider", "scout", "shaman" },
                new[] { "slayer", "hunter", "scout", "priest" },
                new[] { "slayer", "reaver", "scout", "priest" },
                "slayer",
                new[] { "scout", "priest" }),
            "site_ruined_crypt" => new CampaignSiteFallbackSpec(
                "site_ruined_crypt",
                "chapter_ruined_crypts",
                1,
                "faction_bone_host",
                ThreatTierValue.Tier3,
                "boss_overlay_ruined_crypt",
                new[] { "guardian", "hexer", "priest", "hunter" },
                new[] { "bulwark", "hexer", "priest", "marksman" },
                new[] { "bulwark", "hexer", "priest", "marksman" },
                "hexer",
                new[] { "priest", "guardian" }),
            "site_grave_sanctum" => new CampaignSiteFallbackSpec(
                "site_grave_sanctum",
                "chapter_ruined_crypts",
                2,
                "faction_grave_sanctum",
                ThreatTierValue.Tier3,
                "boss_overlay_grave_sanctum",
                new[] { "guardian", "hexer", "shaman", "marksman" },
                new[] { "bulwark", "hexer", "shaman", "hunter" },
                new[] { "bulwark", "guardian", "hexer", "shaman" },
                "shaman",
                new[] { "guardian", "hexer" }),
            _ => null!,
        };

        return spec != null;
    }

    private sealed record ArchetypeFallbackSpec(
        string RaceId,
        string ClassId,
        string TraitPoolId,
        string RoleFamilyTag,
        string WeaponFamilyTag,
        string RoleTag,
        RecruitTier RecruitTier,
        string[] DefaultSkillIds);

    private sealed record CampaignSiteFallbackSpec(
        string SiteId,
        string ChapterId,
        int SiteOrder,
        string FactionId,
        ThreatTierValue ThreatTier,
        string BossOverlayId,
        IReadOnlyList<string> SkirmishA,
        IReadOnlyList<string> SkirmishB,
        IReadOnlyList<string> Elite,
        string BossCaptain,
        IReadOnlyList<string> BossEscorts);

    private static T ResolveReference<T>(
        string[] lines,
        string key,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        var guid = ExtractGuid(ExtractLine(lines, key));
        return ResolveGuid(guid, guidToPath, definitions)!;
    }

    private static T? ResolveGuid<T>(
        string? guid,
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, T> definitions)
        where T : UnityEngine.Object
    {
        if (string.IsNullOrWhiteSpace(guid) || !guidToPath.TryGetValue(guid, out var path))
        {
            return null;
        }

        var fileName = Path.GetFileNameWithoutExtension(path);
        var candidateId = fileName[(fileName.IndexOf('_') + 1)..];
        if (definitions.TryGetValue(candidateId, out var byId))
        {
            return byId;
        }

        return definitions.Values.FirstOrDefault(definition => string.Equals(definition.name, fileName, StringComparison.Ordinal));
    }

    private static string ExtractLine(string[] lines, string key)
    {
        return lines.FirstOrDefault(line => line.TrimStart().StartsWith(key, StringComparison.Ordinal)) ?? string.Empty;
    }

    private static string ExtractValue(string[] lines, string key)
    {
        var line = ExtractLine(lines, key);
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return line.TrimStart()[key.Length..].Trim();
    }

    private static int ExtractInt(string[] lines, string key)
    {
        return ParseInt(ExtractValue(lines, key));
    }

    private static float ExtractFloat(string[] lines, string key)
    {
        return ParseFloat(ExtractValue(lines, key));
    }

    private static bool ExtractBool(string[] lines, string key)
    {
        return ParseBool(ExtractValue(lines, key));
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static List<TEnum> ParsePackedEnumList<TEnum>(string packedHex) where TEnum : struct, Enum
    {
        var result = new List<TEnum>();
        if (string.IsNullOrWhiteSpace(packedHex) || packedHex.Length < 8)
        {
            return result;
        }

        for (var index = 0; index + 8 <= packedHex.Length; index += 8)
        {
            var chunk = packedHex.Substring(index, 8);
            try
            {
                var bytes = new byte[4];
                for (var byteIndex = 0; byteIndex < 4; byteIndex++)
                {
                    bytes[byteIndex] = byte.Parse(
                        chunk.Substring(byteIndex * 2, 2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture);
                }

                var value = BitConverter.ToInt32(bytes, 0);
                result.Add((TEnum)Enum.ToObject(typeof(TEnum), value));
            }
            catch
            {
                continue;
            }
        }

        return result;
    }

    private static float ParseFloat(string value)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
    }

    private static bool ParseBool(string value)
    {
        return bool.TryParse(value, out var parsed)
            ? parsed
            : ParseInt(value) != 0;
    }

    private static int FindLineIndex(string[] lines, string key)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.Equals(lines[i].Trim(), key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int GetIndent(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == ' ')
        {
            count++;
        }

        return count;
    }

    private static string ExtractGuid(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        var match = GuidRegex.Match(line);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ToUnityPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static void SetLegacyField(object instance, string fieldName, string value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(instance, value);
        }
    }
}
