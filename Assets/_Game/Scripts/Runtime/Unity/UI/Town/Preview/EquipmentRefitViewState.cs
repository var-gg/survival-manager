using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit V1 surface ViewState — runtime 모델 정합 (audit §4.1 P1-2).
/// 모델: item-centric refit — pool에서 item 선택 → quality percentile/next floor 확인 → 전체 affix 재선택.
///
/// - ✅ pool = Profile.Inventory · RarityKey = ItemBaseDefinition.RarityTier · SlotKey = ItemBaseDefinition.SlotType ·
///   refit = RefitItem(itemInstanceId)
/// - ◐ GroupKey = AffixDefinition.Tier (Implicit/Prefix/Suffix) · Name = AffixDefinition.NameKey resolved
/// - ✅ affix instance magnitude = InventoryItemRecord.AffixMagnitudeRolls. legacy save는 definition modifier 값.
///   MagnitudeText는 실제 값 + range 내 percentile + min/max context를 함께 표기한다.
/// - ⚑ hero 컨텍스트 = InventoryItemRecord.EquippedHeroId 파생 (장착된 hero — refit은 item-centric이라 hero 불필요)
/// </summary>
public sealed record EquipmentRefitAffixRowViewState(
    string AffixId,              // click identity ← AffixDefinition.Id
    string GroupKey,             // implicit / prefix / suffix ← AffixDefinition.Tier (스타일 후크 전용)
    string GroupLabel,           // 화면에 나가는 계층 머리글 ← EquipmentRefitText.AffixGroupHeader
    string CategoryKey,          // offenseflat / utility / ...
    string Name,                 // ← AffixDefinition.NameKey resolved
    string MagnitudeText,        // ← persisted magnitude + percentile + AffixDefinition.ValueMin~ValueMax
    Texture2D? IconSprite,
    bool IsLocked = false,
    bool LockToggleEnabled = false,
    string LockLabel = ""
);

public sealed record EquipmentRefitPoolRowViewState(
    string ItemInstanceId,
    string Name,                 // ← ItemBaseDefinition.NameKey resolved
    string SlotKey,              // weapon / armor / accessory ← ItemBaseDefinition.SlotType
    string SlotLabel,
    string FamilyKey,
    string FamilyLabel,
    Texture2D? IconSprite,
    string RarityKey,            // common / rare / epic ← ItemBaseDefinition.RarityTier
    string RawRarityKey,
    string IdentityKey,
    string IdentityLabel,
    bool ShowsIdentityBadge,
    bool CanRefit,
    bool IsLaunchSupportedRarity,
    bool IsSelected
);

public sealed record EquipmentRefitViewState(
    string SelectedItemName,         // 선택 item 이름 (좌측 컨텍스트)
    string SelectedItemSlotLabel,    // Weapon / Armor / Accessory
    string SelectedItemRarityKey,    // common / rare / epic
    string SelectedItemFamilyKey,
    string SelectedItemFamilyLabel,
    string SelectedItemIdentityKey,
    string SelectedItemIdentityLabel,
    bool SelectedItemShowsIdentityBadge,
    bool SelectedItemCanRefit,
    string EquippedHeroLabel,        // "장착: {hero}" 또는 "미장착" ← EquippedHeroId
    Texture2D? EquippedHeroPortrait, // EquippedHeroId의 portrait (미장착이면 null)
    Texture2D? EchoSprite,
    double CurrentQualityPercent,
    double NextFloorPercent,
    int RefitCost,
    bool RefitMaxed,
    string RefitStatusMessage,
    IReadOnlyList<EquipmentRefitAffixRowViewState> Affixes,
    IReadOnlyList<EquipmentRefitPoolRowViewState> Pool,
    CraftOperationKindValue SelectedOperation = CraftOperationKindValue.Reforge,
    bool ReforgeOperationSelectable = false,
    bool SealOperationSelectable = false,
    string SealOperationReason = "",
    bool SelectedOperationCanPurchase = false,
    int SelectedOperationCost = 0,
    string SelectedOperationCostLabel = "",
    string SelectedOperationStatusMessage = "",
    bool ConfirmationVisible = false,
    string PanelTitle = "",
    string OperationSelectorLabel = "",
    string ReforgeOperationLabel = "",
    string SealOperationLabel = "",
    string CraftActionLabel = "",
    string ConfirmationMessage = "",
    string ConfirmLabel = "",
    string CancelLabel = ""
);
