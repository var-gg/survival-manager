using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Editor.Validation;

internal sealed record CampaignWallDeficitSearchResult(
    double? AdditionalLogDeficit,
    int EvaluationCount,
    bool MonotonicityViolated,
    bool RightCensored);

/// <summary>Observed wall battle에서 최소 추가 log-power를 찾는 단조 이진 탐색.</summary>
internal static class CampaignWallDeficitSearch
{
    internal static CampaignWallDeficitSearchResult FindMinimumWinningCorrection(
        Func<double, bool> wins,
        double searchMaximum,
        double tolerance)
    {
        if (wins == null)
        {
            throw new ArgumentNullException(nameof(wins));
        }

        if (!double.IsFinite(searchMaximum) || searchMaximum <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(searchMaximum));
        }

        if (!double.IsFinite(tolerance) || tolerance <= 0d || tolerance >= searchMaximum)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        }

        var evaluations = new SortedDictionary<double, bool>();
        bool Evaluate(double additionalLogPower)
        {
            if (!evaluations.TryGetValue(additionalLogPower, out var won))
            {
                won = wins(additionalLogPower);
                evaluations.Add(additionalLogPower, won);
            }

            return won;
        }

        if (Evaluate(0d))
        {
            throw new InvalidOperationException(
                "Per-wall deficit search requires an actually observed losing battle at zero correction.");
        }

        var low = 0d;
        var high = Math.Min(0.125d, searchMaximum);
        while (high < searchMaximum && !Evaluate(high))
        {
            low = high;
            high = Math.Min(searchMaximum, high * 2d);
        }

        if (!Evaluate(high))
        {
            return new CampaignWallDeficitSearchResult(
                null,
                evaluations.Count,
                HasMonotonicityViolation(evaluations),
                true);
        }

        while (high - low > tolerance)
        {
            var midpoint = low + ((high - low) / 2d);
            if (Evaluate(midpoint))
            {
                high = midpoint;
            }
            else
            {
                low = midpoint;
            }
        }

        return new CampaignWallDeficitSearchResult(
            high,
            evaluations.Count,
            HasMonotonicityViolation(evaluations),
            false);
    }

    private static bool HasMonotonicityViolation(
        IReadOnlyDictionary<double, bool> evaluations)
    {
        var seenWin = false;
        foreach (var pair in evaluations.OrderBy(value => value.Key))
        {
            if (pair.Value)
            {
                seenWin = true;
            }
            else if (seenWin)
            {
                return true;
            }
        }

        return false;
    }
}
