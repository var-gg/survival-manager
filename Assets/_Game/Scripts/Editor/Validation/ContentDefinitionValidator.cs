using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Editor.SeedData;
using SM.Core.Stats;
using UnityEditor;
using UnityEngine;

namespace SM.Editor.Validation;

public enum StatIdValidationStatus
{
    Unsupported = 0,
    LegacyAlias = 1,
    Canonical = 2,
}

public static class ContentDefinitionValidator
{
    public static StatIdValidationStatus GetStatIdStatus(string statId)
    {
        return StatKey.TryResolve(statId, out _, out var isLegacyAlias)
            ? isLegacyAlias ? StatIdValidationStatus.LegacyAlias : StatIdValidationStatus.Canonical
            : StatIdValidationStatus.Unsupported;
    }

    [MenuItem("SM/Validation/Validate Content Definitions")]
    public static void Validate()
    {
        var allAssets = AssetDatabase.FindAssets("t:ScriptableObject", new[] { SampleSeedGenerator.ResourcesRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
            .OfType<ScriptableObject>()
            .ToList();

        var ids = new Dictionary<string, List<string>>();
        var errors = new List<string>();
        var warnings = new List<string>();

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
                    if (skill.CompileTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Skill has empty compile tag: {AssetDatabase.GetAssetPath(skill)}");
                    }
                    if (skill.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Skill has empty rule modifier tag: {AssetDatabase.GetAssetPath(skill)}");
                    }
                    break;
                case AugmentDefinition augment:
                    RegisterId(ids, augment.Id, AssetDatabase.GetAssetPath(augment));
                    ValidateModifiers(errors, warnings, augment.Modifiers, AssetDatabase.GetAssetPath(augment));
                    if (string.IsNullOrWhiteSpace(augment.FamilyId))
                    {
                        errors.Add($"Augment missing family id: {AssetDatabase.GetAssetPath(augment)}");
                    }
                    if (augment.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Augment has empty rule modifier tag: {AssetDatabase.GetAssetPath(augment)}");
                    }
                    if (augment.MutualExclusionTags.Select(tag => tag == null ? string.Empty : tag.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().Count()
                        != augment.MutualExclusionTags.Count(tag => tag != null && !string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Augment has duplicate mutual exclusion tags: {AssetDatabase.GetAssetPath(augment)}");
                    }
                    break;
                case ItemBaseDefinition item:
                    RegisterId(ids, item.Id, AssetDatabase.GetAssetPath(item));
                    ValidateModifiers(errors, warnings, item.BaseModifiers, AssetDatabase.GetAssetPath(item));
                    if (item.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Item has empty rule modifier tag: {AssetDatabase.GetAssetPath(item)}");
                    }
                    break;
                case AffixDefinition affix:
                    RegisterId(ids, affix.Id, AssetDatabase.GetAssetPath(affix));
                    ValidateModifiers(errors, warnings, affix.Modifiers, AssetDatabase.GetAssetPath(affix));
                    break;
                case StableTagDefinition tag:
                    RegisterId(ids, tag.Id, AssetDatabase.GetAssetPath(tag));
                    break;
                case TeamTacticDefinition teamTactic:
                    RegisterId(ids, teamTactic.Id, AssetDatabase.GetAssetPath(teamTactic));
                    break;
                case RoleInstructionDefinition roleInstruction:
                    RegisterId(ids, roleInstruction.Id, AssetDatabase.GetAssetPath(roleInstruction));
                    if (string.IsNullOrWhiteSpace(roleInstruction.RoleTag))
                    {
                        errors.Add($"Role instruction missing role tag: {AssetDatabase.GetAssetPath(roleInstruction)}");
                    }
                    break;
                case PassiveBoardDefinition board:
                    RegisterId(ids, board.Id, AssetDatabase.GetAssetPath(board));
                    if (board.Nodes.Any(node => node == null))
                    {
                        errors.Add($"Passive board has missing node reference: {AssetDatabase.GetAssetPath(board)}");
                    }
                    break;
                case PassiveNodeDefinition passiveNode:
                    RegisterId(ids, passiveNode.Id, AssetDatabase.GetAssetPath(passiveNode));
                    ValidateModifiers(errors, warnings, passiveNode.Modifiers, AssetDatabase.GetAssetPath(passiveNode));
                    if (passiveNode.RuleModifierTags.Any(tag => tag == null || string.IsNullOrWhiteSpace(tag.Id)))
                    {
                        errors.Add($"Passive node has empty rule modifier tag: {AssetDatabase.GetAssetPath(passiveNode)}");
                    }
                    break;
                case SynergyDefinition synergy:
                    RegisterId(ids, synergy.Id, AssetDatabase.GetAssetPath(synergy));
                    if (string.IsNullOrWhiteSpace(synergy.CountedTagId))
                    {
                        errors.Add($"Synergy missing counted tag id: {AssetDatabase.GetAssetPath(synergy)}");
                    }
                    if (synergy.Tiers.Any(tier => tier == null))
                    {
                        errors.Add($"Synergy has missing tier reference: {AssetDatabase.GetAssetPath(synergy)}");
                    }
                    break;
                case SynergyTierDefinition tier:
                    RegisterId(ids, tier.Id, AssetDatabase.GetAssetPath(tier));
                    ValidateModifiers(errors, warnings, tier.Modifiers, AssetDatabase.GetAssetPath(tier));
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
            foreach (var warning in warnings)
            {
                Debug.LogWarning(warning);
            }

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

    private static void ValidateModifiers(
        ICollection<string> errors,
        ICollection<string> warnings,
        IEnumerable<SerializableStatModifier> modifiers,
        string path)
    {
        foreach (var modifier in modifiers)
        {
            switch (GetStatIdStatus(modifier.StatId))
            {
                case StatIdValidationStatus.Unsupported:
                    errors.Add($"Unsupported stat id '{modifier.StatId}': {path}");
                    break;
                case StatIdValidationStatus.LegacyAlias:
                    warnings.Add($"Legacy stat alias '{modifier.StatId}' should migrate to canonical id: {path}");
                    break;
            }
        }
    }
}
