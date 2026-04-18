using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Meta.Model;
using SM.Unity.ContentParsing;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity;

public sealed record RuntimeCombatParsedContent(
    IReadOnlyList<StatDefinition> Stats,
    IReadOnlyList<RaceDefinition> Races,
    IReadOnlyList<ClassDefinition> Classes,
    IReadOnlyList<CharacterDefinition> Characters,
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
    IReadOnlyList<FirstPlayableSliceDefinitionAsset> FirstPlayableSlices,
    IReadOnlyDictionary<int, string> AssetPaths)
{
    public IReadOnlyList<ScriptableObject> AllAssets =>
        Stats.Cast<ScriptableObject>()
            .Concat(Races)
            .Concat(Classes)
            .Concat(Characters)
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
            .Concat(FirstPlayableSlices)
            .ToList();
}

public static class RuntimeCombatContentFileParser
{
    internal const string RootPath = "Assets/Resources/_Game/Content/Definitions";

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
            var stats = StatFileParser.LoadStats(guidToPath);
            var races = ArchetypeFileParser.LoadRaces(guidToPath);
            var classes = ArchetypeFileParser.LoadClasses(guidToPath);
            var skills = SkillFileParser.LoadSkills(guidToPath);
            var traitPools = ArchetypeFileParser.LoadTraitPools(guidToPath);
            var stableTags = ItemFileParser.LoadStableTags();
            var items = ItemFileParser.LoadItems(guidToPath, stableTags, skills);
            var affixes = ItemFileParser.LoadAffixes(guidToPath, stableTags);
            var augments = ItemFileParser.LoadAugments(guidToPath, stableTags);
            var teamTactics = CatalogFileParser.LoadTeamTactics(guidToPath, stableTags);
            var roleInstructions = CatalogFileParser.LoadRoleInstructions(guidToPath, stableTags);
            var passiveNodes = CatalogFileParser.LoadPassiveNodes(guidToPath, stableTags);
            var passiveBoards = CatalogFileParser.LoadPassiveBoards(guidToPath, passiveNodes);
            var synergyTiers = CatalogFileParser.LoadSynergyTiers();
            var synergies = CatalogFileParser.LoadSynergies(guidToPath, synergyTiers);
            var expeditions = CampaignFileParser.LoadExpeditions(guidToPath);
            var rewardTables = RewardFileParser.LoadRewardTables();
            var archetypes = ArchetypeFileParser.LoadArchetypes(guidToPath, races, classes, traitPools, skills);
            var characters = CharacterFileParser.LoadCharacters(
                guidToPath,
                races,
                classes,
                archetypes.ToDictionary(definition => definition.Id, definition => definition, StringComparer.Ordinal),
                roleInstructions);
            var campaignChapters = CampaignFileParser.LoadCampaignChapters();
            var expeditionSites = CampaignFileParser.LoadExpeditionSites();
            var encounters = CampaignFileParser.LoadEncounters();
            var enemySquads = CampaignFileParser.LoadEnemySquads();
            var bossOverlays = CampaignFileParser.LoadBossOverlays();
            var statusFamilies = StatusFileParser.LoadStatusFamilies();
            var cleanseProfiles = StatusFileParser.LoadCleanseProfiles();
            var controlDiminishingRules = StatusFileParser.LoadControlDiminishingRules();
            var rewardSources = RewardFileParser.LoadRewardSources();
            var dropTables = RewardFileParser.LoadDropTables();
            var lootBundles = RewardFileParser.LoadLootBundles();
            var traitTokens = StatusFileParser.LoadTraitTokens();
            var firstPlayableSlices = LoadFirstPlayableSlices();
            var assetPaths = BuildAssetPaths(
                stats.Values,
                races.Values,
                classes.Values,
                characters.Values,
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
                traitTokens.Values,
                firstPlayableSlices);

            parsed = new RuntimeCombatParsedContent(
                stats.Values.ToList(),
                races.Values.ToList(),
                classes.Values.ToList(),
                characters.Values.ToList(),
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
                firstPlayableSlices,
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
            CharacterDefinition => "Characters",
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
            FirstPlayableSliceDefinitionAsset => "FirstPlayable",
            _ => string.Empty,
        };

