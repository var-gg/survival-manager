using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SM.HeadlessCensus;

/// <summary>기계 witness→intended pair→continuation→flagged full census 순서의 골든 중립 판정기.</summary>
public static class OptionTrapOracleEvaluator
{
    private const double Epsilon = 1e-9d;

    public static IReadOnlyList<string> ScreenStageA(
        IReadOnlyList<OptionWitnessContract> contracts,
        IReadOnlyList<OptionMechanicalWitness> witnesses)
    {
        return (contracts ?? Array.Empty<OptionWitnessContract>())
            .Where(contract => contract != null)
            .Where(contract => contract.StructuralTrapCandidate
                               || contract.StructuralDominanceCandidate
                               || MechanicalDefectCodes(
                                   contract,
                                   (witnesses ?? Array.Empty<OptionMechanicalWitness>())
                                   .Where(value => value.OptionId == contract.OptionId)
                                   .ToArray()).Count > 0)
            .Select(contract => contract.OptionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    public static OptionTrapReport Evaluate(OptionTrapOracleInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var contracts = (input.Contracts ?? Array.Empty<OptionWitnessContract>())
            .Where(value => value != null)
            .OrderBy(value => value.OptionId, StringComparer.Ordinal)
            .ToArray();
        var duplicate = contracts.GroupBy(value => value.OptionId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Duplicate option contract: {duplicate.Key}", nameof(input));
        }

        var evidence = contracts.Select(contract => EvaluateOption(
                contract,
                (input.MechanicalWitnesses ?? Array.Empty<OptionMechanicalWitness>())
                .Where(value => value.OptionId == contract.OptionId)
                .OrderBy(value => value.ContextId, StringComparer.Ordinal)
                .ThenBy(value => value.PromiseId, StringComparer.Ordinal)
                .ToArray(),
                (input.PairedCounterfactuals ?? Array.Empty<OptionPairedCounterfactual>())
                .Where(value => value.OptionId == contract.OptionId)
                .OrderBy(value => value.FullCensus)
                .ThenBy(value => value.ContextId, StringComparer.Ordinal)
                .ThenBy(value => value.PlacementId, StringComparer.Ordinal)
                .ThenBy(value => value.Seed)
                .ThenBy(value => value.ComparatorId, StringComparer.Ordinal)
                .ToArray(),
                (input.ContinuationComparisons ?? Array.Empty<OptionContinuationComparison>())
                .Where(value => value.OptionId == contract.OptionId)
                .OrderBy(value => value.ContextId, StringComparer.Ordinal)
                .ToArray()))
            .OrderBy(value => value.OptionId, StringComparer.Ordinal)
            .ToArray();
        var queue = evidence.Where(value => value.OwnerVerdictRequired)
            .Select(ToOwnerVerdictItem)
            .OrderBy(value => value.CandidateKind, StringComparer.Ordinal)
            .ThenBy(value => value.OptionId, StringComparer.Ordinal)
            .ToArray();
        var report = new OptionTrapReport(
            OptionTrapReport.CurrentSchemaVersion,
            OptionTrapReport.CurrentEvaluatorVersion,
            GoldenNeutral: true,
            ReproductionHash: string.Empty,
            input.SamplingPlan,
            contracts.Length,
            contracts.Count(value => !value.PromiseCoverageComplete),
            contracts.Count(value => !value.ComparatorCoverageComplete),
            evidence.Count(value => value.MechanicalDefectCodes.Count > 0),
            evidence.Count(IsFlagged),
            evidence.Count(value => value.ConfirmedTrap),
            evidence.Count(value => value.BugGradeDominant),
            evidence.Count(value => value.RescuedEnabler),
            evidence,
            queue);
        return report with { ReproductionHash = StableHash(report) };
    }

    private static TrapOptionEvidence EvaluateOption(
        OptionWitnessContract contract,
        IReadOnlyList<OptionMechanicalWitness> witnesses,
        IReadOnlyList<OptionPairedCounterfactual> pairs,
        IReadOnlyList<OptionContinuationComparison> continuations)
    {
        var defects = MechanicalDefectCodes(contract, witnesses);
        var eligible = witnesses.Where(value => value.Eligible).ToArray();
        var intendedPairs = pairs.Where(value => value.IntendedContext).ToArray();
        var fullPairs = intendedPairs.Where(value => value.FullCensus).ToArray();
        var decisionPairs = fullPairs.Length > 0 ? fullPairs : intendedPairs;
        var comparatorNonWorseRate = Rate(decisionPairs, pair => NonWorse(pair.ComparatorOutcome, pair.OptionOutcome));
        var comparatorStrictRate = Rate(decisionPairs, pair => StrictlyBetter(pair.ComparatorOutcome, pair.OptionOutcome));
        var optionNonWorseRate = Rate(decisionPairs, pair => NonWorse(pair.OptionOutcome, pair.ComparatorOutcome));
        var optionStrictRate = Rate(decisionPairs, pair => StrictlyBetter(pair.OptionOutcome, pair.ComparatorOutcome));
        var medianWinUplift = Median(decisionPairs.Select(pair =>
            pair.OptionOutcome.WinScore - pair.ComparatorOutcome.WinScore));
        var continuationMeasured = continuations.Any(value => value.Measured);
        var continuationAdvantage = continuations.Any(value => value.Measured && value.UniqueOptionAdvantage);
        var tradeoff = contract.HasVisibleTradeoff || pairs.Any(value => value.ExplicitTradeoffVisible);
        var pairedTrap = decisionPairs.Length > 0
                         && comparatorNonWorseRate + Epsilon >= 0.95d
                         && comparatorStrictRate + Epsilon >= 0.50d
                         && contract.PotentialUniqueUnlockCount == 0
                         && !tradeoff;
        var fullPositive = witnesses.Count(value => value.FullCensus && value.PositiveWitness)
                           + fullPairs.Count(value => AnyDimensionBetter(value.OptionOutcome, value.ComparatorOutcome));
        var confirmed = pairedTrap
                        && fullPairs.Length > 0
                        && fullPositive == 0
                        && continuationMeasured
                        && !continuationAdvantage;
        var dominant = decisionPairs.Length > 0
                       && optionNonWorseRate + Epsilon >= 0.95d
                       && optionStrictRate + Epsilon >= 0.80d
                       && medianWinUplift + Epsilon >= 0.25d
                       && !tradeoff;
        var automaticGrade = defects.Contains("eligible_no_effect", StringComparer.Ordinal)
                             || defects.Contains("sign_reversal", StringComparer.Ordinal)
                             || defects.Contains("cost_consumed_state_identical", StringComparer.Ordinal)
                             || defects.Contains("prerequisite_unreachable", StringComparer.Ordinal);
        var stageAFlagged = defects.Count > 0
                            || contract.StructuralTrapCandidate
                            || contract.StructuralDominanceCandidate;
        var rescued = pairedTrap && continuationAdvantage;
        var ownerVerdict = defects.Count > 0 || pairedTrap || dominant;
        var status = ResolveStatus(confirmed, dominant, rescued, defects.Count > 0, pairedTrap, decisionPairs.Length > 0);
        return new TrapOptionEvidence(
            contract.OptionId,
            contract.SubjectKind,
            contract.SubjectId,
            stageAFlagged,
            automaticGrade,
            defects,
            eligible.Length,
            eligible.Sum(value => Math.Max(0, value.FiredCount)),
            witnesses.Count(value => value.PositiveWitness),
            intendedPairs.Length,
            fullPairs.Length,
            comparatorNonWorseRate,
            comparatorStrictRate,
            optionNonWorseRate,
            optionStrictRate,
            medianWinUplift,
            contract.PotentialUniqueUnlockCount,
            fullPositive,
            continuationMeasured,
            continuationAdvantage,
            rescued,
            tradeoff,
            confirmed,
            dominant,
            ownerVerdict,
            status,
            ResolveReason(contract, defects, pairedTrap, confirmed, dominant, rescued, fullPairs.Length, continuationMeasured));
    }

    private static IReadOnlyList<string> MechanicalDefectCodes(
        OptionWitnessContract contract,
        IReadOnlyList<OptionMechanicalWitness> witnesses)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        if (witnesses.Any(value => !value.PrerequisiteReachable))
        {
            codes.Add("prerequisite_unreachable");
        }

        foreach (var group in witnesses.Where(value => value.Eligible)
                     .GroupBy(value => value.PromiseId ?? string.Empty, StringComparer.Ordinal))
        {
            if (group.All(value => value.FiredCount <= 0 && !value.StateChanged))
            {
                codes.Add("eligible_no_effect");
            }

            var promise = contract.Promises.FirstOrDefault(value => value.PromiseId == group.Key);
            if (promise != null && promise.ExpectedDeltaDirection is OptionDeltaDirection.Positive or OptionDeltaDirection.Negative
                                && group.Any(value => Opposite(promise.ExpectedDeltaDirection, value.ActualDeltaDirection)))
            {
                codes.Add("sign_reversal");
            }
        }

        if (witnesses.Any(value => value.Eligible && !value.StackRuleMatches)) codes.Add("stack_rule_mismatch");
        if (witnesses.Any(value => value.Eligible && !value.TargetRuleMatches)) codes.Add("target_rule_mismatch");
        if (witnesses.Any(value => value.Eligible
                                   && value.CostConsumed
                                   && string.Equals(value.StateHashBefore, value.StateHashAfter, StringComparison.Ordinal)))
        {
            codes.Add("cost_consumed_state_identical");
        }

        return codes.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool Opposite(string expected, string actual)
        => expected == OptionDeltaDirection.Positive && actual == OptionDeltaDirection.Negative
           || expected == OptionDeltaDirection.Negative && actual == OptionDeltaDirection.Positive;

    private static bool NonWorse(OptionOutcomeVector candidate, OptionOutcomeVector baseline)
        => Dimensions(candidate).Zip(Dimensions(baseline), (left, right) => left + Epsilon >= right).All(value => value);

    private static bool StrictlyBetter(OptionOutcomeVector candidate, OptionOutcomeVector baseline)
        => NonWorse(candidate, baseline)
           && Dimensions(candidate).Zip(Dimensions(baseline), (left, right) => left > right + Epsilon).Any(value => value);

    private static bool AnyDimensionBetter(OptionOutcomeVector candidate, OptionOutcomeVector baseline)
        => Dimensions(candidate).Zip(Dimensions(baseline), (left, right) => left > right + Epsilon).Any(value => value);

    private static IEnumerable<double> Dimensions(OptionOutcomeVector value)
    {
        yield return value.WinScore;
        yield return value.RemainingHpFraction;
        yield return value.RemainingResource;
        yield return value.ConceptMilestoneCount;
        yield return value.UniquePayoffWitnessCount;
        yield return value.CampaignContinuationScore;
    }

    private static double Rate<T>(IReadOnlyList<T> values, Func<T, bool> predicate)
        => values.Count == 0 ? 0d : (double)values.Count(predicate) / values.Count;

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0) return 0d;
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1
            ? ordered[middle]
            : (ordered[middle - 1] + ordered[middle]) / 2d;
    }

