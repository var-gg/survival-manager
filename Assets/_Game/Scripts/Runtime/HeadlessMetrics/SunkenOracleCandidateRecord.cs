using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>한 arrival state에서 한 합법 편성·배치를 paired seed로 재실행한 site-level 결과.</summary>
public sealed class SunkenOracleCandidateRecord
{
    public const string SameStateScope = "same_state";
    public const string LookbackScope = "one_site_lookback";

    public string SchemaVersion { get; set; } = "sunken-oracle-candidate-v1";
    public string RunId { get; set; } = string.Empty;
    public string SampleId { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public int CampaignSeed { get; set; }
    public string Scope { get; set; } = SameStateScope;
    public string StateVariantId { get; set; } = string.Empty;
    public string CandidateId { get; set; } = string.Empty;
    public string BuildId { get; set; } = string.Empty;
    public string PlacementId { get; set; } = string.Empty;
    public string CounterFamilyId { get; set; } = string.Empty;
    public IReadOnlyList<string> HeroIds { get; set; } = new List<string>();
    public IReadOnlyList<string> ArchetypeIds { get; set; } = new List<string>();
    public IReadOnlyList<int> AnchorIds { get; set; } = new List<int>();
    public IReadOnlyList<int> BattleSeeds { get; set; } = new List<int>();
    public bool IsPolicyChoice { get; set; }
    public string AddedRosterArchetypeId { get; set; } = string.Empty;
    public int RewardOptionIndex { get; set; } = -1;
    public string RewardPayloadId { get; set; } = string.Empty;
    public bool SiteCompleted { get; set; }
    public int BattleCount { get; set; }
    public int BattleWinCount { get; set; }
    public double BattleWinRate { get; set; }
    public double FinalTeamHpFraction { get; set; }
    public string FailureEncounterId { get; set; } = string.Empty;
    public string FailureCode { get; set; } = string.Empty;
    public string ReplayManifestHash { get; set; } = string.Empty;
}
