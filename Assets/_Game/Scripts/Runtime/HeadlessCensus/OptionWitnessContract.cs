using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>E02 truth graph가 자동 파생한 옵션별 구조·기계 witness 계약.</summary>
public sealed record OptionWitnessContract(
    string OptionId,
    string SubjectKind,
    string SubjectId,
    string BaselineComparatorId,
    string ComparatorGroupId,
    string BudgetBand,
    IReadOnlyList<string> ComparatorOptionIds,
    IReadOnlyList<string> IntendedPredicates,
    IReadOnlyList<string> PrerequisiteOptionIds,
    IReadOnlyList<string> AcquisitionPaths,
    IReadOnlyList<OptionWitnessPromise> Promises,
    int PotentialUniqueUnlockCount,
    bool HasVisibleTradeoff,
    bool PromiseCoverageComplete,
    bool ComparatorCoverageComplete,
    bool StructuralTrapCandidate,
    bool StructuralDominanceCandidate)
{
    public static string StableOptionId(string subjectKind, string subjectId)
        => $"{subjectKind}:{subjectId}";

    public static bool IsSupportedSubjectKind(string subjectKind)
        => subjectKind is BuildGrammarSubjectKind.Skill
            or BuildGrammarSubjectKind.Item
            or BuildGrammarSubjectKind.Affix
            or BuildGrammarSubjectKind.Augment
            or BuildGrammarSubjectKind.Passive;

    public static string ResolveBaselineComparator(string subjectKind, IEnumerable<string> acquisitionPaths)
    {
        if (subjectKind is BuildGrammarSubjectKind.Item or BuildGrammarSubjectKind.Affix)
        {
            return "empty_slot";
        }

        foreach (var path in acquisitionPaths ?? Array.Empty<string>())
        {
            if (string.Equals(path, "reward", StringComparison.Ordinal))
            {
                return "skip";
            }
        }

        return "not_selected";
    }
}
