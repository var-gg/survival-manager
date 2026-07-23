using System;
using System.Collections.Generic;
using System.Linq;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Editor.Validation;

internal static class CampaignEquipmentUpgradePolicy
{
    internal static void Apply(
        GameSessionState session,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex)
    {
        var candidates = session.Profile.Inventory
            .Select((item, acquisitionIndex) => (Item: item, AcquisitionIndex: acquisitionIndex))
            .Where(entry => string.IsNullOrEmpty(entry.Item.EquippedHeroId))
            .Where(entry => itemIndex.ContainsKey(entry.Item.ItemBaseId))
            .OrderByDescending(entry => ResolveGrade(entry.Item, itemIndex[entry.Item.ItemBaseId]))
            .ThenBy(entry => entry.Item.ItemBaseId, StringComparer.Ordinal)
            .ThenBy(entry => entry.AcquisitionIndex)
            .Select(entry => entry.Item)
            .ToArray();
        foreach (var candidate in candidates)
        {
            var candidateMeta = itemIndex[candidate.ItemBaseId];
            foreach (var hero in session.Profile.Heroes)
            {
                if (candidateMeta.AllowedClassIds.Count > 0
                    && !candidateMeta.AllowedClassIds.Contains(hero.ClassId))
                {
                    continue;
                }

                var equipped = ResolveEquippedInSlot(
                    session,
                    hero.EquippedItemIds,
                    candidateMeta.SlotType,
                    itemIndex);
                if (equipped == null)
                {
                    if (session.EquipItem(hero.HeroId, candidate.ItemInstanceId).IsSuccess)
                    {
                        break;
                    }

                    continue;
                }

                if (ResolveGrade(candidate, candidateMeta)
                    <= ResolveGrade(equipped, itemIndex[equipped.ItemBaseId]))
                {
                    continue;
                }

                var unequip = session.UnequipItem(hero.HeroId, equipped.ItemInstanceId);
                if (!unequip.IsSuccess)
                {
                    continue;
                }

                if (session.EquipItem(hero.HeroId, candidate.ItemInstanceId).IsSuccess)
                {
                    break;
                }

                session.EquipItem(hero.HeroId, equipped.ItemInstanceId);
            }
        }
    }

    private static InventoryItemRecord? ResolveEquippedInSlot(
        GameSessionState session,
        IReadOnlyList<string> equippedItemIds,
        SM.Content.Definitions.ItemSlotType slotType,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex)
    {
        foreach (var instanceId in equippedItemIds)
        {
            var item = session.Profile.Inventory.FirstOrDefault(candidate =>
                string.Equals(candidate.ItemInstanceId, instanceId, StringComparison.Ordinal));
            if (item != null
                && itemIndex.TryGetValue(item.ItemBaseId, out var meta)
                && meta.SlotType == slotType)
            {
                return item;
            }
        }

        return null;
    }

    private static int ResolveGrade(
        InventoryItemRecord item,
        CampaignBalanceSweepRunner.ItemMeta meta)
    {
        return item.RolledRarityTier >= 0
            ? item.RolledRarityTier
            : (int)meta.RarityTier;
    }
}
