using System;
using System.Linq;
using SM.Content;
using SM.Content.Definitions;
using UnityEditor;

namespace SM.Editor.SeedData;

public static partial class SampleSeedGenerator
{
    private static readonly (string id, string enName, string koName)[]
        FarmingIdentityStableTagDefinitions =
        {
            ("pool_accessory", "Accessory Affix Pool", "장신구 어픽스 풀"),
            ("pool_armor", "Armor Affix Pool", "방어구 어픽스 풀"),
            ("pool_blade", "Blade Affix Pool", "검 어픽스 풀"),
            ("pool_bow", "Bow Affix Pool", "활 어픽스 풀"),
            ("pool_duelist_armor", "Duelist Armor Affix Pool", "결투가 방어구 어픽스 풀"),
            ("pool_duelist_trinket", "Duelist Trinket Affix Pool", "결투가 장신구 어픽스 풀"),
            ("pool_focus", "Focus Affix Pool", "초점구 어픽스 풀"),
            ("pool_mystic_armor", "Mystic Armor Affix Pool", "비술사 방어구 어픽스 풀"),
            ("pool_mystic_trinket", "Mystic Trinket Affix Pool", "비술사 장신구 어픽스 풀"),
            ("pool_ranger_armor", "Ranger Armor Affix Pool", "사수 방어구 어픽스 풀"),
            ("pool_ranger_trinket", "Ranger Trinket Affix Pool", "사수 장신구 어픽스 풀"),
            ("pool_shield", "Shield Affix Pool", "방패 어픽스 풀"),
            ("pool_vanguard_armor", "Vanguard Armor Affix Pool", "선봉 방어구 어픽스 풀"),
            ("pool_vanguard_trinket", "Vanguard Trinket Affix Pool", "선봉 장신구 어픽스 풀"),
        };

    [MenuItem("SM/Internal/Content/Apply Farming Identity Seal Authoring")]
    public static void ApplyFarmingIdentity()
    {
        EnsureFolders();
        foreach (var definition in FarmingIdentityStableTagDefinitions)
        {
            CreateAsset<StableTagDefinition>(
                $"{ResourcesRoot}/StableTags/tag_{definition.id}.asset",
                asset =>
                {
                    asset.Id = definition.id;
                    asset.NameKey =
                        $"content.tag.{ContentLocalizationTables.NormalizeId(definition.id)}.name";
                    UpsertStringEntry(
                        ContentLocalizationTables.Tags,
                        asset.NameKey,
                        definition.koName,
                        definition.enName);
                });
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceUpdate
            | ImportAssetOptions.ForceSynchronousImport);
        EquipmentContentV1Assetizer.ApplyFarmingIdentity();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceUpdate
            | ImportAssetOptions.ForceSynchronousImport);
    }
}
