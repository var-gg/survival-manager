using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class AugmentOfferService
{
    public static IReadOnlyList<AugmentCatalogEntry> BuildOffer(
        IReadOnlyDictionary<string, AugmentCatalogEntry> catalog,
        IReadOnlyCollection<string> activeTags,
        IReadOnlyCollection<string> permanentEquippedAugmentIds,
        int maxChoices = 3)
    {
        var entries = catalog.Values
            .Where(entry => !permanentEquippedAugmentIds.Contains(entry.Id))
            .Where(entry => !entry.SuppressIfPermanentEquipped || !permanentEquippedAugmentIds.Any(id => catalog.TryGetValue(id, out var existing) && existing.FamilyId == entry.FamilyId))
            .OrderByDescending(entry => Score(entry, activeTags))
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        var chosen = new List<AugmentCatalogEntry>();
        foreach (var entry in entries)
        {
            if (chosen.Count >= maxChoices)
            {
                break;
            }

            if (chosen.Any(existing => existing.FamilyId == entry.FamilyId))
            {
                continue;
            }

            if (entry.MutualExclusionTags.Any(tag => activeTags.Contains(tag))
                || chosen.SelectMany(existing => existing.MutualExclusionTags).Any(tag => entry.Tags.Contains(tag)))
            {
                continue;
            }

            chosen.Add(entry);
        }

        return chosen;
    }

    private static float Score(AugmentCatalogEntry entry, IReadOnlyCollection<string> activeTags)
    {
        var score = entry.Tier * 10f;
        score += entry.Tags.Count(tag => activeTags.Contains(tag)) * 5f;
        score += entry.Category switch
        {
            "synergy" => 2f,
            "economy_loot" => 1f,
            "run_utility" => 0.5f,
            _ => 3f,
        };
        return score;
    }
}