        return string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(asset.name)
            ? string.Empty
            : ToUnityPath(Path.Combine(RootPath, folder, asset.name + ".asset"));
    }

    private static IReadOnlyList<FirstPlayableSliceDefinitionAsset> LoadFirstPlayableSlices()
    {
        var result = new List<FirstPlayableSliceDefinitionAsset>();
        var folderPath = Path.Combine(RootPath, "FirstPlayable");
        if (!Directory.Exists(folderPath))
        {
            return result;
        }

        foreach (var assetPath in Directory.EnumerateFiles(folderPath, "*.asset", SearchOption.TopDirectoryOnly))
        {
            var lines = File.ReadAllLines(assetPath);
            var definition = ScriptableObject.CreateInstance<FirstPlayableSliceDefinitionAsset>();
            definition.name = Path.GetFileNameWithoutExtension(assetPath);
            definition.UnitBlueprintCap = ExtractIntOrDefault(lines, "UnitBlueprintCap:", definition.UnitBlueprintCap);
            definition.SignatureActiveCap = ExtractIntOrDefault(lines, "SignatureActiveCap:", definition.SignatureActiveCap);
            definition.SignaturePassiveCap = ExtractIntOrDefault(lines, "SignaturePassiveCap:", definition.SignaturePassiveCap);
            definition.FlexActiveCap = ExtractIntOrDefault(lines, "FlexActiveCap:", definition.FlexActiveCap);
            definition.FlexPassiveCap = ExtractIntOrDefault(lines, "FlexPassiveCap:", definition.FlexPassiveCap);
            definition.AffixCap = ExtractIntOrDefault(lines, "AffixCap:", definition.AffixCap);
            definition.SynergyFamilyCap = ExtractIntOrDefault(lines, "SynergyFamilyCap:", definition.SynergyFamilyCap);
            definition.TemporaryAugmentCap = ExtractIntOrDefault(lines, "TemporaryAugmentCap:", definition.TemporaryAugmentCap);
            definition.PermanentAugmentCap = ExtractIntOrDefault(lines, "PermanentAugmentCap:", definition.PermanentAugmentCap);
            definition.PassiveBoardCap = ExtractIntOrDefault(lines, "PassiveBoardCap:", definition.PassiveBoardCap);
            definition.RequireAllThreatPatternsCovered = ExtractBoolOrDefault(lines, "RequireAllThreatPatternsCovered:", definition.RequireAllThreatPatternsCovered);
            definition.RequireAllCounterToolsCovered = ExtractBoolOrDefault(lines, "RequireAllCounterToolsCovered:", definition.RequireAllCounterToolsCovered);
            definition.CoverageQuotas = ParseCoverageQuotas(lines);
            definition.UnitBlueprintIds = ParseStringList(lines, "UnitBlueprintIds:");
            definition.SignatureActiveIds = ParseStringList(lines, "SignatureActiveIds:");
            definition.SignaturePassiveIds = ParseStringList(lines, "SignaturePassiveIds:");
            definition.FlexActiveIds = ParseStringList(lines, "FlexActiveIds:");
            definition.FlexPassiveIds = ParseStringList(lines, "FlexPassiveIds:");
            definition.AffixIds = ParseStringList(lines, "AffixIds:");
            definition.SynergyFamilyIds = ParseStringList(lines, "SynergyFamilyIds:");
            definition.TemporaryAugmentIds = ParseStringList(lines, "TemporaryAugmentIds:");
            definition.PermanentAugmentIds = ParseStringList(lines, "PermanentAugmentIds:");
            definition.PassiveBoardIds = ParseStringList(lines, "PassiveBoardIds:");
            definition.ParkingLotContentIds = ParseStringList(lines, "ParkingLotContentIds:");
            definition.SynergyGrammar = ParseSynergyGrammar(lines);
            definition.ClassLabelMappings = ParseClassLabelMappings(lines);
            result.Add(definition);
        }

        return result;
    }

    private static int ExtractIntOrDefault(string[] lines, string key, int fallback)
    {
        var value = ExtractValue(lines, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : ParseInt(value);
    }

    private static bool ExtractBoolOrDefault(string[] lines, string key, bool fallback)
    {
        var value = ExtractValue(lines, key);
        return string.IsNullOrWhiteSpace(value) ? fallback : ParseBool(value);
    }

    private static List<SliceCoverageQuota> ParseCoverageQuotas(string[] lines)
    {
        var result = new List<SliceCoverageQuota>();
        var index = FindLineIndex(lines, "CoverageQuotas:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- Kind:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var quota = new SliceCoverageQuota
            {
                Kind = (SliceCoverageQuotaKind)ParseInt(trimmed["- Kind:".Length..].Trim())
            };
            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- Kind:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("MinimumCount:", StringComparison.Ordinal))
                {
                    quota.MinimumCount = ParseInt(trimmed["MinimumCount:".Length..].Trim());
                }
            }

            result.Add(quota);
        }

        return result;
    }

    private static List<SynergyGrammarEntry> ParseSynergyGrammar(string[] lines)
    {
        var result = new List<SynergyGrammarEntry>();
        var index = FindLineIndex(lines, "SynergyGrammar:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- FamilyId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var entry = new SynergyGrammarEntry
            {
                FamilyId = trimmed["- FamilyId:".Length..].Trim()
            };
            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- FamilyId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("FamilyType:", StringComparison.Ordinal))
                {
                    entry.FamilyType = (SynergyFamilyType)ParseInt(trimmed["FamilyType:".Length..].Trim());
                }
                else if (trimmed.StartsWith("MinorThreshold:", StringComparison.Ordinal))
                {
                    entry.MinorThreshold = ParseInt(trimmed["MinorThreshold:".Length..].Trim());
                }
                else if (trimmed.StartsWith("MajorThreshold:", StringComparison.Ordinal))
                {
                    entry.MajorThreshold = ParseInt(trimmed["MajorThreshold:".Length..].Trim());
                }
            }

            result.Add(entry);
        }

        return result;
    }

    private static List<ClassLabelMapping> ParseClassLabelMappings(string[] lines)
    {
        var result = new List<ClassLabelMapping>();
        var index = FindLineIndex(lines, "ClassLabelMappings:");
        if (index < 0)
        {
            return result;
        }

        for (index++; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith("- CanonicalId:", StringComparison.Ordinal))
            {
                if (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal))
                {
                    break;
                }

                continue;
            }

            var mapping = new ClassLabelMapping
            {
                CanonicalId = trimmed["- CanonicalId:".Length..].Trim()
            };
            for (index++; index < lines.Length; index++)
            {
                trimmed = lines[index].Trim();
                if (trimmed.StartsWith("- CanonicalId:", StringComparison.Ordinal)
                    || (GetIndent(lines[index]) <= 2 && trimmed.EndsWith(":", StringComparison.Ordinal)))
                {
                    index--;
                    break;
                }

                if (trimmed.StartsWith("PlayerFacingLabel:", StringComparison.Ordinal))
                {
                    mapping.PlayerFacingLabel = trimmed["PlayerFacingLabel:".Length..].Trim();
                }
            }

            result.Add(mapping);
        }

        return result;
    }

    internal static Dictionary<string, T> LoadAssets<T>(
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
}