    private static bool IsFlagged(TrapOptionEvidence value)
        => value.StageAFlagged
           || value.ConfirmedTrap
           || value.BugGradeDominant
           || value.RescuedEnabler
           || value.CandidateStatus == "trap_candidate";

    private static OptionTrapReport.OwnerVerdictItem ToOwnerVerdictItem(TrapOptionEvidence value)
    {
        var kind = value.ConfirmedTrap
            ? "confirmed_trap"
            : value.BugGradeDominant
                ? "bug_grade_dominant"
                : value.MechanicalDefectCodes.Count > 0
                    ? "mechanical_defect_candidate"
                    : "trap_candidate";
        return new OptionTrapReport.OwnerVerdictItem(
            value.OptionId,
            kind,
            value.VerdictReason,
            "awaiting_owner_verdict");
    }

    private static string ResolveStatus(
        bool confirmed,
        bool dominant,
        bool rescued,
        bool mechanical,
        bool pairedTrap,
        bool pairedMeasured)
    {
        if (confirmed) return "confirmed_trap";
        if (dominant) return "bug_grade_dominant";
        if (rescued) return "rescued_enabler";
        if (mechanical) return "mechanical_defect_candidate";
        if (pairedTrap) return "trap_candidate";
        return pairedMeasured ? "screened_healthy" : "insufficient_evidence";
    }

