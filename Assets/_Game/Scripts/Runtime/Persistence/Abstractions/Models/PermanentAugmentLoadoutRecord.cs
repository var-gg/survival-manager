using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class PermanentAugmentLoadoutRecord
{
    public string BlueprintId = string.Empty;
    public List<string> EquippedAugmentIds = new();
}
