using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// Generated item affix selection의 canonical candidate topology와 natural traversal을 소유한다.
/// Quality profile compiler는 같은 candidate ordering/filtering을 fixed-point state로 순회한다.
/// </summary>
internal sealed class GeneratedItemAffixStateGraph
{
    internal const string ImplicitTier = "Implicit";
    internal const string PrefixTier = "Prefix";
    internal const string SuffixTier = "Suffix";

    private readonly IReadOnlyList<Candidate> _candidates;

    private GeneratedItemAffixStateGraph(ItemTemplate item, IReadOnlyList<Candidate> candidates)
    {
        Item = item;
        _candidates = candidates;
    }

    internal ItemTemplate Item { get; }
    internal IReadOnlyList<Candidate> Candidates => _candidates;

    internal static bool TryCreate(
        ISessionContentLookup lookup,
        string itemBaseId,
        out GeneratedItemAffixStateGraph graph)
    {
        graph = null!;
        if (lookup.Snapshot.ItemCatalog is not { } items
            || !items.TryGetValue(itemBaseId, out var item))
        {
            return false;
        }

        graph = new GeneratedItemAffixStateGraph(item, BuildCandidateCatalog(lookup, item));
        return true;
    }

    internal IReadOnlyList<string> SelectLegacyNatural(int seed)
    {
        var tiers = new List<string>
        {
            ImplicitTier,
            PrefixTier,
        };
        if (Item.RarityTier >= SM.Core.Content.ItemRarityTierValue.Rare
            || Item.IdentityKind != SM.Core.Content.ItemIdentityValue.Baseline)
        {
            tiers.Add(SuffixTier);
        }

        if (Item.RarityTier >= SM.Core.Content.ItemRarityTierValue.Epic
            || Item.IdentityKind == SM.Core.Content.ItemIdentityValue.Unique)
        {
            tiers.Add(PrefixTier);
        }

        var random = new Random(seed);
        var selected = new List<string>();
        var selectedMask = BigInteger.Zero;
        var occupiedExclusiveGroups = BigInteger.Zero;
        foreach (var tier in tiers)
        {
            var candidates = GetCandidates(tier, selectedMask, occupiedExclusiveGroups);
            if (candidates.Count == 0)
            {
                continue;
            }

            var chosen = candidates[random.Next(candidates.Count)];
            Select(chosen, selected, ref selectedMask, ref occupiedExclusiveGroups);
        }

        return selected;
    }

    internal IReadOnlyList<string> SelectGeneratedNatural(
        int seed,
        SM.Core.Content.ItemRarityTierValue rolledGrade,
        float gradeStepBudgetScore)
    {
        var targetBudget = Math.Max(0.01f, gradeStepBudgetScore);
        var random = new Random(seed);
        var selected = new List<string>();
        var selectedMask = BigInteger.Zero;
        var occupiedExclusiveGroups = BigInteger.Zero;

        SelectOne(
            ImplicitTier,
            targetBudget,
            random,
            selected,
            ref selectedMask,
            ref occupiedExclusiveGroups);

        foreach (var tier in GetGradeStepTiers(rolledGrade))
        {
            var stepBudget = ResolveDiscreteStepBudget(targetBudget, random);
            var accumulatedBudget = 0f;
            while (accumulatedBudget < stepBudget)
            {
                var candidates = GetCandidates(tier, selectedMask, occupiedExclusiveGroups);
                var chosen = SelectBudgetWeighted(
                    candidates,
                    Math.Max(0.01f, stepBudget - accumulatedBudget),
                    random);
                if (chosen == null)
                {
                    break;
                }

                Select(chosen, selected, ref selectedMask, ref occupiedExclusiveGroups);
                accumulatedBudget += Math.Max(0.01f, chosen.Template.BudgetScore);
            }
        }

        return selected;
    }

    internal IReadOnlyList<Candidate> GetCandidates(
        string tier,
        BigInteger selectedMask,
        BigInteger occupiedExclusiveGroups)
    {
        var result = new List<Candidate>();
        foreach (var candidate in _candidates)
        {
            if (!string.Equals(candidate.Template.Tier, tier, StringComparison.Ordinal)
                || (selectedMask & candidate.IdMask) != BigInteger.Zero
                || candidate.ExclusiveGroupMask != BigInteger.Zero
                    && (occupiedExclusiveGroups & candidate.ExclusiveGroupMask) != BigInteger.Zero)
            {
                continue;
            }

            result.Add(candidate);
        }

        return result;
    }

