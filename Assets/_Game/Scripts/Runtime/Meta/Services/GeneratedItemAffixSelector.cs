using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>Snapshot item metadata만으로 신규 아이템의 결정적 affix id를 고른다.</summary>
public static class GeneratedItemAffixSelector
{
    private const string ImplicitTier = "Implicit";
    private const string PrefixTier = "Prefix";
    private const string SuffixTier = "Suffix";

    public static IReadOnlyList<string> Select(ISessionContentLookup lookup, string itemBaseId, int seed)
    {
        if (lookup == null)
        {
            throw new ArgumentNullException(nameof(lookup));
        }

        if (lookup.Snapshot.ItemCatalog is not { } items
            || !items.TryGetValue(itemBaseId, out var item))
        {
            return Array.Empty<string>();
        }

        var tiers = new List<string>
        {
            ImplicitTier,
            PrefixTier,
        };
        if (item.RarityTier >= ItemRarityTierValue.Rare || item.IdentityKind != ItemIdentityValue.Baseline)
        {
            tiers.Add(SuffixTier);
        }

        if (item.RarityTier >= ItemRarityTierValue.Epic || item.IdentityKind == ItemIdentityValue.Unique)
        {
            tiers.Add(PrefixTier);
        }

        var rng = new Random(seed);
        var selected = new List<string>();
        foreach (var tier in tiers)
        {
            var candidates = lookup.GetCanonicalAffixIds()
                .Where(candidateId => IsCandidate(lookup.Snapshot, item, tier, candidateId, selected))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count > 0)
            {
                selected.Add(candidates[rng.Next(candidates.Count)]);
            }
        }

        return selected;
    }

    private static bool IsCandidate(
        CombatContentSnapshot snapshot,
        ItemTemplate item,
        string tier,
        string candidateId,
        IReadOnlyCollection<string> selectedAffixIds)
    {
        if (string.IsNullOrWhiteSpace(candidateId)
            || snapshot.AffixCatalog is not { } affixes
            || !affixes.TryGetValue(candidateId, out var candidate)
            || !string.Equals(candidate.Tier, tier, StringComparison.Ordinal)
            || candidate.SpawnWeight <= 0f
            || candidate.ItemLevelMin >= 999
            || candidate.AllowedSlotTypes is { Count: > 0 }
                && !candidate.AllowedSlotTypes.Contains(item.SlotType, StringComparer.Ordinal)
            || selectedAffixIds.Contains(candidate.Id, StringComparer.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate.ExclusiveGroupId))
        {
            return true;
        }

        return selectedAffixIds.All(selectedId =>
            !affixes.TryGetValue(selectedId, out var selected)
            || !string.Equals(candidate.ExclusiveGroupId, selected.ExclusiveGroupId, StringComparison.Ordinal));
    }
}
