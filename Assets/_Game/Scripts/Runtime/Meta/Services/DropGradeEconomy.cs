using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class DropGradeEconomy
{
    private static readonly double[] CutPoints = { 0.5d, 1.5d, 2.5d, 3.5d };

    public static ItemRarityTierValue MapRarityBracket(RarityBracketValue bracket)
    {
        return bracket switch
        {
            RarityBracketValue.Advanced => ItemRarityTierValue.Magic,
            RarityBracketValue.Elite => ItemRarityTierValue.Rare,
            RarityBracketValue.Boss => ItemRarityTierValue.Epic,
            _ => ItemRarityTierValue.Common,
        };
    }

    public static ItemRarityTierValue RollGrade(
        DropTableTemplate table,
        string chapterId,
        RarityBracketValue fallbackBracket,
        int seed)
    {
        var profile = table.GradeProfiles?
            .FirstOrDefault(candidate =>
                string.Equals(candidate.ChapterId, chapterId, StringComparison.Ordinal));
        if (profile == null
            || profile.StandardDeviation <= 0d
            || table.GradePowerKappa <= 0d)
        {
            return MapRarityBracket(fallbackBracket);
        }

        var expectedPower = ExpectedItemPower(
            profile.MeanPreservingLatentMean,
            profile.StandardDeviation,
            table.GradePowerKappa);
        var targetPower = Math.Exp(table.GradePowerKappa * (int)MapRarityBracket(fallbackBracket));
        if (Math.Abs(expectedPower - targetPower) > 0.0005d)
        {
            return MapRarityBracket(fallbackBracket);
        }

        var probabilities = GradeProbabilities(
            profile.MeanPreservingLatentMean,
            profile.StandardDeviation);
        var random = new Random(CampaignEncounterSeed.Derive(seed, "drop-grade"));
        var roll = random.NextDouble();
        var cursor = 0d;
        for (var grade = 0; grade < probabilities.Count; grade++)
        {
            cursor += probabilities[grade];
            if (roll < cursor)
            {
                return (ItemRarityTierValue)grade;
            }
        }

        return ItemRarityTierValue.Legendary;
    }

    public static IReadOnlyList<double> GradeProbabilities(double mean, double standardDeviation)
    {
        if (!double.IsFinite(mean)
            || !double.IsFinite(standardDeviation)
            || standardDeviation <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(standardDeviation),
                standardDeviation,
                "Latent grade standard deviation must be finite and positive.");
        }

        var result = new double[5];
        var previous = 0d;
        for (var index = 0; index < CutPoints.Length; index++)
        {
            var cumulative = NormalCdf((CutPoints[index] - mean) / standardDeviation);
            result[index] = Math.Max(0d, cumulative - previous);
            previous = cumulative;
        }

        result[4] = Math.Max(0d, 1d - previous);
        var total = result.Sum();
        if (total <= 0d)
        {
            throw new InvalidOperationException("Latent grade probabilities have no mass.");
        }

        for (var index = 0; index < result.Length; index++)
        {
            result[index] /= total;
        }

        return result;
    }

    public static double ExpectedItemPower(
        double mean,
        double standardDeviation,
        double kappa)
    {
        return GradeProbabilities(mean, standardDeviation)
            .Select((probability, grade) => probability * Math.Exp(kappa * grade))
            .Sum();
    }

    public static double CalibrateMean(
        ItemRarityTierValue baselineGrade,
        double standardDeviation,
        double kappa)
    {
        var target = Math.Exp(kappa * (int)baselineGrade);
        var low = -12d;
        var high = 12d;
        for (var iteration = 0; iteration < 96; iteration++)
        {
            var midpoint = low + ((high - low) / 2d);
            if (ExpectedItemPower(midpoint, standardDeviation, kappa) < target)
            {
                low = midpoint;
            }
            else
            {
                high = midpoint;
            }
        }

        return low + ((high - low) / 2d);
    }

    private static double NormalCdf(double value)
    {
        return 0.5d * (1d + Erf(value / Math.Sqrt(2d)));
    }

    private static double Erf(double value)
    {
        var sign = value < 0d ? -1d : 1d;
        var x = Math.Abs(value);
        const double a1 = 0.254829592d;
        const double a2 = -0.284496736d;
        const double a3 = 1.421413741d;
        const double a4 = -1.453152027d;
        const double a5 = 1.061405429d;
        const double p = 0.3275911d;
        var t = 1d / (1d + (p * x));
        var approximation = 1d
                            - (((((a5 * t) + a4) * t + a3) * t + a2) * t + a1)
                            * t
                            * Math.Exp(-x * x);
        return sign * approximation;
    }
}
