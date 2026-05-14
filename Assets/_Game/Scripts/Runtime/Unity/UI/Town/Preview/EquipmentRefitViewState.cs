using System.Collections.Generic;
using UnityEngine;

namespace SM.Unity.UI.Town.Preview;

/// <summary>
/// Equipment Refit V1 surface ViewState — pindoc V1 wiki SoT.
/// item-and-affix-system.md V1 floor:
/// - 3 equipment slot (weapon / armor / accessory)
/// - 5 affix line per item (implicit 1 + prefix 2 + suffix 2)
/// - refit 고정 15 Echo single-affix reroll
/// </summary>
public sealed record EquipmentRefitAffixRowViewState(
    string GroupKey,             // implicit / prefix / suffix
    string IconKey,              // affix sprite
    Texture2D? IconSprite,
    bool IsSelectedForReroll
);

public sealed record EquipmentRefitPoolRowViewState(
    string ItemInstanceId,
    string IconKey,
    Texture2D? IconSprite,
    string RarityKey             // common / rare / epic (gem 색상)
);

public sealed record EquipmentRefitViewState(
    Texture2D? StandeePortrait,  // 선택 hero portrait
    Texture2D? EchoSprite,
    int RefitCost,               // 15 Echo (item-and-affix-system.md V1 fixed)
    IReadOnlyList<EquipmentRefitAffixRowViewState> Affixes,
    IReadOnlyList<EquipmentRefitPoolRowViewState> Pool
);
