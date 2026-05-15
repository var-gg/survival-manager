using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Inventory V1 surface ViewState — runtime 모델 정합 (audit §4.1 P1-3).
/// - currency: Gold / Echo — CurrencyRecord 8종 중 V1 활성 경제 2종만 노출
/// - 4 category (ALL + weapon/armor/accessory) — ItemSlotType 3종 + ALL
/// - N×M item grid (InventoryItemRecord + ItemBaseDefinition) — rarity / weapon family / equipped
/// - affix detail: 이름 + 값 범위 (AffixDefinition.NameKey / ValueMin~ValueMax —
///   instance 확정 roll 미저장이라 범위 표기). sell 액션은 runtime API 부재로 제거.
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
    string IconKey,             // affix sprite proxy
    string RarityKey,           // common / rare / epic
    string WeaponFamilyKey,     // shield / blade / bow / focus
    string WeaponFamilyLabel,   // 방패 / 검 / 활 / 매개체
    bool IsEquipped,
    Texture2D? IconSprite
);

public sealed record InventoryAffixRowViewState(
    string GroupKey,            // implicit / prefix / suffix ← AffixDefinition.Tier
    string Name,                // ← AffixDefinition.NameKey resolved
    string ValueRange           // ← AffixDefinition.ValueMin~ValueMax (instance 확정 roll 미저장 → 범위)
);

public sealed record InventoryDetailViewState(
    string ItemInstanceId,
    Texture2D? IconSprite,
    IReadOnlyList<InventoryAffixRowViewState> Affixes
);

public sealed record InventoryViewState(
    long Gold,
    long Echo,
    Texture2D? GoldSprite,
    Texture2D? EchoSprite,
    IReadOnlyList<InventoryCategoryViewState> Categories,
    IReadOnlyList<InventoryItemViewState> Items,
    InventoryDetailViewState? Detail
);
