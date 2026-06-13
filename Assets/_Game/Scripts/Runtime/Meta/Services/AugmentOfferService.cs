using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class AugmentOfferService
{
    private const string StatLightTag = "stat_light";

    private static readonly HashSet<string> BoundOfferTags = new(StringComparer.Ordinal)
    {
        "hero_bound",
        "role_bound",
        "class_bound",
        "synergy_bound",
    };

    public static IReadOnlyList<AugmentCatalogEntry> BuildOffer(
        IReadOnlyDictionary<string, AugmentCatalogEntry> catalog,
        IReadOnlyCollection<string> activeTags,
        IReadOnlyCollection<string> permanentEquippedAugmentIds,
        int maxChoices = 3)
    {
        return BuildOffer(
            catalog,
            new AugmentOfferContext(
                string.Empty,
                activeTags,
                Array.Empty<string>(),
                permanentEquippedAugmentIds,
                Seed: 0,
                PickIndex: 0,
                MaxChoices: maxChoices));
    }

    public static IReadOnlyList<AugmentCatalogEntry> BuildOffer(
        IReadOnlyDictionary<string, AugmentCatalogEntry> catalog,
        AugmentOfferContext context)
    {
        if (catalog.Count == 0 || context.MaxChoices <= 0)
        {
            return Array.Empty<AugmentCatalogEntry>();
        }

        var activeTags = ToSet(context.ActiveTags);
        var acquiredAugmentIds = ToSet(context.AcquiredAugmentIds);
        var permanentEquippedAugmentIds = ToSet(context.PermanentEquippedAugmentIds);
        var permanentEquippedFamilies = permanentEquippedAugmentIds
            .Select(id => catalog.TryGetValue(id, out var existing) ? existing.FamilyId : string.Empty)
            .Where(familyId => !string.IsNullOrWhiteSpace(familyId))
            .ToHashSet(StringComparer.Ordinal);

        var entries = catalog.Values
            .Where(entry => IsEligible(entry, activeTags, acquiredAugmentIds, permanentEquippedAugmentIds, permanentEquippedFamilies))
            .OrderByDescending(entry => Score(entry, activeTags, context.PreferredBucket, context.Seed, context.PickIndex))
            .ThenBy(entry => entry.Id, StringComparer.Ordinal)
            .ToList();

        var chosen = new List<AugmentCatalogEntry>();
        var statLightCount = 0;
        var maxStatLightChoices = Math.Max(0, context.MaxStatLightChoices);
        foreach (var entry in entries)
        {
            if (chosen.Count >= context.MaxChoices)
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

            if (HasTag(entry, StatLightTag) && statLightCount >= maxStatLightChoices)
            {
                continue;
            }

            chosen.Add(entry);
            if (HasTag(entry, StatLightTag))
            {
                statLightCount++;
            }
        }

        return chosen;
    }

    private static bool IsEligible(
        AugmentCatalogEntry entry,
        ISet<string> activeTags,
        ISet<string> acquiredAugmentIds,
        ISet<string> permanentEquippedAugmentIds,
        ISet<string> permanentEquippedFamilies)
    {
        if (entry.IsPermanent
            || acquiredAugmentIds.Contains(entry.Id)
            || permanentEquippedAugmentIds.Contains(entry.Id))
        {
            return false;
        }

        if (entry.SuppressIfPermanentEquipped && permanentEquippedFamilies.Contains(entry.FamilyId))
        {
            return false;
        }

        return !IsDeadBoundOffer(entry, activeTags);
    }

    private static float Score(AugmentCatalogEntry entry, ISet<string> activeTags, string preferredBucket, int seed, int pickIndex)
    {
        var score = entry.Tier * 10f;
        score += entry.Tags.Count(activeTags.Contains) * 5f;
        score += (entry.BuildBiasTags ?? Array.Empty<string>()).Count(activeTags.Contains) * 6f;
        if (!string.IsNullOrWhiteSpace(preferredBucket)
            && string.Equals(entry.OfferBucket, preferredBucket, StringComparison.Ordinal))
        {
            score += 25f;
        }

        score += entry.Category switch
        {
            "synergy" => 2f,
            "economy_loot" => 1f,
            "run_utility" => 0.5f,
            _ => 3f,
        };
        score += StableJitter(entry.Id, seed, pickIndex);
        return score;
    }

    private static bool IsDeadBoundOffer(AugmentCatalogEntry entry, ISet<string> activeTags)
    {
        var tags = EnumerateOfferTags(entry).ToList();
        if (!tags.Any(BoundOfferTags.Contains))
        {
            return false;
        }

        return !tags.Any(tag => !BoundOfferTags.Contains(tag) && activeTags.Contains(tag));
    }

    private static bool HasTag(AugmentCatalogEntry entry, string tag) =>
        entry.Tags.Any(candidate => string.Equals(candidate, tag, StringComparison.Ordinal))
        || (entry.BuildBiasTags ?? Array.Empty<string>()).Any(candidate => string.Equals(candidate, tag, StringComparison.Ordinal));

    private static IEnumerable<string> EnumerateOfferTags(AugmentCatalogEntry entry)
    {
        foreach (var tag in entry.Tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                yield return tag;
            }
        }

        foreach (var tag in entry.BuildBiasTags ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                yield return tag;
            }
        }
    }

    private static HashSet<string> ToSet(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal)
        ?? new HashSet<string>(StringComparer.Ordinal);

    private static float StableJitter(string id, int seed, int pickIndex)
    {
        unchecked
        {
            var hash = 2166136261u;
            hash = Mix(hash, seed);
            hash = Mix(hash, pickIndex);
            foreach (var ch in id ?? string.Empty)
            {
                hash ^= ch;
                hash *= 16777619u;
            }

            return (hash % 1000) / 1000f;
        }
    }

    private static uint Mix(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            return hash * 16777619u;
        }
    }
}
