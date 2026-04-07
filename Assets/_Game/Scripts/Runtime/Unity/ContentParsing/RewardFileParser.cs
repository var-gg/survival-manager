using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Core.Stats;
using UnityEngine;
using static SM.Unity.ContentParsing.YamlFieldExtractor;

namespace SM.Unity.ContentParsing;

internal static class RewardFileParser
{
    internal static IReadOnlyList<RewardSourceDefinition> LoadRewardSources()
    {
        return RuntimeCombatContentFileParser.LoadAssets("RewardSources", path =>
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

    internal static IReadOnlyList<DropTableDefinition> LoadDropTables()
    {
        return RuntimeCombatContentFileParser.LoadAssets("DropTables", path =>
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

    internal static IReadOnlyList<LootBundleDefinition> LoadLootBundles()
    {
        return RuntimeCombatContentFileParser.LoadAssets("LootBundles", path =>
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

    internal static Dictionary<string, RewardTableDefinition> LoadRewardTables()
    {
        return RuntimeCombatContentFileParser.LoadAssets("Rewards", path =>
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

    internal static List<LootBundleEntryDefinition> ParseLootEntries(string[] lines)
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
                else if (trimmed.StartsWith("RequiredContextTags:", StringComparison.Ordinal))
                {
                    entry.RequiredContextTags = ParseNestedStringList(lines, ref index);
                }
            }

            result.Add(entry);
        }

        return result;
    }

    internal static List<RewardEntry> ParseRewardEntries(string[] lines)
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

    internal static void ApplyRewardTableFallbacks(RewardTableDefinition definition)
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

    internal static void ApplyRewardSourceFallbacks(RewardSourceDefinition definition)
    {
        definition.DropTableId = Coalesce(definition.DropTableId, definition.Id.Replace("reward_source_", "drop_table_", StringComparison.Ordinal));
    }

    internal static void ApplyDropTableFallbacks(DropTableDefinition definition)
    {
        definition.RewardSourceId = Coalesce(definition.RewardSourceId, definition.Id.Replace("drop_table_", "reward_source_", StringComparison.Ordinal));
    }

    internal static void ApplyLootBundleFallbacks(LootBundleDefinition definition)
    {
        definition.RewardSourceId = Coalesce(definition.RewardSourceId, "reward_source_extract");
    }

    private static List<string> ParseNestedStringList(IReadOnlyList<string> lines, ref int index)
    {
        var values = new List<string>();
        var parentIndent = GetIndent(lines[index]);
        while (index + 1 < lines.Count)
        {
            var nextIndex = index + 1;
            var nextLine = lines[nextIndex];
            var nextTrimmed = nextLine.Trim();
            var nextIndent = GetIndent(nextLine);
            if (nextIndent <= parentIndent || !nextTrimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                break;
            }

            values.Add(nextTrimmed[2..].Trim());
            index = nextIndex;
        }

        return values;
    }
}
