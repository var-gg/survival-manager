using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

public sealed partial class GameSessionState
{
    private InventoryItemRecord CreateGeneratedInventoryItem(
        string itemBaseId,
        string itemInstanceId = "",
        string equippedHeroId = "")
    {
        var resolvedInstanceId = string.IsNullOrWhiteSpace(itemInstanceId)
            ? $"{itemBaseId}-{Guid.NewGuid():N}"
            : itemInstanceId;
        var seed = BuildStableSeed(resolvedInstanceId, Profile.Inventory.Count);

        return new InventoryItemRecord
        {
            ItemInstanceId = resolvedInstanceId,
            ItemBaseId = itemBaseId,
            EquippedHeroId = equippedHeroId,
            AffixIds = BuildGeneratedAffixIds(itemBaseId, seed),
        };
    }

    /// <summary>
    /// PlayMode 진입 시 SeedDemoProfile이 한 번에 4 hero에게만 item을 주므로, Equipment Refit / Inventory
    /// 같이 inventory를 사용하는 panel이 dev/preview 진입 시 텅 빈 상태가 된다. 이 helper는 inventory가
    /// 비어있을 때만 모든 hero에 baseline item을 한 개씩 채워준다 (affix 자동 생성 path 사용).
    /// production gameplay에서는 SeedDemoProfile 또는 reward path가 inventory를 미리 채우므로 no-op.
    /// </summary>
    public void SeedDevDemoInventoryIfEmpty()
    {
        if (Profile.Inventory.Count > 0)
        {
            return;
        }

        var itemIds = _combatContentLookup.GetCanonicalItemIds();
        if (itemIds.Count == 0 || Profile.Heroes.Count == 0)
        {
            return;
        }

        for (var i = 0; i < Profile.Heroes.Count; i++)
        {
            var hero = Profile.Heroes[i];
            hero.EquippedItemIds ??= new List<string>();
            var itemBaseId = itemIds[i % itemIds.Count];
            var instanceId = $"dev-item-{i + 1:D2}";
            var record = CreateGeneratedInventoryItem(itemBaseId, instanceId, hero.HeroId);
            Profile.Inventory.Add(record);
            if (!hero.EquippedItemIds.Any(id => string.Equals(id, instanceId, StringComparison.Ordinal)))
            {
                hero.EquippedItemIds.Add(instanceId);
            }
        }
    }

    private IReadOnlyList<string> BuildRefitCandidateAffixIds(InventoryItemRecord item, int affixSlotIndex)
    {
        if (item.AffixIds == null
            || affixSlotIndex < 0
            || affixSlotIndex >= item.AffixIds.Count
            || !_combatContentLookup.TryGetItemDefinition(item.ItemBaseId, out var itemDefinition)
            || !_combatContentLookup.TryGetAffixDefinition(item.AffixIds[affixSlotIndex], out var oldAffix))
        {
            var slice = _combatContentLookup.GetFirstPlayableSlice();
            return slice?.AffixIds ?? Array.Empty<string>();
        }

        if (oldAffix.Tier == AffixTierValue.Implicit)
        {
            return Array.Empty<string>();
        }

        var otherAffixIds = item.AffixIds
            .Where((_, index) => index != affixSlotIndex)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToList();

        return _combatContentLookup.GetCanonicalAffixIds()
            .Where(candidateId => IsRefitCandidate(itemDefinition, oldAffix, candidateId, otherAffixIds))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    private List<string> BuildGeneratedAffixIds(string itemBaseId, int seed)
    {
        if (!_combatContentLookup.TryGetItemDefinition(itemBaseId, out var itemDefinition))
        {
            return new List<string>();
        }

        var tiers = new List<AffixTierValue>
        {
            AffixTierValue.Implicit,
            AffixTierValue.Prefix,
        };

        if (itemDefinition.RarityTier >= ItemRarityTierValue.Rare || itemDefinition.IdentityKind != ItemIdentityValue.Baseline)
        {
            tiers.Add(AffixTierValue.Suffix);
        }

        if (itemDefinition.RarityTier >= ItemRarityTierValue.Epic || itemDefinition.IdentityKind == ItemIdentityValue.Unique)
        {
            tiers.Add(AffixTierValue.Prefix);
        }

        var rng = new Random(seed);
        var selected = new List<string>();
        foreach (var tier in tiers)
        {
            var candidates = _combatContentLookup.GetCanonicalAffixIds()
                .Where(candidateId => IsGeneratedAffixCandidate(itemDefinition, tier, candidateId, selected))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            selected.Add(candidates[rng.Next(candidates.Count)]);
        }

        return selected;
    }

    private bool IsRefitCandidate(
        ItemBaseDefinition itemDefinition,
        AffixDefinition oldAffix,
        string candidateId,
        IReadOnlyList<string> otherAffixIds)
    {
        if (string.IsNullOrWhiteSpace(candidateId)
            || !_combatContentLookup.TryGetAffixDefinition(candidateId, out var candidate)
            || string.Equals(candidate.Id, oldAffix.Id, StringComparison.Ordinal)
            || candidate.Tier != oldAffix.Tier
            || !IsLiveAffix(candidate)
            || !IsAffixCompatibleWithItem(itemDefinition, candidate)
            || otherAffixIds.Contains(candidate.Id, StringComparer.Ordinal))
        {
            return false;
        }

        return !HasExclusiveGroupConflict(candidate, otherAffixIds);
    }

    private bool IsGeneratedAffixCandidate(
        ItemBaseDefinition itemDefinition,
        AffixTierValue tier,
        string candidateId,
        IReadOnlyList<string> selectedAffixIds)
    {
        if (string.IsNullOrWhiteSpace(candidateId)
            || !_combatContentLookup.TryGetAffixDefinition(candidateId, out var candidate)
            || candidate.Tier != tier
            || !IsLiveAffix(candidate)
            || !IsAffixCompatibleWithItem(itemDefinition, candidate)
            || selectedAffixIds.Contains(candidate.Id, StringComparer.Ordinal))
        {
            return false;
        }

        return !HasExclusiveGroupConflict(candidate, selectedAffixIds);
    }

    private static bool IsLiveAffix(AffixDefinition affix)
    {
        return affix.SpawnWeight > 0f && affix.ItemLevelMin < 999;
    }

    private static bool IsAffixCompatibleWithItem(ItemBaseDefinition itemDefinition, AffixDefinition affix)
    {
        return affix.AllowedSlotTypes.Count == 0 || affix.AllowedSlotTypes.Contains(itemDefinition.SlotType);
    }

    private bool HasExclusiveGroupConflict(AffixDefinition candidate, IReadOnlyList<string> selectedAffixIds)
    {
        if (string.IsNullOrWhiteSpace(candidate.ExclusiveGroupId))
        {
            return false;
        }

        foreach (var selectedAffixId in selectedAffixIds)
        {
            if (!_combatContentLookup.TryGetAffixDefinition(selectedAffixId, out var selectedAffix))
            {
                continue;
            }

            if (string.Equals(candidate.ExclusiveGroupId, selectedAffix.ExclusiveGroupId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
