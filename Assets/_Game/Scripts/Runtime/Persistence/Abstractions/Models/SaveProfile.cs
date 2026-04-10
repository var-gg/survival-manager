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
    public List<ArenaDefenseSnapshotRecord> ArenaDefenseSnapshots = new();
    public List<ArenaBlueprintSlotRecord> ArenaBlueprintSlots = new();
    public List<ArenaMatchRecordRecord> ArenaMatchRecords = new();
    public List<ArenaSeasonStateRecord> ArenaSeasons = new();
    public List<ArenaRewardLedgerEntryRecord> ArenaRewardLedger = new();
    public NarrativeProgressRecord Narrative = NarrativeProgressRecord.Empty;
}
