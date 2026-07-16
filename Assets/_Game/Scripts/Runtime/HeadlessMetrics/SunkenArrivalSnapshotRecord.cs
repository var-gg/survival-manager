using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>sunken_bastion 진입 직전 player-visible 캠페인 상태의 진단 전용 projection.</summary>
public sealed class SunkenArrivalSnapshotRecord
{
    public string SchemaVersion { get; set; } = "sunken-arrival-snapshot-v1";
    public string RunId { get; set; } = string.Empty;
    public string SampleId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public int CampaignSeed { get; set; }
    public int SiteIndex { get; set; }
    public int BattleStartIndex { get; set; }
    public string ChapterId { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public int Gold { get; set; }
    public int Echo { get; set; }
    public IReadOnlyList<string> OwnedArchetypeIds { get; set; } = new List<string>();
    public IReadOnlyList<string> ExpeditionSquadHeroIds { get; set; } = new List<string>();
    public IReadOnlyList<RosterHero> Roster { get; set; } = new List<RosterHero>();
    public IReadOnlyList<Placement> ChosenPlacements { get; set; } = new List<Placement>();
    public string ChosenRationale { get; set; } = string.Empty;
    public double ChosenEstimatedValue { get; set; }
    public string CurrentEncounterId { get; set; } = string.Empty;
    public IReadOnlyList<string> CurrentEnemyArchetypeIds { get; set; } = new List<string>();
    public string PreviousSiteId { get; set; } = string.Empty;
    public int PreviousRewardOptionIndex { get; set; } = -1;
    public string PreviousRewardPayloadId { get; set; } = string.Empty;
    public IReadOnlyList<RewardOption> PreviousRewardOptions { get; set; } = new List<RewardOption>();
    public IReadOnlyList<RecruitOffer> PreviousRecruitOffers { get; set; } = new List<RecruitOffer>();

    public sealed class RosterHero
    {
        public string HeroId { get; set; } = string.Empty;
        public string ArchetypeId { get; set; } = string.Empty;
        public string RaceId { get; set; } = string.Empty;
        public string ClassId { get; set; } = string.Empty;
        public string RoleTag { get; set; } = string.Empty;
        public int Level { get; set; }
        public int CurrentHp { get; set; }
        public int MaxHp { get; set; }
        public int EquippedItemCount { get; set; }
        public bool InExpeditionSquad { get; set; }
    }

    public sealed class Placement
    {
        public int AnchorId { get; set; }
        public string HeroId { get; set; } = string.Empty;
    }

    public sealed class RewardOption
    {
        public int OptionIndex { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string PayloadId { get; set; } = string.Empty;
        public int GoldAmount { get; set; }
        public int EchoAmount { get; set; }
    }

    public sealed class RecruitOffer
    {
        public int OfferIndex { get; set; }
        public string ArchetypeId { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public int GoldCost { get; set; }
    }
}
