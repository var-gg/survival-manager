using System;
using System.Collections.Generic;

namespace SM.Editor.Validation;

public sealed record CampaignTwoArmSweepReport
{
    public string SchemaVersion { get; init; } = CampaignBalanceSweepConfig.CurrentSchemaVersion;
    public string GeneratedAtUtc { get; init; } = DateTime.UtcNow.ToString("O");
    public CampaignBalanceSweepConfig Config { get; init; } = CampaignBalanceSweepConfig.Default;
    public CampaignGridExecutionReport Grid { get; init; } = new();
    public IReadOnlyList<CampaignBalanceArmSpec> Arms { get; init; } = Array.Empty<CampaignBalanceArmSpec>();
    public IReadOnlyList<CampaignTwoArmNodeReport> Nodes { get; init; } = Array.Empty<CampaignTwoArmNodeReport>();
    public IReadOnlyList<CampaignTwoArmSiteReport> Sites { get; init; } = Array.Empty<CampaignTwoArmSiteReport>();
    public IReadOnlyList<CampaignTwoArmChapterReport> Chapters { get; init; } = Array.Empty<CampaignTwoArmChapterReport>();
    public IReadOnlyList<CampaignDecisionDensityReport> DecisionDensity { get; init; } = Array.Empty<CampaignDecisionDensityReport>();
    public CampaignThreatLandingWitnessReport ThreatLandingWitness { get; init; } = new();
    public CampaignTwoArmSweepSummary Summary { get; init; } = new();
    public IReadOnlyList<string> PhaseAApproximations { get; init; } = Array.Empty<string>();
    public string JsonReportPath { get; init; } = string.Empty;
}

public sealed record CampaignGridExecutionReport
{
    public int ReferenceSquadCount { get; init; }
    public int BuildPowerQuantileCount { get; init; }
    public int EnemyCompositionVariantCount { get; init; }
    public int RosterCoverageVariantCount { get; init; }
    public int FullCellCountPerArmPerNode { get; init; }
    public int ExecutedCellCountPerArmPerNode { get; init; }
    public int MinimumRequiredEffectiveSamples { get; init; }
    public double MaximumAllowedWilsonHalfWidth { get; init; }
    public double MaximumObservedWilsonHalfWidth { get; init; }
    public int? SamplingCap { get; init; }
    public string SamplingCapLog { get; init; } = string.Empty;
    public bool MeetsSamplingContract { get; init; }
}

public sealed record CampaignArmSampleAggregate(
    string ArmId,
    string PolicyId,
    int SampleCount,
    int WinCount,
    int BossWinWithAnswerTagCount)
{
    public double WinRate => SampleCount == 0 ? 0 : WinCount / (double)SampleCount;
    public double? AnswerTagGivenWinRate => WinCount == 0 ? null : BossWinWithAnswerTagCount / (double)WinCount;
}

public sealed record CampaignSquadArmSampleAggregate(
    string SquadId,
    CampaignArmSampleAggregate Naive,
    CampaignArmSampleAggregate Informed)
{
    public double Gap => Informed.WinRate - Naive.WinRate;
}

public sealed record CampaignNodeBandTarget(
    string NodeKind,
    ProbabilityBand NaiveWinBand,
    ProbabilityBand InfoWinBand,
    ProbabilityRange ArmGapBand,
    bool NaiveBossCliffExemptWhenGapPasses,
    double? AnswerTagGivenNaiveWinMinimum);

public sealed record CampaignTwoArmNodeReport(
    string ChapterId,
    int ChapterOrder,
    string SiteId,
    int SiteOrder,
    string NodeId,
    int NodeOrder,
    string EncounterId,
    bool IsElite,
    bool IsBoss,
    CampaignArmSampleAggregate Naive,
    CampaignArmSampleAggregate Informed,
    double Gap,
    double NaiveWilsonHalfWidth,
    double InfoWilsonHalfWidth,
    CampaignNodeBandTarget Target,
    bool NaiveBandPass,
    bool InfoBandPass,
    bool GapBandPass,
    bool AnswerTagPass,
    bool Chapter1EachSquadFloorPass,
    string Status,
    IReadOnlyList<string> Findings,
    IReadOnlyList<CampaignSquadArmSampleAggregate> ByReferenceSquad);

public sealed record CampaignTwoArmSiteReport(
    string ChapterId,
    int ChapterOrder,
    string SiteId,
    int SiteOrder,
    CampaignArmSampleAggregate NaiveSiteAnd,
    CampaignArmSampleAggregate InformedSiteAnd,
    double Gap,
    IReadOnlyList<CampaignSquadArmSampleAggregate> ByReferenceSquad);

public sealed record CampaignTwoArmChapterReport(
    string ChapterId,
    int ChapterOrder,
    double NaiveNodeWinRate,
    double InfoNodeWinRate,
    double Gap,
    double NaiveBossWinRate,
    double InfoBossWinRate,
    double BossGap,
    int NodeSampleCountPerArm,
    int BossSampleCountPerArm);

public sealed record CampaignDecisionDensityCounts(
    long Branch,
    long Preview,
    long Maintenance,
    long Interlude)
{
    public long Total => Branch + Preview + Maintenance + Interlude;
}

public sealed record CampaignDecisionDensityReport(
    string ArmId,
    string PolicyId,
    long UniqueBattlesEntered,
    CampaignDecisionDensityCounts AuthoredOpportunities,
    CampaignDecisionDensityCounts RealizedStateChanges,
    long ForcedNoOpClicks,
    long LossesObserved,
    long LossesFollowedByChangedSetup,
    double AuthoredDecisionOpportunityRatio,
    double RealizedStateChangingRatio,
    double ForcedNoOpClickRatio,
    double LossToChangedRetryRate,
    string Status,
    IReadOnlyList<string> Findings);

public sealed record CampaignTwoArmSweepSummary
{
    public string Status { get; init; } = "BASELINE-GAP";
    public int NodeCount { get; init; }
    public int PassNodeCount { get; init; }
    public int FailNodeCount { get; init; }
    public int BaselineGapNodeCount { get; init; }
    public double MeanNaiveWinRate { get; init; }
    public double MeanInfoWinRate { get; init; }
    public double MeanGap { get; init; }
    public double MeanBossNaiveWinRate { get; init; }
    public double MeanBossInfoWinRate { get; init; }
    public double MeanBossGap { get; init; }
    public int PrepFormationDivergenceCount { get; init; }
    public int OutcomeDivergenceCount { get; init; }
    public int InformedOnlyWinCount { get; init; }
    public int NaiveOnlyWinCount { get; init; }
    public int PrepEquipmentAssignmentCount { get; init; }
    public int GearCounterSampleCount { get; init; }
    public double GearCounterNaiveWinRate { get; init; }
    public double GearCounterInformedWinRate { get; init; }
    public double GearCounterGap { get; init; }
    public IReadOnlyList<string> CliffFindings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ConsecutiveInfoSiteAndDropFindings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> LateSaturationFindings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> FinalSiteAndFindings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SamplingFindings { get; init; } = Array.Empty<string>();
}

public sealed record CampaignTwoArmNodeAggregate(
    string ChapterId,
    int ChapterOrder,
    string SiteId,
    int SiteOrder,
    string NodeId,
    int NodeOrder,
    string EncounterId,
    bool IsElite,
    bool IsBoss,
    CampaignArmSampleAggregate Naive,
    CampaignArmSampleAggregate Informed,
    IReadOnlyList<CampaignSquadArmSampleAggregate> ByReferenceSquad);
