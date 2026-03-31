using System;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ArenaBlueprintSlotRecord
{
    public string SlotId = string.Empty;
    public string BlueprintId = string.Empty;
    public bool IsDefense;
    public bool IsActive;
}
