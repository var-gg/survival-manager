using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class InventoryItemRecord
{
    public string ItemInstanceId = string.Empty;
    public string ItemBaseId = string.Empty;
    public List<string> AffixIds = new();
    // Additive save field: an absent/empty list means legacy fixed definition magnitudes.
    public List<InventoryAffixMagnitudeRecord> AffixMagnitudeRolls = new();
    public string EquippedHeroId = string.Empty;
    public int RolledRarityTier = -1;
    // Additive save field: absent in legacy JSON deserializes to the CLR default (0).
    public int RefitLevel;
}
