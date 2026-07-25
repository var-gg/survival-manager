using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class InventoryAffixMagnitudeRecord
{
    public string AffixId = string.Empty;
    public float Magnitude;
}
