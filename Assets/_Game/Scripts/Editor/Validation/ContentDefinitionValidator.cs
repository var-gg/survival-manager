using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public static class ContentDefinitionValidator
{
    [MenuItem("SM/Validation/Validate Content Definitions")]
    public static void Validate()
    {
        var allAssets = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Game/Content/Definitions" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
            .OfType<ScriptableObject>()
            .ToList();

        var ids = new Dictionary<string, List<string>>();
        var errors = new List<string>();

        foreach (var asset in allAssets)
        {
            switch (asset)
            {
                case StatDefinition stat:
                    RegisterId(ids, stat.Id, AssetDatabase.GetAssetPath(stat));
                    break;
                case RaceDefinition race:
                    RegisterId(ids, race.Id, AssetDatabase.GetAssetPath(race));
                    break;
                case ClassDefinition @class:
                    RegisterId(ids, @class.Id, AssetDatabase.GetAssetPath(@class));
                    break;
                case TraitPoolDefinition traitPool:
                    RegisterId(ids, traitPool.Id, AssetDatabase.GetAssetPath(traitPool));
                    if (traitPool.PositiveTraits.Count < 3 || traitPool.NegativeTraits.Count < 3)
                    {
                        errors.Add($"Trait pool missing 3+3 structure: {AssetDatabase.GetAssetPath(traitPool)}");
                    }
                    break;
                case UnitArchetypeDefinition archetype:
                    RegisterId(ids, archetype.Id, AssetDatabase.GetAssetPath(archetype));
                    if (archetype.Race == null || archetype.Class == null || archetype.TraitPool == null)
                    {
                        errors.Add($"Archetype missing references: {AssetDatabase.GetAssetPath(archetype)}");
                    }
                    if (archetype.TacticPreset == null || archetype.TacticPreset.Count == 0)
                    {
                        errors.Add($"Archetype missing tactic preset: {AssetDatabase.GetAssetPath(archetype)}");
                    }
                    break;
                case SkillDefinitionAsset skill:
                    RegisterId(ids, skill.Id, AssetDatabase.GetAssetPath(skill));
                    break;
                case AugmentDefinition augment:
                    RegisterId(ids, augment.Id, AssetDatabase.GetAssetPath(augment));
                    break;
                case ItemBaseDefinition item:
                    RegisterId(ids, item.Id, AssetDatabase.GetAssetPath(item));
                    break;
                case AffixDefinition affix:
                    RegisterId(ids, affix.Id, AssetDatabase.GetAssetPath(affix));
                    break;
                case ExpeditionDefinition expedition:
                    RegisterId(ids, expedition.Id, AssetDatabase.GetAssetPath(expedition));
                    if (expedition.Nodes.Any(n => n.RewardTable == null))
                    {
                        errors.Add($"Expedition has node with missing reward table: {AssetDatabase.GetAssetPath(expedition)}");
                    }
                    break;
                case RewardTableDefinition rewardTable:
                    RegisterId(ids, rewardTable.Id, AssetDatabase.GetAssetPath(rewardTable));
                    break;
            }
        }

        foreach (var pair in ids.Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value.Count > 1))
        {
            errors.Add($"Duplicate ID '{pair.Key}': {string.Join(", ", pair.Value)}");
        }

        if (errors.Count == 0)
        {
            Debug.Log("SM content validation passed.");
            return;
        }

        foreach (var error in errors)
        {
            Debug.LogError(error);
        }

        throw new System.Exception($"SM content validation failed with {errors.Count} issue(s).");
    }

    private static void RegisterId(Dictionary<string, List<string>> ids, string id, string path)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!ids.TryGetValue(id, out var list))
        {
            list = new List<string>();
            ids[id] = list;
        }

        list.Add(path);
    }
}
