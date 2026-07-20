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
    public List<string> ActiveWoundHeroIds = new();
    public List<string> ResolvedExpeditionNodeIds = new();
    public RecruitPhaseState RecruitPhase = new();
    public RecruitPityState RecruitPity = new();
    public string CompileVersion = string.Empty;
    public string CompileHash = string.Empty;
    public string LastBattleMatchId = string.Empty;
    public bool LastSettlementWasVictory;
    public string ChapterId = string.Empty;
    public string SiteId = string.Empty;
    public int SiteNodeIndex = 0;
    public string EncounterId = string.Empty;
    public int BattleSeed = 0;
    public string BattleContextHash = string.Empty;
    public string RewardSourceId = string.Empty;
    public string RewardCommitId = string.Empty;

    /// <summary>
    /// 이 sortie에서 서약한 Warrant id(없으면 ""). per-sortie truth — RewardSourceId와 동렬 rail.
    /// settlement이 이 값으로 WarrantSpec을 조회해 서약 이행을 판정한다. ADR-0027(P2a).
    /// </summary>
    public string PledgedWarrantId = string.Empty;
    public string FirstSelectedTemporaryAugmentId = string.Empty;
    public string PendingPermanentUnlockId = string.Empty;
    public bool StoryCleared;
    public bool EndlessUnlocked;

    /// <summary>0 = 스토리 원정, 1+ = 무한 순환 N회차 run.</summary>
    public int EndlessCycleIndex;
}
