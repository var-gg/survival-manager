using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Meta.Services;

public sealed record RefitResult(
    string OldAffixId,
    string NewAffixId,
    int EchoCost);

/// <summary>
/// Echo-only affix reroll. Rerolls exactly one affix slot,
/// selecting a new affix from the available pool. No full crafting.
/// </summary>
public static class RefitService
{
    public const int BaseEchoCost = 15;

    /// <summary>
    /// Attempts to refit (reroll) a single affix slot.
    /// Returns null if the operation is invalid (out-of-range slot, no candidates).
    /// </summary>
    public static RefitResult? Refit(
        IReadOnlyList<string> currentAffixIds,
        int affixSlotIndex,
        IReadOnlyList<string> availableAffixIds,
        int seed)
    {
        if (currentAffixIds == null || affixSlotIndex < 0 || affixSlotIndex >= currentAffixIds.Count)
        {
            return null;
        }

        var oldAffixId = currentAffixIds[affixSlotIndex];
        var existingAffixes = new HashSet<string>(currentAffixIds, StringComparer.Ordinal);

        var candidates = availableAffixIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Where(id => !string.Equals(id, oldAffixId, StringComparison.Ordinal))
            .Where(id => !existingAffixes.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        var rng = new Random(seed);
        var newAffixId = candidates[rng.Next(candidates.Count)];
        return new RefitResult(oldAffixId, newAffixId, BaseEchoCost);
    }
}
