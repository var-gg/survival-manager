using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>같은 편성·적에서 census medoid placement별 승률을 비교한다.</summary>
public static class PlacementLeverageEvaluator
{
    public sealed record Result(
        IReadOnlyList<PlacementLeverageRecord> Records,
        double MedianLeverage,
        double SensitiveMedianLeverage,
        double LeverageP90,
        double DefaultOptimalRate);

    public static Result Evaluate(IReadOnlyList<FormationBattleRecord> battles)
    {
        if (battles == null)
        {
            throw new ArgumentNullException(nameof(battles));
        }

        var records = battles
            .Where(record => string.IsNullOrWhiteSpace(record.FailureCode)
                             && !string.IsNullOrWhiteSpace(record.PlacementSetId)
                             && !record.IsHealerComparison)
            .GroupBy(record => record.PlacementSetId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(EvaluateSet)
            .Where(record => record != null)
            .Cast<PlacementLeverageRecord>()
            .ToArray();
        var leverages = records.Select(record => record.WinRateLeverage).OrderBy(value => value).ToArray();
        var sensitive = records.Where(record => record.FormationSensitive)
            .Select(record => record.WinRateLeverage)
            .OrderBy(value => value)
            .ToArray();
        return new Result(
            records,
            Percentile(leverages, 0.5d),
            Percentile(sensitive, 0.5d),
            Percentile(leverages, 0.9d),
            records.Length == 0 ? 0d : records.Count(record => record.DefaultWasOptimal) / (double)records.Length);
    }

    private static PlacementLeverageRecord? EvaluateSet(IGrouping<string, FormationBattleRecord> group)
    {
        var defaultBattles = group.Where(record => record.IsDefaultPlacement).ToArray();
        if (defaultBattles.Length == 0)
        {
            return null;
        }

        var variants = group.GroupBy(record => record.PlacementVariantId, StringComparer.Ordinal)
            .Select(variant => new
            {
                Id = variant.Key,
                WinRate = variant.Count(record => string.Equals(record.WinnerSide, "ally", StringComparison.Ordinal))
                          / (double)variant.Count(),
            })
            .OrderByDescending(variant => variant.WinRate)
            .ThenBy(variant => variant.Id, StringComparer.Ordinal)
            .ToArray();
        var defaultWinRate = defaultBattles.Count(record => string.Equals(record.WinnerSide, "ally", StringComparison.Ordinal))
                             / (double)defaultBattles.Length;
        var best = variants[0];
        var leverage = Math.Max(0d, best.WinRate - defaultWinRate);
        var sensitive = group.SelectMany(record => record.Channels).Any(channel => channel.Eligible);
        return new PlacementLeverageRecord(
            group.Key,
            group.Select(record => record.PolicyId).OrderBy(id => id, StringComparer.Ordinal).First(),
            group.Select(record => record.Seed).Distinct().Count(),
            variants.Length,
            defaultWinRate,
            best.WinRate,
            leverage,
            leverage <= 0.000000001d,
            sensitive,
            best.Id);
    }

    private static double Percentile(IReadOnlyList<double> sorted, double percentile)
    {
        if (sorted.Count == 0)
        {
            return 0d;
        }

        var position = (sorted.Count - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return sorted[lower];
        }

        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
    }
}
