using System.Collections.Generic;

namespace SM.Editor.Validation;

internal sealed record CampaignWallDeficitMeasurementReport(
    string SchemaVersion,
    string Method,
    int SeedBase,
    int SeedCount,
    double SearchMaximum,
    double SearchTolerance,
    int AdaptationRetryCap,
    string PolicyId,
    int WallsObserved,
    double MeanWallDeficit,
    double SigmaWallPopulation,
    IReadOnlyList<CampaignWallDeficitQuantile> WallDeficitQuantiles,
    IReadOnlyList<CampaignWallCountQuantile> WallsPerCampaignQuantiles,
    int MaxWallsPerCampaign,
    IReadOnlyList<CampaignWallProgressQuantile> ProgressAfterUnblockQuantiles,
    IReadOnlyList<CampaignWallDeficitCampaignObservation> Campaigns,
    int MonotonicityViolationCount,
    int RightCensoredCount,
    string CanonicalHash);

internal sealed record CampaignWallDeficitCampaignObservation(
    int CampaignIndex,
    int CampaignSeed,
    bool Completed,
    int SiteCount,
    int BattleNodesCompleted,
    int WallsObserved,
    double CumulativeLogPower,
    IReadOnlyList<CampaignWallDeficitObservation> Walls);

internal sealed record CampaignWallDeficitObservation(
    int WallIndex,
    string ChapterId,
    string SiteId,
    string NodeId,
    int SiteAttempt,
    int BattleNodeOrdinal,
    double CumulativeLogBefore,
    double AdditionalLogDeficit,
    double CumulativeLogAfter,
    double AdditionalPowerPercent,
    int EvaluationCount,
    int NodesAdvancedAfterUnblock);

internal sealed record CampaignWallDeficitQuantile(
    double Probability,
    double Deficit,
    double EmpiricalCdf);

internal sealed record CampaignWallCountQuantile(
    double Probability,
    int Walls,
    double EmpiricalCdf);

internal sealed record CampaignWallProgressQuantile(
    double Probability,
    int NodesAdvanced,
    double EmpiricalCdf);
