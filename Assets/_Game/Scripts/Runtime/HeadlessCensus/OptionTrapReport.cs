using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed record OptionTrapReport(
    string SchemaVersion,
    string EvaluatorVersion,
    bool GoldenNeutral,
    string ReproductionHash,
    OptionTrapSamplingPlan SamplingPlan,
    int OptionContractCount,
    int PromiseCoverageGapCount,
    int ComparatorCoverageGapCount,
    int MechanicalDefectCandidateCount,
    int FlaggedOptionCount,
    int ConfirmedTrapCount,
    int BugGradeDominantCount,
    int RescuedEnablerCount,
    IReadOnlyList<TrapOptionEvidence> Evidence,
    IReadOnlyList<OptionTrapReport.OwnerVerdictItem> OwnerVerdictQueue)
{
    public const string CurrentSchemaVersion = "option-trap-report-bt1-v1";
    public const string CurrentEvaluatorVersion = "option-trap-oracle-bt1-v1";

    public sealed record OwnerVerdictItem(
        string OptionId,
        string CandidateKind,
        string EvidenceSummary,
        string Status);
}
