using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>진단 record만 소비해 oracle/regret/lookback과 Pro Stage 5 판정 칸을 계산한다.</summary>
public static class SunkenSolvabilityEvaluator
{
    public const string EncounterWallCell = "encounter_wall";
    public const string MixedCell = "mixed_balance_policy";
    public const string PolicyProblemCell = "policy_problem";
    public const string HorizonProblemCell = "horizon_problem";
    public const string PuzzleLockCell = "puzzle_lock";
    public const string SameStateSolvableCell = "same_state_solvable";

    public static SunkenSolvabilityReport Evaluate(
        string runId,
        string targetSiteId,
        IReadOnlyList<SunkenArrivalSnapshotRecord> snapshots,
        IReadOnlyList<SunkenOracleCandidateRecord> candidates,
        string searchMode)
    {
        if (snapshots == null)
        {
            throw new ArgumentNullException(nameof(snapshots));
        }

        if (candidates == null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        var orderedSnapshots = snapshots
            .OrderBy(value => value.SampleId, StringComparer.Ordinal)
            .ToArray();
        var sameState = candidates
            .Where(value => string.Equals(value.Scope, SunkenOracleCandidateRecord.SameStateScope, StringComparison.Ordinal))
            .ToArray();
        var lookback = candidates
            .Where(value => string.Equals(value.Scope, SunkenOracleCandidateRecord.LookbackScope, StringComparison.Ordinal))
            .ToArray();

        var sameStateOracleSuccesses = 0;
        var chosenSuccesses = 0;
        var lookbackOracleSuccesses = 0;
        var availabilityOracleSuccesses = 0;
        var puzzleLockStates = 0;
        foreach (var snapshot in orderedSnapshots)
        {
            var sampleSameState = sameState.Where(value => value.SampleId == snapshot.SampleId).ToArray();
            var sampleLookback = lookback.Where(value => value.SampleId == snapshot.SampleId).ToArray();
            var sameStateWon = sampleSameState.Any(value => value.SiteCompleted);
            var recruitLookbackWon = sampleLookback.Any(value => value.SiteCompleted
                                                                 && !string.IsNullOrWhiteSpace(value.AddedRosterArchetypeId));
            if (sameStateWon)
            {
                sameStateOracleSuccesses++;
            }

            if (sampleSameState.Any(value => value.IsPolicyChoice && value.SiteCompleted))
            {
                chosenSuccesses++;
            }

            if (sameStateWon || sampleLookback.Any(value => value.SiteCompleted))
            {
                lookbackOracleSuccesses++;
            }

            if (sameStateWon || recruitLookbackWon)
            {
                availabilityOracleSuccesses++;
            }

            var winningChoices = sampleSameState
                .Where(value => !value.IsPolicyChoice && value.SiteCompleted)
                .GroupBy(value => $"{value.BuildId}|{value.PlacementId}", StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            if (winningChoices.Select(value => value.BuildId).Distinct(StringComparer.Ordinal).Count() == 1
                && winningChoices.Select(value => value.PlacementId).Distinct(StringComparer.Ordinal).Count() == 1
                && !sampleSameState.Any(value => value.IsPolicyChoice && value.SiteCompleted))
            {
                puzzleLockStates++;
            }
        }

        var denominator = orderedSnapshots.Length;
        var sameStateRate = Rate(sameStateOracleSuccesses, denominator);
        var chosenRate = Rate(chosenSuccesses, denominator);
        var lookbackRate = Rate(lookbackOracleSuccesses, denominator);
        var availabilityRate = Rate(availabilityOracleSuccesses, denominator);
        var selectionRegret = Math.Max(0d, sameStateRate - chosenRate);
        var availabilityGap = Math.Max(0d, availabilityRate - sameStateRate);
        var puzzleRate = Rate(puzzleLockStates, denominator);
        var winningCandidates = sameState.Where(value => !value.IsPolicyChoice && value.SiteCompleted).ToArray();
        var familySummaries = BuildFamilySummaries(orderedSnapshots, sameState);
        var bestFamily = familySummaries.FirstOrDefault()?.CounterFamilyId ?? string.Empty;
        var puzzleSignal = sameStateRate >= 0.75d
                           && puzzleRate >= 0.75d
                           && winningCandidates.Select(value => value.BuildId).Distinct(StringComparer.Ordinal).Count() == 1;
        var decision = Classify(sameStateRate, selectionRegret, lookbackRate, puzzleSignal);

        return new SunkenSolvabilityReport
        {
            RunId = runId ?? string.Empty,
            TargetSiteId = targetSiteId ?? string.Empty,
            SnapshotCount = denominator,
            SameStateCandidateCount = sameState.Length,
            LookbackCandidateCount = lookback.Length,
            FailedCandidateCount = candidates.Count(value => !string.IsNullOrWhiteSpace(value.FailureCode)),
            SameStateOracleWinRate = sameStateRate,
            ChosenWinRate = chosenRate,
            SelectionRegret = selectionRegret,
            AvailabilityGap = availabilityGap,
            OneSiteLookbackOracle = lookbackRate,
            BestCounterFamily = bestFamily,
            WinningBuildCount = winningCandidates.Select(value => value.BuildId).Distinct(StringComparer.Ordinal).Count(),
            WinningPlacementCount = winningCandidates.Select(value => value.PlacementId).Distinct(StringComparer.Ordinal).Count(),
            PuzzleLockStateRate = puzzleRate,
            PuzzleLockSignal = puzzleSignal,
            DecisionCell = decision.Cell,
            DecisionLabelKo = decision.LabelKo,
            DecisionRationale = decision.Rationale,
            SearchMode = searchMode ?? string.Empty,
            CounterFamilies = familySummaries,
        };
    }

    private static IReadOnlyList<SunkenSolvabilityReport.CounterFamilySummary> BuildFamilySummaries(
        IReadOnlyList<SunkenArrivalSnapshotRecord> snapshots,
        IReadOnlyList<SunkenOracleCandidateRecord> sameState)
    {
        return sameState
            .Where(value => !value.IsPolicyChoice && !string.IsNullOrWhiteSpace(value.CounterFamilyId))
            .GroupBy(value => value.CounterFamilyId, StringComparer.Ordinal)
            .Select(group =>
            {
                var sampleBest = snapshots.Select(snapshot => group
                        .Where(value => value.SampleId == snapshot.SampleId)
                        .OrderByDescending(value => value.SiteCompleted)
                        .ThenByDescending(value => value.BattleWinRate)
                        .ThenByDescending(value => value.FinalTeamHpFraction)
                        .ThenBy(value => value.CandidateId, StringComparer.Ordinal)
                        .FirstOrDefault())
                    .Where(value => value != null)
                    .Cast<SunkenOracleCandidateRecord>()
                    .ToArray();
                return new SunkenSolvabilityReport.CounterFamilySummary
                {
                    CounterFamilyId = group.Key,
                    SampleCount = sampleBest.Length,
                    CandidateCount = group.Count(),
                    OracleWinRate = Rate(sampleBest.Count(value => value.SiteCompleted), snapshots.Count),
                    MeanBattleWinRate = sampleBest.Length == 0 ? 0d : sampleBest.Average(value => value.BattleWinRate),
                    MeanFinalTeamHpFraction = sampleBest.Length == 0 ? 0d : sampleBest.Average(value => value.FinalTeamHpFraction),
                };
            })
            .OrderByDescending(value => value.OracleWinRate)
            .ThenByDescending(value => value.MeanBattleWinRate)
            .ThenByDescending(value => value.MeanFinalTeamHpFraction)
            .ThenBy(value => value.CounterFamilyId, StringComparer.Ordinal)
            .ToArray();
    }

    private static (string Cell, string LabelKo, string Rationale) Classify(
        double sameStateOracle,
        double selectionRegret,
        double lookbackOracle,
        bool puzzleLock)
    {
        if (sameStateOracle < 0.60d)
        {
            if (lookbackOracle >= 0.75d)
            {
                return (
                    HorizonProblemCell,
                    "horizon 문제",
                    "same-state oracle은 60% 미만이지만 one-site lookback oracle이 75% 이상이다.");
            }

            return (
                EncounterWallCell,
                "인카운터 벽",
                "same-state oracle과 one-site lookback oracle 모두 벽을 안정적으로 넘지 못한다.");
        }

        if (sameStateOracle < 0.75d)
        {
            return (
                MixedCell,
                "밸런스·정책 혼합",
                "same-state oracle이 60% 이상 75% 미만인 혼합 구간이다.");
        }

        if (puzzleLock)
        {
            return (
                PuzzleLockCell,
                "퍼즐 자물쇠",
                "oracle은 높지만 승리 가능한 build와 placement가 사실상 하나로 수렴한다.");
        }

        if (selectionRegret >= 0.20d)
        {
            return (
                PolicyProblemCell,
                "정책 문제",
                "same-state oracle이 75% 이상이고 실제 선택 regret가 20%p 이상이다.");
        }

        return (
            SameStateSolvableCell,
            "현재 상태에서 해결 가능",
            "same-state oracle이 75% 이상이고 정책 regret가 20%p 미만이다.");
    }

    private static double Rate(int numerator, int denominator)
        => denominator <= 0 ? 0d : (double)numerator / denominator;
}
