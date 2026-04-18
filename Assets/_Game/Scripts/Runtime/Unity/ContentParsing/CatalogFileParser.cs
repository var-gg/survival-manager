using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class CatalogFileParser
{
    internal static Dictionary<string, TeamTacticDefinition> LoadTeamTactics(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return RuntimeCombatContentFileParser.LoadAssets("TeamTactics", path =>
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

    internal static Dictionary<string, RoleInstructionDefinition> LoadRoleInstructions(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return RuntimeCombatContentFileParser.LoadAssets("RoleInstructions", path =>
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

    internal static Dictionary<string, PassiveNodeDefinition> LoadPassiveNodes(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, StableTagDefinition> stableTags)
    {
        return RuntimeCombatContentFileParser.LoadAssets("PassiveNodes", path =>
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

    internal static Dictionary<string, PassiveBoardDefinition> LoadPassiveBoards(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, PassiveNodeDefinition> passiveNodes)
    {
        return RuntimeCombatContentFileParser.LoadAssets("PassiveBoards", path =>
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

    internal static Dictionary<string, SynergyTierDefinition> LoadSynergyTiers()
    {
        return RuntimeCombatContentFileParser.LoadAssets("Synergies", path =>
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
            return definition;
        }).Where(pair => pair.Value != null).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    internal static Dictionary<string, SynergyDefinition> LoadSynergies(
        IReadOnlyDictionary<string, string> guidToPath,
        IReadOnlyDictionary<string, SynergyTierDefinition> tiers)
    {
        return RuntimeCombatContentFileParser.LoadAssets("Synergies", path =>
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

    internal static void ApplySynergyTierFallbacks(SynergyTierDefinition definition, string assetPath)
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

        if (definition.BudgetCard != null && definition.BudgetCard.Vector != null && definition.BudgetCard.Vector.FinalScore > 0)
        {
            if (definition.Threshold is 3 or 4)
            {
                definition.BudgetCard.PowerBand = PowerBand.Major;
                AdjustBudgetFinalScore(definition.BudgetCard.Vector, 18);
            }
            else if (definition.Threshold == 2)
            {
                AdjustBudgetFinalScore(definition.BudgetCard.Vector, 12);
            }

            return;
        }

        var target = definition.Threshold is 3 or 4 ? 18 : 12;
        var vector = definition.Threshold is 3 or 4
            ? MakeBudgetVector(6, 2, 4, 2, 0, 2, 0, 2)
            : MakeBudgetVector(3, 1, 3, 1, 0, 2, 0, 2);
        AdjustBudgetFinalScore(vector, target);
        definition.BudgetCard = BuildBudgetCard(BudgetDomain.SynergyBreakpoint, ContentRarity.Common, definition.Threshold is 3 or 4 ? PowerBand.Major : PowerBand.Standard, CombatRoleBudgetProfile.None, vector, 2, 1, 0, Array.Empty<ThreatPattern>(), Array.Empty<CounterToolContribution>());
    }

    internal static void ApplySynergyFallbacks(SynergyDefinition definition)
    {
        definition.CountedTagId = Coalesce(definition.CountedTagId, definition.Id.Replace("synergy_", string.Empty, StringComparison.Ordinal));
        var expectedMajorThreshold = IsClassSynergy(definition.CountedTagId) ? 3 : 4;
        definition.Tiers = definition.Tiers
            .Where(tier => tier != null && (tier.Threshold == 2 || tier.Threshold == expectedMajorThreshold))
            .OrderBy(tier => tier.Threshold)
            .ToList();
    }

    private static bool IsClassSynergy(string countedTagId)
    {
        return countedTagId is "vanguard" or "duelist" or "ranger" or "mystic";
    }
}
