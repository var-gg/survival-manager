using System;
using System.Collections.Generic;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

internal sealed record H100SealPolicyMeasurementReport(
    string SchemaVersion,
    string PreregistrationSha256,
    int SeedBase,
    int SeedCount,
    IReadOnlyList<int> LogicalSeeds,
    string CoverageAnchorId,
    string MeasurementIntentId,
    string TargetAffixId,
    H100SealGoldenComparison Golden,
    H100SealRefitWindowCensus Census,
    H100SealPolicyArmReport NoSealArm,
    IReadOnlyList<H100SealPolicySweepResult> PolicySweep,
    H100SealH2Verdict H2Verdict,
    H100SealWidthProbeReport WidthProbe,
    H100SealMeasurementConclusion Conclusion,
    IReadOnlyList<string> Surprises);

internal sealed record H100SealGoldenComparison(
    string BaselineTracePath,
    string CandidateTracePath,
    string BaselineSha256,
    string CandidateSha256,
    long BaselineByteCount,
    long CandidateByteCount,
    bool ByteIdentical);

internal sealed record H100SealPolicyCalibration(
    double Threshold,
    double NetValueFloor,
    double Baseline)
{
    public string Id =>
        $"t{Threshold:R}-f{NetValueFloor:R}-b{Baseline:R}";
}

internal sealed record H100SealPolicyArmReport(
    string ArmId,
    H100SealPolicyCalibration? Calibration,
    int RefitWindowCount,
    int SealCount,
    int PlainRefitCount,
    int SkipCount,
    int CampaignsWithSeal,
    int CraftingEchoSpent,
    IReadOnlyList<H100SealRefitWindowRecord> RefitWindows,
    IReadOnlyList<H100SealCraftingOperationRecord> CraftingOperations,
    IReadOnlyList<H100SealCampaignTerminalRecord> TerminalCampaigns,
    IReadOnlyList<CampaignMetricRecord> Campaigns,
    PlayerVisibleFactAuditResult FactAudit);

internal sealed record H100SealRefitWindowRecord(
    int CampaignIndex,
    int LogicalSeed,
    int DerivedCampaignSeed,
    int SiteIndex,
    int DecisionIndex,
    int WindowOrdinal,
    int WalletGold,
    int WalletEcho,
    int VisibleRefitItemCount,
    string CandidateItemId,
    string CandidateItemInstanceId,
    int CandidateSlotIndex,
    int CandidatePlainRefitCost,
    bool CandidateAllowsSeal,
    int CandidateAffixCount,
    IReadOnlyList<H100SealCostRecord> CandidateSealCosts,
    IReadOnlyList<H100SealAffixObservationRecord> CandidateAffixes,
    double? CandidateMeanRollQuality,
    double? VisibleInventoryMeanRollQuality,
    double? CandidateSelectionBias,
    int AffordableSealQuoteCount,
    string AppliedAction,
    int? EchoSpent,
    double? CandidateRollQualityAfter);

internal sealed record H100SealAffixObservationRecord(
    string AffixId,
    int SlotIndex,
    double RollQuality,
    float Magnitude,
    float ValueMin,
    float ValueMax);

internal sealed record H100SealCostRecord(
    int LockedAffixCount,
    int EchoCost,
    bool Affordable);

internal sealed record H100SealCraftingOperationRecord(
    int CampaignIndex,
    int LogicalSeed,
    int DerivedCampaignSeed,
    int DecisionIndex,
    string Operation,
    string ItemId,
    string ItemInstanceId,
    int EchoSpent,
    double RollQualityBefore,
    double RollQualityAfter,
    double RollQualityDelta);

internal sealed record H100SealCampaignTerminalRecord(
    int CampaignIndex,
    int LogicalSeed,
    int DerivedCampaignSeed,
    bool Observed,
    int? Gold,
    int? Echo,
    double? InventoryMeanRollQuality,
    int InventoryAffixCount);

internal sealed record H100SealRefitWindowCensus(
    IReadOnlyList<H100SealCampaignWindowCount> WindowsPerCampaign,
    int CampaignsWithWindows,
    int TotalWindows,
    int CandidateAffixObservationCount,
    int DistinctQualityVectorCount,
    H100SealQuantiles WalletGoldQuantiles,
    H100SealQuantiles WalletEchoQuantiles,
    IReadOnlyList<H100SealCountFrequency> AffixesPerCandidateItem,
    int SealAvailableWindowCount,
    int AffordableSealQuoteCount,
    H100SealQuantiles RollQualityQuantiles,
    IReadOnlyList<H100SealHistogramBin> RollQualityHistogram,
    double FractionAtOrAbove070,
    double? MaxObservedRollQuality,
    double? MeanCandidateSelectionBias,
    bool DataAdequate,
    IReadOnlyList<string> InadequacyReasons);

internal sealed record H100SealCampaignWindowCount(
    int CampaignIndex,
    int LogicalSeed,
    int DerivedCampaignSeed,
    int WindowCount);

internal sealed record H100SealCountFrequency(
    int Value,
    int Count);

internal sealed record H100SealQuantiles(
    double? P0,
    double? P10,
    double? P25,
    double? P50,
    double? P75,
    double? P90,
    double? P100);

internal sealed record H100SealHistogramBin(
    double LowerInclusive,
    double Upper,
    bool UpperInclusive,
    int Count);

internal sealed record H100SealPolicySweepResult(
    H100SealPolicyCalibration Calibration,
    int RefitWindowCount,
    int SealCount,
    int CampaignsWithSeal,
    double SealFrequency,
    double? CurrencyDelta,
    double? TerminalGoldDelta,
    int CraftingEchoSpentDelta,
    double? RollQualityDelta,
    double OutcomeDelta,
    int CompletedDelta,
    int SiteDelta,
    int BattleDelta,
    int WinDelta,
    int LossDelta,
    int CrashDelta,
    int TruncationDelta,
    int PairedTerminalCount,
    int MissingTerminalPairCount,
    bool FactAuditPassed,
    bool MeetsH2Rule);

internal sealed record H100SealH2Verdict(
    bool AnyReasonableCalibrationSeals,
    H100SealPolicyCalibration? BestSetting,
    string WhatItAchieved,
    bool RuledOut,
    bool InsufficientData);

internal sealed record H100SealWidthProbePoint(
    double Multiplier,
    double MaximumQualityRecomputeError,
    bool DecisionSurfaceChanged,
    double BestSealFrequency);

internal sealed record H100SealWidthProbeReport(
    bool Ran,
    string WhyOrWhyNot,
    double? MultiplierNeeded,
    double? ResultingSealFrequency,
    string Method,
    bool NoShippedAssetModified,
    IReadOnlyList<H100SealWidthProbePoint> Points);

internal sealed record H100SealMeasurementConclusion(
    string SupportedHypothesis,
    string Confidence,
    bool InsufficientData,
    string WhatWouldSettleIt);
