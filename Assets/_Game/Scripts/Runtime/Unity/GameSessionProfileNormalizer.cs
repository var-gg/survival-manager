using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

internal static class GameSessionProfileNormalizer
{
    internal static int NormalizePermanentAugments(SaveProfile profile, ICombatContentLookup combatContentLookup)
    {
        profile.UnlockedPermanentAugmentIds = NormalizePermanentAugmentUnlockIds(
            profile.UnlockedPermanentAugmentIds,
            combatContentLookup);

        foreach (var loadout in profile.PermanentAugmentLoadouts)
        {
            loadout.EquippedAugmentIds = NormalizePermanentAugmentLoadout(
                loadout.EquippedAugmentIds,
                profile.UnlockedPermanentAugmentIds,
                combatContentLookup);
        }

        return MetaBalanceDefaults.MaxPermanentAugmentSlots;
    }

    private static List<string> NormalizePermanentAugmentUnlockIds(
        IEnumerable<string> unlockedAugmentIds,
        ICombatContentLookup combatContentLookup)
    {
        return unlockedAugmentIds
            .Where(IsValidPermanentAugmentId)
            .Where(id => !combatContentLookup.TryGetAugmentDefinition(id, out var augment) || augment.IsPermanent)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static List<string> NormalizePermanentAugmentLoadout(
        IEnumerable<string> equippedAugmentIds,
        List<string> unlockedAugmentIds,
        ICombatContentLookup combatContentLookup)
    {
        var validEquippedAugmentIds = equippedAugmentIds
            .Where(IsValidPermanentAugmentId)
            .Where(id => !combatContentLookup.TryGetAugmentDefinition(id, out var augment) || augment.IsPermanent)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var equippedAugmentId in validEquippedAugmentIds)
        {
            if (!unlockedAugmentIds.Contains(equippedAugmentId, StringComparer.Ordinal))
            {
                unlockedAugmentIds.Add(equippedAugmentId);
            }
        }

        return validEquippedAugmentIds
            .Take(MetaBalanceDefaults.MaxPermanentAugmentSlots)
            .ToList();
    }

    private static bool IsValidPermanentAugmentId(string id)
    {
        return !string.IsNullOrWhiteSpace(id)
               && !id.StartsWith("perm-slot", StringComparison.Ordinal)
               && !id.StartsWith("perm-slot.", StringComparison.Ordinal);
    }
}