    private static string ResolveReason(
        OptionWitnessContract contract,
        IReadOnlyList<string> defects,
        bool pairedTrap,
        bool confirmed,
        bool dominant,
        bool rescued,
        int fullPairCount,
        bool continuationMeasured)
    {
        if (confirmed) return "확정 규칙의 intended pair, full census, positive-witness 부재, continuation 부재를 모두 충족";
        if (dominant) return "동급 comparator 대비 95% non-worse, 80% strictly-better, median win uplift 25%p trigger 충족";
        if (rescued) return "paired 열위였지만 IntentTrackEvaluator가 옵션 고유 continuation 이점을 확인";
        if (defects.Count > 0) return $"기계 witness 후보: {string.Join(",", defects)}";
        if (pairedTrap && fullPairCount == 0) return "screening pair는 trap threshold를 충족했으나 flagged full census가 아직 없음";
        if (pairedTrap && !continuationMeasured) return "full pair threshold를 충족했으나 continuation evidence가 아직 없음";
        if (!contract.PromiseCoverageComplete) return "truth graph promise가 없어 판정 보류";
        if (!contract.ComparatorCoverageComplete) return "동급 sibling comparator가 없어 dominance 판정 보류";
        return "등록 임계치에 해당하지 않음";
    }

    private static string StableHash(OptionTrapReport report)
    {
        var payload = BuildSpaceJson.Serialize(report with { ReproductionHash = string.Empty });
        using var sha256 = SHA256.Create();
        return string.Concat(sha256.ComputeHash(Encoding.UTF8.GetBytes(payload))
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
