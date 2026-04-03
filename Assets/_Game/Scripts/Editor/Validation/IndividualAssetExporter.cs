using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SM.Content.Definitions;
using SM.Meta.Model;
using SM.Unity.ContentConversion;
using UnityEngine;

namespace SM.Editor.Validation;

public static class IndividualAssetExporter
{
    public const string OutputRoot = "Assets/Resources/_Game/Content/ExportedData";

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
    };

    private static readonly IReadOnlyDictionary<Type, (string Subfolder, Func<ScriptableObject, ContentDefinitionRegistry?, object?> Convert)> TypeMap =
        new Dictionary<Type, (string, Func<ScriptableObject, ContentDefinitionRegistry?, object?>)>
        {
            [typeof(UnitArchetypeDefinition)] = ("Archetypes", (asset, reg) =>
            {
                var def = (UnitArchetypeDefinition)asset;
                if (reg == null || string.IsNullOrWhiteSpace(def.Id)) return null;
                var converter = new ArchetypeConverter(reg.SkillDefinitions, reg.FirstPlayableSlice);
                return converter.BuildArchetypeTemplate(def);
            }),
            [typeof(SkillDefinitionAsset)] = ("Skills", (asset, _) =>
            {
                var def = (SkillDefinitionAsset)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : SkillConverter.BuildSkillSpec(def);
            }),
            [typeof(ItemBaseDefinition)] = ("Items", (asset, _) =>
            {
                var def = (ItemBaseDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : ModifierPackageConverter.BuildItemPackage(def);
            }),
            [typeof(AffixDefinition)] = ("Affixes", (asset, _) =>
            {
                var def = (AffixDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : ModifierPackageConverter.BuildAffixPackage(def);
            }),
            [typeof(AugmentDefinition)] = ("Augments", (asset, _) =>
            {
                var def = (AugmentDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : (object)CatalogEntryConverter.BuildAugmentCatalogEntry(def);
            }),
            [typeof(TeamTacticDefinition)] = ("TeamTactics", (asset, _) =>
            {
                var def = (TeamTacticDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CatalogEntryConverter.BuildTeamTacticTemplate(def);
            }),
            [typeof(RoleInstructionDefinition)] = ("RoleInstructions", (asset, _) =>
            {
                var def = (RoleInstructionDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CatalogEntryConverter.BuildRoleInstructionTemplate(def);
            }),
            [typeof(PassiveNodeDefinition)] = ("PassiveNodes", (asset, _) =>
            {
                var def = (PassiveNodeDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CatalogEntryConverter.BuildPassiveNodeTemplate(def);
            }),
            [typeof(CampaignChapterDefinition)] = ("CampaignChapters", (asset, _) =>
            {
                var def = (CampaignChapterDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CampaignConverter.BuildCampaignChapterTemplate(def);
            }),
            [typeof(ExpeditionSiteDefinition)] = ("ExpeditionSites", (asset, _) =>
            {
                var def = (ExpeditionSiteDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CampaignConverter.BuildExpeditionSiteTemplate(def);
            }),
            [typeof(EncounterDefinition)] = ("Encounters", (asset, _) =>
            {
                var def = (EncounterDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CampaignConverter.BuildEncounterTemplate(def);
            }),
            [typeof(EnemySquadTemplateDefinition)] = ("EnemySquads", (asset, _) =>
            {
                var def = (EnemySquadTemplateDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CampaignConverter.BuildEnemySquadTemplate(def);
            }),
            [typeof(BossOverlayDefinition)] = ("BossOverlays", (asset, _) =>
            {
                var def = (BossOverlayDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : CampaignConverter.BuildBossOverlayTemplate(def);
            }),
            [typeof(StatusFamilyDefinition)] = ("StatusFamilies", (asset, _) =>
            {
                var def = (StatusFamilyDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : StatusConverter.BuildStatusFamilyTemplate(def);
            }),
            [typeof(CleanseProfileDefinition)] = ("CleanseProfiles", (asset, _) =>
            {
                var def = (CleanseProfileDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : StatusConverter.BuildCleanseProfileTemplate(def);
            }),
            [typeof(ControlDiminishingRuleDefinition)] = ("ControlDiminishingRules", (asset, _) =>
            {
                var def = (ControlDiminishingRuleDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : StatusConverter.BuildControlDiminishingTemplate(def);
            }),
            [typeof(RewardSourceDefinition)] = ("RewardSources", (asset, _) =>
            {
                var def = (RewardSourceDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : RewardConverter.BuildRewardSourceTemplate(def);
            }),
            [typeof(DropTableDefinition)] = ("DropTables", (asset, _) =>
            {
                var def = (DropTableDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : RewardConverter.BuildDropTableTemplate(def);
            }),
            [typeof(LootBundleDefinition)] = ("LootBundles", (asset, _) =>
            {
                var def = (LootBundleDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : RewardConverter.BuildLootBundleTemplate(def);
            }),
            [typeof(TraitTokenDefinition)] = ("TraitTokens", (asset, _) =>
            {
                var def = (TraitTokenDefinition)asset;
                return string.IsNullOrWhiteSpace(def.Id) ? null : RewardConverter.BuildTraitTokenTemplate(def);
            }),
        };

    internal static (string json, string relativePath)? TryExportSingle(
        ScriptableObject asset,
        ContentDefinitionRegistry? registry = null)
    {
        if (asset == null) return null;

        var assetType = asset.GetType();
        if (!TypeMap.TryGetValue(assetType, out var mapping)) return null;

        var converted = mapping.Convert(asset, registry);
        if (converted == null) return null;

        var id = GetAssetId(asset);
        if (string.IsNullOrWhiteSpace(id)) return null;

        var json = JsonConvert.SerializeObject(converted, JsonSettings);
        var relativePath = $"{OutputRoot}/{mapping.Subfolder}/{id}.json";
        return (json, relativePath);
    }

    public static void ExportAll(string outputRoot = OutputRoot)
    {
        var registry = new ContentDefinitionRegistry();
        registry.EnsureLoaded();
        var assembler = new SnapshotAssembler(registry);
        var snapshot = assembler.Assemble();

        ExportDictionary(outputRoot, "Archetypes", snapshot.Archetypes);
        ExportDictionary(outputRoot, "Skills", snapshot.SkillCatalog);
        ExportDictionary(outputRoot, "Items", snapshot.ItemPackages);
        ExportDictionary(outputRoot, "Affixes", snapshot.AffixPackages);
        ExportDictionary(outputRoot, "Augments", snapshot.AugmentCatalog);
        ExportDictionary(outputRoot, "TeamTactics", snapshot.TeamTactics);
        ExportDictionary(outputRoot, "RoleInstructions", snapshot.RoleInstructions);
        ExportDictionary(outputRoot, "PassiveNodes", snapshot.PassiveNodes);
        ExportDictionary(outputRoot, "Synergies", snapshot.SynergyCatalog);
        if (snapshot.CampaignChapters != null) ExportDictionary(outputRoot, "CampaignChapters", snapshot.CampaignChapters);
        if (snapshot.ExpeditionSites != null) ExportDictionary(outputRoot, "ExpeditionSites", snapshot.ExpeditionSites);
        if (snapshot.Encounters != null) ExportDictionary(outputRoot, "Encounters", snapshot.Encounters);
        if (snapshot.EnemySquads != null) ExportDictionary(outputRoot, "EnemySquads", snapshot.EnemySquads);
        if (snapshot.BossOverlays != null) ExportDictionary(outputRoot, "BossOverlays", snapshot.BossOverlays);
        if (snapshot.StatusFamilies != null) ExportDictionary(outputRoot, "StatusFamilies", snapshot.StatusFamilies);
        if (snapshot.CleanseProfiles != null) ExportDictionary(outputRoot, "CleanseProfiles", snapshot.CleanseProfiles);
        if (snapshot.ControlDiminishingRules != null) ExportDictionary(outputRoot, "ControlDiminishingRules", snapshot.ControlDiminishingRules);
        if (snapshot.RewardSources != null) ExportDictionary(outputRoot, "RewardSources", snapshot.RewardSources);
        if (snapshot.DropTables != null) ExportDictionary(outputRoot, "DropTables", snapshot.DropTables);
        if (snapshot.LootBundles != null) ExportDictionary(outputRoot, "LootBundles", snapshot.LootBundles);
        if (snapshot.TraitTokens != null) ExportDictionary(outputRoot, "TraitTokens", snapshot.TraitTokens);

        Debug.Log($"[IndividualAssetExporter] Exported individual assets to {outputRoot}");
    }

    private static void ExportDictionary<T>(string outputRoot, string subfolder, IReadOnlyDictionary<string, T> catalog)
    {
        var dir = Path.Combine(outputRoot, subfolder);
        Directory.CreateDirectory(dir);
        foreach (var (id, value) in catalog)
        {
            var json = JsonConvert.SerializeObject(value, JsonSettings);
            File.WriteAllText(Path.Combine(dir, $"{id}.json"), json);
        }
    }

    private static string? GetAssetId(ScriptableObject asset)
    {
        var idProp = asset.GetType().GetProperty("Id");
        return idProp?.GetValue(asset) as string;
    }
}
