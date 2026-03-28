using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class InventoryItemRecord
{
    public string ItemInstanceId = string.Empty;
    public string ItemBaseId = string.Empty;
    public List<string> AffixIds = new();
    public string EquippedHeroId = string.Empty;
}
