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
        => ApplyAndMeasure(
            session,
            itemIndex,
            revisitIndex: 0,
            Array.Empty<string>());

    internal static CampaignGradeAdoptionFunnelObservation ApplyAndMeasure(
        GameSessionState session,
        IReadOnlyDictionary<string, CampaignBalanceSweepRunner.ItemMeta> itemIndex,
        int revisitIndex,
        IReadOnlyCollection<string> observedItemInstanceIds)
    {
        var observedIds = observedItemInstanceIds.ToHashSet(StringComparer.Ordinal);
        var observations = new List<CampaignGradeAdoptionRollObservation>(observedIds.Count);
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
            var candidateGrade = ResolveGrade(candidate, candidateMeta);
            var eligibleHeroCount = 0;
            var strictlyBetter = false;
            var equippedHeroId = string.Empty;
            var bestUpgradeDelta = int.MinValue;
            foreach (var hero in session.Profile.Heroes)
            {
                if (candidateMeta.AllowedClassIds.Count > 0
                    && !candidateMeta.AllowedClassIds.Contains(hero.ClassId))
                {
                    continue;
                }

                eligibleHeroCount++;
                var equipped = ResolveEquippedInSlot(
                    session,
                    hero.EquippedItemIds,
                    candidateMeta.SlotType,
                    itemIndex);
                if (equipped == null)
                {
                    strictlyBetter = true;
                    bestUpgradeDelta = Math.Max(bestUpgradeDelta, candidateGrade + 1);
                    if (session.EquipItem(hero.HeroId, candidate.ItemInstanceId).IsSuccess)
                    {
                        equippedHeroId = hero.HeroId;
                        break;
                    }

                    continue;
                }

                var equippedGrade = ResolveGrade(equipped, itemIndex[equipped.ItemBaseId]);
                var upgradeDelta = candidateGrade - equippedGrade;
                bestUpgradeDelta = Math.Max(bestUpgradeDelta, upgradeDelta);
                if (upgradeDelta <= 0)
                {
                    continue;
                }

                strictlyBetter = true;
                var unequip = session.UnequipItem(hero.HeroId, equipped.ItemInstanceId);
                if (!unequip.IsSuccess)
                {
                    continue;
                }

                if (session.EquipItem(hero.HeroId, candidate.ItemInstanceId).IsSuccess)
                {
                    equippedHeroId = hero.HeroId;
                    break;
                }

                session.EquipItem(hero.HeroId, equipped.ItemInstanceId);
            }

            if (observedIds.Contains(candidate.ItemInstanceId))
            {
                observations.Add(new CampaignGradeAdoptionRollObservation(
                    revisitIndex,
                    candidate.ItemInstanceId,
                    candidate.ItemBaseId,
                    candidateMeta.SlotType.ToString(),
                    candidateGrade,
                    eligibleHeroCount,
                    eligibleHeroCount > 0,
                    strictlyBetter,
                    !string.IsNullOrWhiteSpace(equippedHeroId),
                    equippedHeroId,
                    bestUpgradeDelta == int.MinValue ? 0 : bestUpgradeDelta));
            }
        }

        var ordered = observations
            .OrderBy(value => value.ItemInstanceId, StringComparer.Ordinal)
            .ToArray();
        return new CampaignGradeAdoptionFunnelObservation(
            ordered.Length,
            ordered.Count(value => value.SlotEligible),
            ordered.Count(value => value.StrictlyBetter),
            ordered.Count(value => value.Equipped),
            ordered);
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
