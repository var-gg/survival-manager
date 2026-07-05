using System;
using System.Collections.Generic;
using SM.Meta;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SaveProfile
{
    public string ProfileId = "default";
    public string DisplayName = "Player";
    public List<HeroInstanceRecord> Heroes = new();
    public List<InventoryItemRecord> Inventory = new();
    public CurrencyRecord Currencies = new();
    public CampaignProgressRecord CampaignProgress = new();
    public List<string> UnlockedPermanentAugmentIds = new();
    public List<HeroLoadoutRecord> HeroLoadouts = new();
    public List<HeroProgressionRecord> HeroProgressions = new();
    public List<SkillInstanceRecord> SkillInstances = new();
    public List<PassiveSelectionRecord> PassiveSelections = new();
    public List<PermanentAugmentLoadoutRecord> PermanentAugmentLoadouts = new();
    public List<SquadBlueprintRecord> SquadBlueprints = new();
    public string ActiveBlueprintId = "blueprint.default";
    public ActiveRunRecord ActiveRun = new();
    public List<MatchRecordHeader> MatchHeaders = new();
    public List<MatchRecordBlob> MatchBlobs = new();
    public List<InventoryLedgerEntryRecord> InventoryLedger = new();
    public List<RewardLedgerEntryRecord> RewardLedger = new();
    public List<SuspicionFlagRecord> SuspicionFlags = new();
    public List<RunSummaryRecord> RunSummaries = new();
    public List<DossierEntryRecord> Dossier = new();
    public List<FactionStandingRecord> FactionStanding = new();
    public List<ArenaDefenseSnapshotRecord> ArenaDefenseSnapshots = new();
    public List<ArenaBlueprintSlotRecord> ArenaBlueprintSlots = new();
    public List<ArenaMatchRecordRecord> ArenaMatchRecords = new();
    public List<ArenaSeasonStateRecord> ArenaSeasons = new();
    public List<ArenaRewardLedgerEntryRecord> ArenaRewardLedger = new();
    // 공유 static(NarrativeProgressRecord.Empty) 참조 금지 — populate형 역직렬화가 필드의 기존
    // 인스턴스를 재사용하면 전역 Empty가 오염된다. 프로필마다 독립 인스턴스를 가진다.
    public NarrativeProgressRecord Narrative = new();
}
