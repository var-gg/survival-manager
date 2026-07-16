using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>동일 seed의 healer 포함/미포함 pair에서 승률과 최종 전력차 marginal value를 계산한다.</summary>
public static class HealerMarginalValueEvaluator
{
    public sealed record Result(
        IReadOnlyList<HealerMarginalValueRecord> Records,
        int PositiveStateCount,
        int AlignedPositiveStateCount,
        double PositiveSelectionAlignmentRate);

    public static Result Evaluate(IReadOnlyList<FormationBattleRecord> battles)
    {
        if (battles == null)
        {
            throw new ArgumentNullException(nameof(battles));
        }

        var records = battles
            .Where(record => record.IsHealerComparison
                             && string.IsNullOrWhiteSpace(record.FailureCode)
                             && !string.IsNullOrWhiteSpace(record.HealerComparisonId))
            .GroupBy(record => record.HealerComparisonId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(EvaluateComparison)
            .Where(record => record != null)
            .Cast<HealerMarginalValueRecord>()
            .ToArray();
        var positive = records.Where(record => record.PositiveMarginalValue).ToArray();
        var aligned = positive.Count(record => record.SelectionAligned);
        return new Result(
            records,
            positive.Length,
            aligned,
            positive.Length == 0 ? 0d : aligned / (double)positive.Length);
    }

    private static HealerMarginalValueRecord? EvaluateComparison(
        IGrouping<string, FormationBattleRecord> group)
    {
        var withHealer = group.Where(record => record.ContainsHealer).ToArray();
        var withoutHealer = group.Where(record => !record.ContainsHealer).ToArray();
        if (withHealer.Length == 0 || withoutHealer.Length == 0)
        {
            return null;
        }

        var withWinRate = WinRate(withHealer);
        var withoutWinRate = WinRate(withoutHealer);
        var winDelta = withWinRate - withoutWinRate;
        var powerDelta = withHealer.Average(record => record.NormalizedFinalPowerDifference)
                         - withoutHealer.Average(record => record.NormalizedFinalPowerDifference);
        var marginal = winDelta + (powerDelta * 0.5d);
        var positive = marginal > 0.000000001d;
        var selected = group.Any(record => record.CompetentSelectedHealer);
        return new HealerMarginalValueRecord(
            group.Key,
            group.Select(record => record.Seed).Distinct().Count(),
            withWinRate,
            withoutWinRate,
            winDelta,
            powerDelta,
            marginal,
            positive,
            selected,
            !positive || selected);
    }

    private static double WinRate(IReadOnlyCollection<FormationBattleRecord> records)
        => records.Count(record => string.Equals(record.WinnerSide, "ally", StringComparison.Ordinal))
           / (double)records.Count;
}
