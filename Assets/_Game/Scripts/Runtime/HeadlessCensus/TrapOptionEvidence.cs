using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>옵션 한 건의 4단계 판정과 dominant mirror를 함께 보존하는 owner-review evidence.</summary>
public sealed record TrapOptionEvidence(
    string OptionId,
    string SubjectKind,
    string SubjectId,
    bool StageAFlagged,
    bool AutomaticConfirmGrade,
    IReadOnlyList<string> MechanicalDefectCodes,
    int EligibleWitnessCount,
    int FiredWitnessCount,
    int PositiveWitnessCount,
    int IntendedPairCount,
    int FullCensusPairCount,
    double ComparatorNonWorseRate,
    double ComparatorStrictlyBetterRate,
    double OptionNonWorseRate,
    double OptionStrictlyBetterRate,
    double MedianPairedWinUplift,
    int PotentialUniqueUnlockCount,
    int FullCensusPositiveWitnessCount,
    bool ContinuationMeasured,
    bool ContinuationUniqueAdvantage,
    bool RescuedEnabler,
    bool HasVisibleTradeoff,
    bool ConfirmedTrap,
    bool BugGradeDominant,
    bool OwnerVerdictRequired,
    string CandidateStatus,
    string VerdictReason);
