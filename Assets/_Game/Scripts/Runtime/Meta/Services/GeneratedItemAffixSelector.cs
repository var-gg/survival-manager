using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;

namespace SM.Meta.Services;

/// <summary>Snapshot item metadata만으로 신규 아이템의 결정적 affix id를 고른다.</summary>
public static class GeneratedItemAffixSelector
{
    public static IReadOnlyList<string> GetEligibleAffixIds(
        ISessionContentLookup lookup,
        string itemBaseId)
    {
        if (lookup == null)
        {
            throw new ArgumentNullException(nameof(lookup));
        }

        return GeneratedItemAffixStateGraph.TryCreate(lookup, itemBaseId, out var graph)
            ? graph.Candidates.Select(candidate => candidate.Template.Id).ToArray()
            : Array.Empty<string>();
    }

    public static IReadOnlyList<string> Select(ISessionContentLookup lookup, string itemBaseId, int seed)
    {
        if (lookup == null)
        {
            throw new ArgumentNullException(nameof(lookup));
        }

        if (!GeneratedItemAffixStateGraph.TryCreate(lookup, itemBaseId, out var graph))
        {
            return Array.Empty<string>();
        }

        return graph.SelectLegacyNatural(seed);
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

        if (!GeneratedItemAffixStateGraph.TryCreate(lookup, itemBaseId, out var graph))
        {
            return Array.Empty<string>();
        }

        // Common keeps the existing one-implicit baseline. GradeStepBudgetScore applies only to
        // the four increments above Common, so increasing it does not silently raise every drop's
        // intercept and the mean-preserving grade calibration can still hold first-clear power.
        return graph.SelectGeneratedNatural(seed, rolledGrade, gradeStepBudgetScore);
    }
}
