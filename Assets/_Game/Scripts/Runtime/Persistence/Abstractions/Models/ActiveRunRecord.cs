using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class ActiveRunRecord
{
    public string RunId = string.Empty;
    public string ExpeditionId = string.Empty;
    public string BlueprintId = string.Empty;
    public bool IsQuickBattle;
    public int CurrentNodeIndex = 0;
    public List<string> TemporaryAugmentIds = new();
    public List<string> PendingRewardIds = new();
    public List<string> BattleDeployHeroIds = new();
    public string CompileVersion = string.Empty;
    public string CompileHash = string.Empty;
    public string LastBattleMatchId = string.Empty;
}
