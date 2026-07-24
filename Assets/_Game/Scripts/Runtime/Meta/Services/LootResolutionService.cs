using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
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
                .Select(entry => BuildDropTableEntry(dropTable, entry, seed, contextTags)));

            var weightedEntries = dropTable.Entries
                .Where(entry => MatchesContext(entry, contextTags))
                .Where(entry => !entry.IsGuaranteed)
                .ToList();
            if (weightedEntries.Count > 0)
            {
                var selected = SelectWeightedEntry(weightedEntries, seed);
                if (selected != null)
                {
                    entries.Add(BuildDropTableEntry(dropTable, selected, seed, contextTags));
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
                .GroupBy(
                    entry => $"{entry.Id}:{entry.RewardType}:{entry.RarityBracket}:{entry.ItemGrade}",
                    StringComparer.Ordinal)
                .Select(group => new LootEntry(
                    group.First().Id,
                    group.First().RewardType,
                    group.Sum(entry => entry.Amount),
                    group.First().RarityBracket,
                    group.First().ItemGrade))
                .OrderBy(entry => entry.RarityBracket)
                .ThenBy(entry => entry.Id, StringComparer.Ordinal)
                .ToList());
        return true;
    }

    /// <summary>
    /// Resolves one repeat-farm item roll from the source's existing weighted drop-table entries.
    /// Guaranteed entries and non-item reward types are intentionally excluded; campaign recovery
    /// owns repeat Gold/Echo separately so their chapter cap cannot leak through automatic loot.
    /// </summary>
    public bool TryResolveItemRoll(
        string sourceId,
        int seed,
        IReadOnlyList<string> contextTags,
        out LootEntry entry,
        out string error)
        => TryResolveItemRoll(
            sourceId,
            seed,
            contextTags,
            ItemRarityTierValue.Common,
            out entry,
            out error);

    public bool TryResolveItemRoll(
        string sourceId,
        int seed,
        IReadOnlyList<string> contextTags,
        ItemRarityTierValue minimumGrade,
        out LootEntry entry,
        out string error)
    {
        entry = null!;
        error = string.Empty;

        if (_content.RewardSources is not { } rewardSources
            || !rewardSources.TryGetValue(sourceId, out var source))
        {
            error = $"Reward source '{sourceId}' not found.";
            return false;
        }

        if (_content.DropTables is not { } dropTables
            || !dropTables.TryGetValue(source.DropTableId, out var dropTable))
        {
            error = $"Drop table '{source.DropTableId}' not found.";
            return false;
        }

        var itemEntries = dropTable.Entries
            .Where(candidate => !candidate.IsGuaranteed)
            .Where(candidate => candidate.RewardType == RewardType.Item)
            .Where(candidate => MatchesContext(candidate, contextTags))
            .ToArray();
        var selected = SelectWeightedEntry(itemEntries, seed);
        if (selected == null)
        {
            error = $"Reward source '{sourceId}' has no eligible weighted item entries.";
            return false;
        }

        entry = BuildDropTableEntry(dropTable, selected, seed, contextTags, minimumGrade);
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

    private LootEntry BuildDropTableEntry(
        DropTableTemplate table,
        LootBundleEntryTemplate entry,
        int seed,
        IReadOnlyList<string> contextTags,
        ItemRarityTierValue minimumGrade = ItemRarityTierValue.Common)
    {
        ItemRarityTierValue? grade = null;
        if (entry.RewardType == RewardType.Item)
        {
            var rolledGrade = DropGradeEconomy.RollGrade(
                table,
                ResolveChapterId(contextTags),
                entry.RarityBracket,
                seed);
            grade = rolledGrade < minimumGrade ? minimumGrade : rolledGrade;
        }

        return new LootEntry(
            entry.Id,
            entry.RewardType,
            entry.Amount,
            entry.RarityBracket,
            grade);
    }

    private string ResolveChapterId(IReadOnlyList<string> contextTags)
    {
        if (_content.CampaignChapters != null)
        {
            foreach (var tag in contextTags)
            {
                if (_content.CampaignChapters.ContainsKey(tag))
                {
                    return tag;
                }
            }
        }

        if (_content.ExpeditionSites != null)
        {
            foreach (var tag in contextTags)
            {
                if (_content.ExpeditionSites.TryGetValue(tag, out var site))
                {
                    return site.ChapterId;
                }
            }
        }

        return string.Empty;
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
