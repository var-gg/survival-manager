using System;
using System.Collections.Generic;
using SM.Meta.Model;

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
    public RecruitPhaseState RecruitPhase = new();
    public RecruitPityState RecruitPity = new();
    public string CompileVersion = string.Empty;
    public string CompileHash = string.Empty;
    public string LastBattleMatchId = string.Empty;
    public string ChapterId = string.Empty;
    public string SiteId = string.Empty;
    public int SiteNodeIndex = 0;
    public string EncounterId = string.Empty;
    public int BattleSeed = 0;
    public string BattleContextHash = string.Empty;
    public string RewardSourceId = string.Empty;
    public bool StoryCleared;
    public bool EndlessUnlocked;
}
