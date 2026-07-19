using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity;

internal sealed class SessionInventoryItemBuilder
{
    private readonly ICombatContentLookup _lookup;
    private readonly ISessionContentLookup _sessionLookup;
    private readonly Func<string, int, int> _buildStableSeed;

    internal SessionInventoryItemBuilder(ICombatContentLookup lookup, Func<string, int, int> buildStableSeed)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        _sessionLookup = lookup;
        _buildStableSeed = buildStableSeed ?? throw new ArgumentNullException(nameof(buildStableSeed));
    }

    internal InventoryItemRecord CreateGeneratedInventoryItem(
        SaveProfile profile,
        string itemBaseId,
        string itemInstanceId = "",
        string equippedHeroId = "")
    {
        var resolvedInstanceId = itemInstanceId;
        if (string.IsNullOrWhiteSpace(resolvedInstanceId))
        {
            profile.ItemInstanceCounter = checked(profile.ItemInstanceCounter + 1L);
            resolvedInstanceId = $"{itemBaseId}-i{profile.ItemInstanceCounter.ToString(CultureInfo.InvariantCulture)}";
        }

        // 어픽스 롤 시드는 인스턴스 id가 아니라 결정적 컨텍스트(base id + 획득 순번)에서 파생 —
        // 같은 진행은 같은 드랍이어야 결정성 원칙(cross-process byte-identical)과 측정 재현성(sweep 곡선)이
        // 성립한다. GUID가 시드에 스미면 동일 세이브·동일 전투 시드에서도 run마다 어픽스가 리롤된다
        // (sweep 2회전 실측: 무변경 재실행에서 곡선이 wolfpine부터 분기 — GUID hero id 허구와 동일 계열).
        // 인스턴스 id 자체는 profile에 영속되는 생성 순번으로 발급하되, 어픽스 시드와는 계속 분리한다.
        var seed = _buildStableSeed($"{itemBaseId}|{profile.Inventory.Count}", profile.Inventory.Count);

        return new InventoryItemRecord
        {
            ItemInstanceId = resolvedInstanceId,
            ItemBaseId = itemBaseId,
            EquippedHeroId = equippedHeroId,
            AffixIds = GeneratedItemAffixSelector.Select(_sessionLookup, itemBaseId, seed).ToList(),
        };
    }

    internal IReadOnlyList<string> BuildRefitCandidateAffixIds(InventoryItemRecord item, int affixSlotIndex)
    {
        if (item.AffixIds == null
            || affixSlotIndex < 0
            || affixSlotIndex >= item.AffixIds.Count
            || !_lookup.TryGetItemDefinition(item.ItemBaseId, out var itemDefinition)
            || !_lookup.TryGetAffixDefinition(item.AffixIds[affixSlotIndex], out var oldAffix))
        {
            var slice = _lookup.GetFirstPlayableSlice();
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

        return _lookup.GetCanonicalAffixIds()
            .Where(candidateId => IsRefitCandidate(itemDefinition, oldAffix, candidateId, otherAffixIds))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    internal void EnsureAffixPadding(InventoryItemRecord record, string itemBaseId, int targetCount)
    {
        record.AffixIds ??= new List<string>();
        if (record.AffixIds.Count >= targetCount) return;
        if (!_lookup.TryGetItemDefinition(itemBaseId, out var itemDefinition)) return;

        var existing = new HashSet<string>(record.AffixIds, StringComparer.Ordinal);
        foreach (var tier in new[] { AffixTierValue.Prefix, AffixTierValue.Suffix, AffixTierValue.Implicit })
        {
            if (record.AffixIds.Count >= targetCount) break;
            var candidates = _lookup.GetCanonicalAffixIds()
                .Where(id => !existing.Contains(id))
                .Where(id => IsGeneratedAffixCandidate(itemDefinition, tier, id, record.AffixIds))
                .ToList();
            foreach (var candidate in candidates)
            {
                if (record.AffixIds.Count >= targetCount) break;
                record.AffixIds.Add(candidate);
                existing.Add(candidate);
            }
        }
    }

    private bool IsRefitCandidate(
        ItemBaseDefinition itemDefinition,
        AffixDefinition oldAffix,
        string candidateId,
        IReadOnlyList<string> otherAffixIds)
    {
        if (string.IsNullOrWhiteSpace(candidateId)
            || !_lookup.TryGetAffixDefinition(candidateId, out var candidate)
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
            || !_lookup.TryGetAffixDefinition(candidateId, out var candidate)
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
            if (!_lookup.TryGetAffixDefinition(selectedAffixId, out var selectedAffix))
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
