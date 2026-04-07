using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class LootResolutionService
{
    private readonly CombatContentSnapshot _content;

    public LootResolutionService(CombatContentSnapshot content)
    {
        _content = content;
    }

    public bool TryResolveBundle(string sourceId, int seed, out LootBundleResult bundle, out string error)
    {
        return TryResolveBundle(sourceId, seed, Array.Empty<string>(), out bundle, out error);
    }

    public bool TryResolveBundle(string sourceId, int seed, IReadOnlyList<string> contextTags, out LootBundleResult bundle, out string error)
    {
        bundle = null!;
        error = string.Empty;

        if (_content.RewardSources is not { } rewardSources || !_content.RewardSources.TryGetValue(sourceId, out var source))
        {
            error = $"Reward source '{sourceId}' not found.";
            return false;
        }

        var entries = new List<LootEntry>();
        if (_content.DropTables is { } dropTables && dropTables.TryGetValue(source.DropTableId, out var dropTable))
        {
            entries.AddRange(dropTable.Entries
                .Where(entry => MatchesContext(entry, contextTags))
                .Where(entry => entry.IsGuaranteed)
                .Select(entry => new LootEntry(entry.Id, entry.RewardType, entry.Amount, entry.RarityBracket)));

            var weightedEntries = dropTable.Entries
                .Where(entry => MatchesContext(entry, contextTags))
                .Where(entry => !entry.IsGuaranteed)
                .ToList();
            if (weightedEntries.Count > 0)
            {
                var selected = SelectWeightedEntry(weightedEntries, seed);
                if (selected != null)
                {
                    entries.Add(new LootEntry(selected.Id, selected.RewardType, selected.Amount, selected.RarityBracket));
                }
            }
        }

        if (_content.LootBundles is { } lootBundles)
        {
            foreach (var template in lootBundles.Values.Where(definition =>
                         string.Equals(definition.RewardSourceId, sourceId, StringComparison.Ordinal)))
            {
                entries.AddRange(template.Entries
                    .Where(entry => MatchesContext(entry, contextTags))
                    .Select(entry => new LootEntry(entry.Id, entry.RewardType, entry.Amount, entry.RarityBracket)));
            }
        }

        bundle = new LootBundleResult(
            sourceId,
            source.Kind.ToString(),
            entries
                .GroupBy(entry => $"{entry.Id}:{entry.RewardType}:{entry.RarityBracket}", StringComparer.Ordinal)
                .Select(group => new LootEntry(
                    group.First().Id,
                    group.First().RewardType,
                    group.Sum(entry => entry.Amount),
                    group.First().RarityBracket))
                .OrderBy(entry => entry.RarityBracket)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .ToList());
        return true;
    }

    public static string FormatSummary(LootBundleResult bundle)
    {
        if (bundle.Entries.Count == 0)
        {
            return "No automatic loot.";
        }

        return string.Join(", ", bundle.Entries.Select(entry => $"{entry.Id} x{entry.Amount}"));
    }

    private static bool MatchesContext(LootBundleEntryTemplate entry, IReadOnlyList<string> contextTags)
    {
        if (entry.RequiredContextTags == null || entry.RequiredContextTags.Count == 0)
        {
            return true;
        }

        if (contextTags == null || contextTags.Count == 0)
        {
            return false;
        }

        var available = contextTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToHashSet(StringComparer.Ordinal);
        return entry.RequiredContextTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .All(available.Contains);
    }

    private static LootBundleEntryTemplate? SelectWeightedEntry(IReadOnlyList<LootBundleEntryTemplate> entries, int seed)
    {
        var totalWeight = entries.Sum(entry => Math.Max(1, entry.Weight));
        if (totalWeight <= 0)
        {
            return null;
        }

        var random = new Random(seed);
        var roll = random.Next(0, totalWeight);
        var cursor = 0;
        foreach (var entry in entries)
        {
            cursor += Math.Max(1, entry.Weight);
            if (roll < cursor)
            {
                return entry;
            }
        }

        return entries.LastOrDefault();
    }
}
