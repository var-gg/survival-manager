using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed class LootResolutionService
{
    private readonly CombatContentSnapshot _content;
    private readonly Action<DropGradeRollObservation>? _dropGradeObserver;
    private readonly double _heatDropLatentMeanNumerator;
    private readonly double _heatDropJackpotWeightStep;

    public LootResolutionService(CombatContentSnapshot content)
    {
        _content = content;
        _heatDropLatentMeanNumerator = EndlessCycleService.HeatDropLatentMeanNumerator;
        _heatDropJackpotWeightStep = EndlessCycleService.HeatDropJackpotWeightStep;
    }

    internal LootResolutionService(
        CombatContentSnapshot content,
        Action<DropGradeRollObservation> dropGradeObserver)
        : this(
            content,
            dropGradeObserver,
            EndlessCycleService.HeatDropLatentMeanNumerator,
            EndlessCycleService.HeatDropJackpotWeightStep)
    {
    }

    internal LootResolutionService(
        CombatContentSnapshot content,
        Action<DropGradeRollObservation> dropGradeObserver,
        double heatDropLatentMeanNumerator,
        double heatDropJackpotWeightStep)
        : this(content)
    {
        _dropGradeObserver = dropGradeObserver
                              ?? throw new ArgumentNullException(nameof(dropGradeObserver));
        _heatDropLatentMeanNumerator = heatDropLatentMeanNumerator;
        _heatDropJackpotWeightStep = heatDropJackpotWeightStep;
    }

    public bool TryResolveBundle(
        string sourceId,
        int seed,
        out LootBundleResult bundle,
        out OperationFailure? failure,
        int heat = 0)
    {
        return TryResolveBundle(sourceId, seed, Array.Empty<string>(), out bundle, out failure, heat);
    }

    public bool TryResolveBundle(
        string sourceId,
        int seed,
        IReadOnlyList<string> contextTags,
        out LootBundleResult bundle,
        out OperationFailure? failure,
        int heat = 0)
    {
        bundle = null!;
        failure = null;

        if (_content.RewardSources is not { } rewardSources || !_content.RewardSources.TryGetValue(sourceId, out var source))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.LootRewardSourceMissing,
                $"Reward source '{sourceId}' was not found.");
            return false;
        }

        var entries = new List<LootEntry>();
        if (_content.DropTables is { } dropTables && dropTables.TryGetValue(source.DropTableId, out var dropTable))
        {
            entries.AddRange(dropTable.Entries
                .Where(entry => MatchesContext(entry, contextTags))
                .Where(entry => entry.IsGuaranteed)
                .Select(entry => BuildDropTableEntry(dropTable, entry, seed, contextTags, heat: heat)));

            var weightedEntries = dropTable.Entries
                .Where(entry => MatchesContext(entry, contextTags))
                .Where(entry => !entry.IsGuaranteed)
                .ToList();
            if (weightedEntries.Count > 0)
            {
                var selected = SelectWeightedEntry(weightedEntries, seed);
                if (selected != null)
                {
                    entries.Add(BuildDropTableEntry(dropTable, selected, seed, contextTags, heat: heat));
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
        out OperationFailure? failure)
        => TryResolveItemRoll(
            sourceId,
            seed,
            contextTags,
            ItemRarityTierValue.Common,
            out entry,
            out failure);

    public bool TryResolveItemRoll(
        string sourceId,
        int seed,
        IReadOnlyList<string> contextTags,
        ItemRarityTierValue minimumGrade,
        out LootEntry entry,
        out OperationFailure? failure)
    {
        entry = null!;
        failure = null;

        if (_content.RewardSources is not { } rewardSources
            || !rewardSources.TryGetValue(sourceId, out var source))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.LootRewardSourceMissing,
                $"Reward source '{sourceId}' was not found.");
            return false;
        }

        if (_content.DropTables is not { } dropTables
            || !dropTables.TryGetValue(source.DropTableId, out var dropTable))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.LootDropTableMissing,
                $"Drop table '{source.DropTableId}' was not found for reward source '{sourceId}'.");
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
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.LootWeightedItemMissing,
                $"Reward source '{sourceId}' has no eligible weighted item entries.");
            return false;
        }

        entry = BuildDropTableEntry(dropTable, selected, seed, contextTags, minimumGrade);
        return true;
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
        ItemRarityTierValue minimumGrade = ItemRarityTierValue.Common,
        int heat = 0)
    {
        ItemRarityTierValue? grade = null;
        if (entry.RewardType == RewardType.Item)
        {
            var roll = DropGradeEconomy.RollGradeObserved(
                table,
                ResolveChapterId(contextTags),
                entry.RarityBracket,
                seed,
                heat,
                _heatDropLatentMeanNumerator,
                _heatDropJackpotWeightStep);
            _dropGradeObserver?.Invoke(roll);
            var rolledGrade = roll.Grade;
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
