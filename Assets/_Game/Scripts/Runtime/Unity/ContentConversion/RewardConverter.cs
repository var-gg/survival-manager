using System;
using System.Linq;
using SM.Content.Definitions;
using SM.Meta.Model;
using static SM.Unity.ContentConversion.ContentConversionShared;

namespace SM.Unity.ContentConversion;

internal static class RewardConverter
{
    internal static RewardSourceTemplate BuildRewardSourceTemplate(RewardSourceDefinition definition)
    {
        return new RewardSourceTemplate(
            definition.Id,
            ResolveLegacyName(definition.NameKey, definition.LegacyDisplayName, definition.Id),
            definition.Kind,
            definition.DropTableId,
            definition.UsesRewardCards,
            Enumerate(definition.AllowedRarityBrackets)
                .Distinct()
                .ToList());
    }

    internal static DropTableTemplate BuildDropTableTemplate(DropTableDefinition definition)
    {
        return new DropTableTemplate(
            definition.Id,
            definition.RewardSourceId,
            Enumerate(definition.Entries)
                .Where(entry => entry != null)
                .Select(BuildLootBundleEntryTemplate)
                .ToList());
    }

    internal static LootBundleTemplate BuildLootBundleTemplate(LootBundleDefinition definition)
    {
        return new LootBundleTemplate(
            definition.Id,
            definition.RewardSourceId,
            Enumerate(definition.Entries)
                .Where(entry => entry != null)
                .Select(BuildLootBundleEntryTemplate)
                .ToList());
    }

    internal static TraitTokenTemplate BuildTraitTokenTemplate(TraitTokenDefinition definition)
    {
        return new TraitTokenTemplate(
            definition.Id,
            definition.RewardType);
    }

    internal static LootBundleEntryTemplate BuildLootBundleEntryTemplate(LootBundleEntryDefinition definition)
    {
        return new LootBundleEntryTemplate(
            string.IsNullOrWhiteSpace(definition.Id) ? $"{definition.RewardType}:{definition.RarityBracket}:{definition.Amount}" : definition.Id,
            definition.RewardType,
            Math.Max(1, definition.Amount),
            definition.RarityBracket,
            Math.Max(1, definition.Weight),
            definition.IsGuaranteed);
    }
}
