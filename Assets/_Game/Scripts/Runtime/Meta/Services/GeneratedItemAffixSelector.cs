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

    public static IReadOnlyList<string> Select(
        ISessionContentLookup lookup,
        string itemBaseId,
        int seed,
        ItemRarityTierValue rolledGrade,
        float gradeStepBudgetScore)
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

        var gradeStepTiers = rolledGrade switch
        {
            ItemRarityTierValue.Common => Array.Empty<string>(),
            ItemRarityTierValue.Magic => new[] { PrefixTier },
            ItemRarityTierValue.Rare => new[] { PrefixTier, SuffixTier },
            ItemRarityTierValue.Epic => new[] { PrefixTier, SuffixTier, PrefixTier },
            _ => new[] { PrefixTier, SuffixTier, PrefixTier, SuffixTier },
        };
        var targetBudget = Math.Max(0.01f, gradeStepBudgetScore);
        var rng = new Random(seed);
        var selected = new List<string>();

        // Common keeps the existing one-implicit baseline. GradeStepBudgetScore applies only to
        // the four increments above Common, so increasing it does not silently raise every drop's
        // intercept and the mean-preserving grade calibration can still hold first-clear power.
        SelectOne(lookup, item, ImplicitTier, targetBudget, rng, selected);
        foreach (var tier in gradeStepTiers)
        {
            var stepBudget = ResolveDiscreteStepBudget(targetBudget, rng);
            var accumulatedBudget = 0f;
            while (accumulatedBudget < stepBudget)
            {
                var candidates = BuildCandidates(lookup, item, tier, selected);
                var chosen = SelectBudgetWeighted(
                    candidates,
                    Math.Max(0.01f, stepBudget - accumulatedBudget),
                    rng);
                if (chosen == null)
                {
                    break;
                }

                selected.Add(chosen.Id);
                accumulatedBudget += Math.Max(0.01f, chosen.BudgetScore);
            }
        }

        return selected;
    }

    private static float ResolveDiscreteStepBudget(float targetBudget, Random random)
    {
        var lower = Math.Max(1, (int)Math.Floor(targetBudget));
        var fraction = targetBudget - lower;
        return fraction > 0f && random.NextDouble() < fraction
            ? lower + 1
            : lower;
    }

    private static void SelectOne(
        ISessionContentLookup lookup,
        ItemTemplate item,
        string tier,
        float targetBudget,
        Random random,
        List<string> selected)
    {
        var candidates = BuildCandidates(lookup, item, tier, selected);
        var chosen = SelectBudgetWeighted(candidates, targetBudget, random);
        if (chosen != null)
        {
            selected.Add(chosen.Id);
        }
    }

    private static IReadOnlyList<AffixTemplate> BuildCandidates(
        ISessionContentLookup lookup,
        ItemTemplate item,
        string tier,
        IReadOnlyCollection<string> selected)
    {
        return lookup.GetCanonicalAffixIds()
            .Where(candidateId => IsCandidate(lookup.Snapshot, item, tier, candidateId, selected))
            .Select(candidateId => lookup.Snapshot.AffixCatalog![candidateId])
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToList();
    }

    private static AffixTemplate? SelectBudgetWeighted(
        IReadOnlyList<AffixTemplate> candidates,
        float targetBudget,
        Random random)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var weights = candidates
            .Select(candidate =>
                Math.Max(0.0001d, candidate.SpawnWeight)
                / (1d + Math.Abs(candidate.BudgetScore - targetBudget)))
            .ToArray();
        var total = weights.Sum();
        var roll = random.NextDouble() * total;
        var cursor = 0d;
        for (var index = 0; index < candidates.Count; index++)
        {
            cursor += weights[index];
            if (roll < cursor)
            {
                return candidates[index];
            }
        }

        return candidates[^1];
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
