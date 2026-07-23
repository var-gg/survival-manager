using System.Collections.Generic;

namespace SM.Editor.Validation;

internal sealed record CampaignSignedDeficitMeasurementReport(
    string SchemaVersion,
    string Method,
    int SeedBase,
    int SeedCount,
    double SearchMinimum,
    double SearchMaximum,
    double SearchTolerance,
    string InformedPolicyId,
    string NaivePolicyId,
    double DeltaMean,
    double SigmaPopulation,
    double Q0Informed,
    double Q0Naive,
    double? InformedToNaiveRatio,
    IReadOnlyList<CampaignDeficitQuantile> CdfQuantiles,
    IReadOnlyList<CampaignSignedDeficitSeedObservation> InformedSeeds,
    IReadOnlyList<CampaignZeroPowerSeedObservation> NaiveSeeds,
    bool VarianceDecompositionAvailable,
    string VarianceDecompositionNote,
    int MonotonicityViolationCount,
    int LeftCensoredCount,
    int RightCensoredCount,
    string CanonicalHash);

internal sealed record CampaignSignedDeficitSeedObservation(
    int CampaignIndex,
    int CampaignSeed,
    bool ClearedAtZero,
    double? SignedDeficit,
    bool LeftCensored,
    bool RightCensored,
    bool MonotonicityViolated,
    int EvaluationCount,
    string ZeroPowerTerminalNodeId);

internal sealed record CampaignZeroPowerSeedObservation(
    int CampaignIndex,
    int CampaignSeed,
    bool Cleared,
    string TerminalNodeId,
    int BattleCount,
    int SiteCount);

internal sealed record CampaignDeficitQuantile(
    double Probability,
    double Deficit,
    double EmpiricalCdf);

internal sealed record CampaignCompletionObservation(
    bool Completed,
    string TerminalNodeId,
    int BattleCount,
    int SiteCount);
