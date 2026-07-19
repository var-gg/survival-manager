using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Unity.ContentConversion;

/// <summary>Unity authored catalogs의 legacy/canonical 우선순위를 pure snapshot record로 낮춘다.</summary>
internal static class SessionContentOrderConverter
{
    internal static SessionContentOrder Build(
        IReadOnlyDictionary<string, UnitArchetypeDefinition> archetypes,
        IReadOnlyDictionary<string, ItemBaseDefinition> items,
        IReadOnlyDictionary<string, AffixDefinition> affixes,
        IReadOnlyDictionary<string, AugmentDefinition> augments,
        IReadOnlyDictionary<string, PassiveBoardDefinition> passiveBoards,
        IReadOnlyDictionary<string, SynergyDefinition> synergies,
        FirstPlayableSliceDefinition? firstPlayableSlice)
    {
        var archetypeIds = ContentFallbackData.CanonicalArchetypeOrder
            .Where(archetypes.ContainsKey)
            .ToList();
        if (archetypeIds.Count == 0)
        {
            archetypeIds.AddRange(archetypes.Keys.OrderBy(id => id, StringComparer.Ordinal));
        }

        var itemIds = ContentFallbackData.LegacyItemFallbackOrder
            .Where(items.ContainsKey)
            .ToList();
        itemIds.AddRange(items.Keys
            .Where(id => !itemIds.Contains(id, StringComparer.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal));

        var affixIds = firstPlayableSlice?.AffixIds.Count > 0
            ? firstPlayableSlice.AffixIds.Where(affixes.ContainsKey).OrderBy(id => id, StringComparer.Ordinal).ToList()
            : affixes.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();

        var temporaryAugmentIds = ContentFallbackData.LegacyAugmentFallbackOrder
            .Where(id => augments.TryGetValue(id, out var augment) && !augment.IsPermanent)
            .ToList();
        temporaryAugmentIds.AddRange(augments.Values
            .Where(augment => !augment.IsPermanent && !temporaryAugmentIds.Contains(augment.Id, StringComparer.Ordinal))
            .OrderBy(augment => augment.Id, StringComparer.Ordinal)
            .Select(augment => augment.Id));
        if (firstPlayableSlice?.AugmentIds.Count > 0)
        {
            temporaryAugmentIds = temporaryAugmentIds
                .Where(id => firstPlayableSlice.AugmentIds.Contains(id, StringComparer.Ordinal))
                .ToList();
        }

        var permanentAugmentIds = firstPlayableSlice?.PermanentAugmentIds.Count > 0
            ? firstPlayableSlice.PermanentAugmentIds
                .Where(id => augments.TryGetValue(id, out var augment) && augment.IsPermanent)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList()
            : augments.Values
                .Where(augment => augment.IsPermanent && !string.IsNullOrWhiteSpace(augment.Id))
                .OrderBy(augment => augment.Id, StringComparer.Ordinal)
                .Select(augment => augment.Id)
                .ToList();

        var passiveBoardIds = firstPlayableSlice?.PassiveBoardIds.Count > 0
            ? firstPlayableSlice.PassiveBoardIds.Where(passiveBoards.ContainsKey).OrderBy(id => id, StringComparer.Ordinal).ToList()
            : passiveBoards.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();
        var synergyFamilyIds = firstPlayableSlice?.SynergyFamilyIds.Count > 0
            ? firstPlayableSlice.SynergyFamilyIds.Where(synergies.ContainsKey).OrderBy(id => id, StringComparer.Ordinal).ToList()
            : synergies.Keys.OrderBy(id => id, StringComparer.Ordinal).ToList();

        return new SessionContentOrder(
            archetypeIds,
            itemIds,
            affixIds,
            temporaryAugmentIds,
            permanentAugmentIds,
            passiveBoardIds,
            synergyFamilyIds);
    }
}
