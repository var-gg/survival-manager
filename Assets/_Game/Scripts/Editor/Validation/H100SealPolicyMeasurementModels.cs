using System;
using System.Collections.Generic;
using SM.HeadlessMetrics;

namespace SM.Editor.Validation;

internal sealed record H100SealPolicyMeasurementReport(
    string SchemaVersion,
    string CoverageAnchorId,
    string MeasurementIntentId,
    string TargetAffixId,
    H100SealGoldenComparison Golden,
    H100SealPolicyArmReport WithSeal,
    H100SealPolicyArmReport WithoutSeal,
    H100SealPolicyDelta Delta,
    string Verdict);

internal sealed record H100SealGoldenComparison(
    string BaselineTracePath,
    string CandidateTracePath,
    string BaselineSha256,
    string CandidateSha256,
    long BaselineByteCount,
    long CandidateByteCount,
    bool ByteIdentical);

internal sealed record H100SealPolicyArmReport(
    string ArmId,
    int RefitWindowCount,
    int SealCount,
    int PlainRefitCount,
    int SkipCount,
    int CraftingEchoSpent,
    IReadOnlyList<string> SealedItems,
    double? MeanRollQualityBefore,
    double? MeanRollQualityAfter,
    double? MeanRollQualityGain,
    IReadOnlyList<H100SealCraftingOperationRecord> CraftingOperations,
    CampaignMetricRecord Campaign);

internal sealed record H100SealCraftingOperationRecord(
    int DecisionIndex,
    string Operation,
    string ItemId,
    string ItemInstanceId,
    int EchoSpent,
    double RollQualityBefore,
    double RollQualityAfter,
    double RollQualityDelta);

internal sealed record H100SealPolicyDelta(
    int SealCount,
    int CraftingEchoSpent,
    double? MeanRollQualityAfter,
    double? MeanRollQualityGain,
    int Completed,
    int Sites,
    int Battles,
    int Wins,
    int Losses,
    bool CampaignOutcomeChanged);
