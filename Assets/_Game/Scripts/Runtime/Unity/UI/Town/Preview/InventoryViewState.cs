using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Inventory V1 surface ViewState — pindoc V1 wiki SoT.
/// - currency: Gold / Echo (Profile.Currencies)
/// - 4 category (ALL + weapon/armor/accessory) — item-and-affix-system.md (3 slot)
/// - N×M item grid (ItemDefinition + ItemInstanceRecord) — rarity / weapon family / equipped
/// - 5 affix detail (implicit 1 + prefix 2 + suffix 2) grouped
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
    string GroupKey,            // implicit / prefix / suffix
    string Label,               // "ATK" / "CRIT" 등
    string Value                // "+256" / "+18.7%" 등
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
