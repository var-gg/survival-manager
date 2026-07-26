using System;
using System.Collections.Generic;
using SM.Core.Content;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ItemCraftOperationRecord
{
    public string OperationId = string.Empty;
    public string ItemInstanceId = string.Empty;
    public string ItemBaseId = string.Empty;
    public CraftOperationKindValue OperationKind = CraftOperationKindValue.Reforge;
    public List<string> SealedAffixIds = new();
    public int AttemptIndex;
    public ulong StableCommandSeed;
    public int TargetRefitLevel;
    public int RulesVersion;
    public int EchoCost;
}
