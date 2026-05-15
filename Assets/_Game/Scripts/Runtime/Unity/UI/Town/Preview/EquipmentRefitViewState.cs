using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit V1 surface ViewState — runtime 모델 정합 (audit §4.1 P1-2).
/// 모델: item-centric refit — pool에서 item 선택 → affix 목록 확인 → 1개 affix reroll.
///
/// - ✅ pool = Profile.Inventory · RarityKey = ItemBaseDefinition.RarityTier · SlotKey = ItemBaseDefinition.SlotType ·
///   refit = RefitItem(itemInstanceId, affixSlotIndex)
/// - ◐ GroupKey = AffixDefinition.Tier (Implicit/Prefix/Suffix) · Name = AffixDefinition.NameKey resolved
/// - ❌ affix instance **확정 roll 값 미저장** — InventoryItemRecord.AffixIds는 definition id 리스트뿐 →
///   ValueRange는 AffixDefinition.ValueMin~ValueMax **범위** 표기 (가짜 rolled value 아님)
/// - ⚑ hero 컨텍스트 = InventoryItemRecord.EquippedHeroId 파생 (장착된 hero — refit은 item-centric이라 hero 불필요)
/// </summary>
public sealed record EquipmentRefitAffixRowViewState(
    string AffixId,              // click identity ← AffixDefinition.Id
    string GroupKey,             // implicit / prefix / suffix ← AffixDefinition.Tier
    string Name,                 // ← AffixDefinition.NameKey resolved
    string ValueRange,           // ← AffixDefinition.ValueMin~ValueMax (instance roll 미저장 — 범위)
    Texture2D? IconSprite,
    bool IsSelectedForReroll
);

public sealed record EquipmentRefitPoolRowViewState(
    string ItemInstanceId,
    string Name,                 // ← ItemBaseDefinition.NameKey resolved
    string SlotKey,              // weapon / armor / accessory ← ItemBaseDefinition.SlotType
    Texture2D? IconSprite,
    string RarityKey,            // common / rare / epic ← ItemBaseDefinition.RarityTier
    bool IsSelected
);

public sealed record EquipmentRefitViewState(
    string SelectedItemName,         // 선택 item 이름 (좌측 컨텍스트)
    string SelectedItemSlotLabel,    // Weapon / Armor / Accessory
    string SelectedItemRarityKey,    // common / rare / epic
    string EquippedHeroLabel,        // "장착: {hero}" 또는 "미장착" ← EquippedHeroId
    Texture2D? EquippedHeroPortrait, // EquippedHeroId의 portrait (미장착이면 null)
    Texture2D? EchoSprite,
    int RefitCost,                   // 15 Echo (RefitItem 고정 — RefitEchoCost)
    IReadOnlyList<EquipmentRefitAffixRowViewState> Affixes,
    IReadOnlyList<EquipmentRefitPoolRowViewState> Pool
);