    internal static IReadOnlyList<string> GetGradeStepTiers(
        SM.Core.Content.ItemRarityTierValue rolledGrade)
    {
        return rolledGrade switch
        {
            SM.Core.Content.ItemRarityTierValue.Common => Array.Empty<string>(),
            SM.Core.Content.ItemRarityTierValue.Magic => new[] { PrefixTier },
            SM.Core.Content.ItemRarityTierValue.Rare => new[] { PrefixTier, SuffixTier },
            SM.Core.Content.ItemRarityTierValue.Epic => new[] { PrefixTier, SuffixTier, PrefixTier },
            _ => new[] { PrefixTier, SuffixTier, PrefixTier, SuffixTier },
        };
    }

    private static IReadOnlyList<Candidate> BuildCandidateCatalog(
        ISessionContentLookup lookup,
        ItemTemplate item)
    {
        if (lookup.Snapshot.AffixCatalog is not { } affixes)
        {
            return Array.Empty<Candidate>();
        }

        var templates = lookup.GetCanonicalAffixIds()
            .Where(candidateId =>
                !string.IsNullOrWhiteSpace(candidateId)
                && affixes.TryGetValue(candidateId, out var candidate)
                && candidate.SpawnWeight > 0f
                && candidate.ItemLevelMin < 999
                && (candidate.AllowedSlotTypes is not { Count: > 0 }
                    || candidate.AllowedSlotTypes.Contains(item.SlotType, StringComparer.Ordinal))
                && (string.IsNullOrWhiteSpace(item.AffixPoolTag)
                    || candidate.CompileTags.Contains(item.AffixPoolTag, StringComparer.Ordinal)))
            .Select(candidateId => affixes[candidateId])
            .OrderBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();

        var idMasks = templates
            .Select(candidate => candidate.Id)
            .Distinct(StringComparer.Ordinal)
            .Select((id, index) => (id, mask: BigInteger.One << index))
            .ToDictionary(entry => entry.id, entry => entry.mask, StringComparer.Ordinal);
        var exclusiveGroupMasks = templates
            .Select(candidate => candidate.ExclusiveGroupId)
            .Where(groupId => !string.IsNullOrWhiteSpace(groupId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(groupId => groupId, StringComparer.Ordinal)
            .Select((groupId, index) => (groupId, mask: BigInteger.One << index))
            .ToDictionary(entry => entry.groupId, entry => entry.mask, StringComparer.Ordinal);

        return templates
            .Select((template, ordinal) => new Candidate(
                ordinal,
                template,
                idMasks[template.Id],
                string.IsNullOrWhiteSpace(template.ExclusiveGroupId)
                    ? BigInteger.Zero
                    : exclusiveGroupMasks[template.ExclusiveGroupId]))
            .ToArray();
    }

    private static float ResolveDiscreteStepBudget(float targetBudget, Random random)
    {
        var lower = Math.Max(1, (int)Math.Floor(targetBudget));
        var fraction = targetBudget - lower;
        return fraction > 0f && random.NextDouble() < fraction
            ? lower + 1
            : lower;
    }

    private void SelectOne(
        string tier,
        float targetBudget,
        Random random,
        List<string> selected,
        ref BigInteger selectedMask,
        ref BigInteger occupiedExclusiveGroups)
    {
        var candidates = GetCandidates(tier, selectedMask, occupiedExclusiveGroups);
        var chosen = SelectBudgetWeighted(candidates, targetBudget, random);
        if (chosen != null)
        {
            Select(chosen, selected, ref selectedMask, ref occupiedExclusiveGroups);
        }
    }

    private static Candidate? SelectBudgetWeighted(
        IReadOnlyList<Candidate> candidates,
        float targetBudget,
        Random random)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        var weights = candidates
            .Select(candidate =>
                Math.Max(0.0001d, candidate.Template.SpawnWeight)
                / (1d + Math.Abs(candidate.Template.BudgetScore - targetBudget)))
            .ToArray();
        return candidates[SelectWeightedIndex(weights, random)];
    }

    internal static int SelectWeightedIndex(
        IReadOnlyList<double> weights,
        Random random)
    {
        var total = weights.Sum();
        var roll = random.NextDouble() * total;
        var cursor = 0d;
        for (var index = 0; index < weights.Count; index++)
        {
            cursor += weights[index];
            if (roll < cursor)
            {
                return index;
            }
        }

        return weights.Count - 1;
    }

    private static void Select(
        Candidate candidate,
        ICollection<string> selected,
        ref BigInteger selectedMask,
        ref BigInteger occupiedExclusiveGroups)
    {
        selected.Add(candidate.Template.Id);
        selectedMask |= candidate.IdMask;
        occupiedExclusiveGroups |= candidate.ExclusiveGroupMask;
    }

    internal sealed record Candidate(
        int Ordinal,
        AffixTemplate Template,
        BigInteger IdMask,
        BigInteger ExclusiveGroupMask);
}
