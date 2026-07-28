using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Inventory V1 surface ViewState — runtime 모델 정합 (audit §4.1 P1-3).
/// - currency: Gold / Echo — CurrencyRecord 8종 중 V1 활성 경제 2종만 노출
/// - 4 category (ALL + weapon/armor/accessory) — ItemSlotType 3종 + ALL
/// - N×M item grid (InventoryItemRecord + ItemBaseDefinition) — rarity / weapon family / equipped
/// - affix detail: 이름 + instance roll로 scaling된 modifier별 effect + labelled roll quality.
///   legacy save는 definition package와 "legacy baseline"을 표시한다. sell 액션은 runtime API 부재로 제거.
/// </summary>
public sealed record InventoryCategoryViewState(
    string Key,                 // all / weapon / armor / accessory
    string Label,               // "ALL" / "WEAPON" 등
    string Count,               // "48/100"
    Texture2D? IconSprite,
    bool IsSelected
);

public sealed record InventoryItemViewState(
    string ItemInstanceId,      // 식별자
    string Name,
    string MetaLabel,
    string IconKey,             // item icon id / item base fallback
    string RarityKey,           // common / rare / epic
    string RawRarityKey,        // common / magic / rare / epic / legendary compatibility
    string SlotKey,             // weapon / armor / accessory / item
    string SlotLabel,           // 무기 / 방어구 / 장신구
    string WeaponFamilyKey,     // shield / blade / bow / focus
    string WeaponFamilyLabel,   // 방패 / 검 / 활 / 매개체
    string IdentityKey,         // baseline / named / unique
    string IdentityLabel,       // NAMED / SIGNATURE
    bool ShowsIdentityBadge,
    bool CanRefit,
    bool IsLaunchSupportedRarity,
    bool IsEquipped,
    bool IsSelected,
    Texture2D? IconSprite,
    // wave-30 GPT Pro patch: lock/protected/incompatible safety state.
    // IsLocked = 다른 hero에 장착됨(분해/이동 제약), IsProtected = 현재 선택 hero에 장착됨, IsIncompatible = slot 호환 안 됨.
    bool IsLocked = false,
    bool IsProtected = false,
    bool IsIncompatible = false,
    string MetaTooltip = ""
);

public sealed record InventoryAffixRowViewState(
    string GroupKey,            // implicit / prefix / suffix ← AffixDefinition.Tier
    string Name,                // ← AffixDefinition.NameKey resolved
    IReadOnlyList<string> EffectLines, // ← scaled modifier별 localized stat + signed value
    string RollContextText      // ← labelled roll quality, 또는 legacy baseline
);

public sealed record InventoryDetailViewState(
    string ItemInstanceId,
    Texture2D? IconSprite,
    IReadOnlyList<InventoryAffixRowViewState> Affixes,
    string Name = "",
    string SlotKey = "",
    string SlotLabel = "",
    string RarityKey = "",
    string RawRarityKey = "",
    string WeaponFamilyKey = "",
    string WeaponFamilyLabel = "",
    string IdentityKey = "",
    string IdentityLabel = "",
    bool ShowsIdentityBadge = false,
    bool CanRefit = false,
    bool IsLaunchSupportedRarity = true,
    string SetBonusTier = "",
    IReadOnlyList<string>? CrossLinks = null
);

public sealed record InventoryCompareRowViewState(
    string Key,
    string Label,
    string SelectedValue,
    string EquippedValue,
    string ToneKey,
    // wave-30 GPT Pro patch: delta chip — Presenter가 +N/-N/= visual signal 공급.
    // DeltaChipText 비어있으면 chip 안 보임. DeltaChipKind = "gain"/"same"/"loss" → USS class.
    string DeltaChipText = "",
    string DeltaChipKind = "same"
);

public sealed record InventoryEquipCtaViewState(
    bool CanEquip,
    string Label,
    string StatusText,
    string TooltipText
);

public sealed record InventoryCompareLaneViewState(
    string TargetHeroId,
    string TargetHeroLabel,
    string TargetHeroMetaLabel,
    string SelectedItemLabel,
    string SelectedItemMetaLabel,
    Texture2D? SelectedItemIconSprite,
    string EquippedItemLabel,
    string EquippedItemMetaLabel,
    Texture2D? EquippedItemIconSprite,
    string EquippedOwnerLabel,
    IReadOnlyList<InventoryCompareRowViewState> Rows,
    InventoryEquipCtaViewState EquipCta
);

public sealed record InventoryViewState(
    long Gold,
    long Echo,
    Texture2D? GoldSprite,
    Texture2D? EchoSprite,
    IReadOnlyList<InventoryCategoryViewState> Categories,
    IReadOnlyList<InventoryItemViewState> Items,
    InventoryDetailViewState? Detail,
    InventoryCompareLaneViewState? Compare
);
