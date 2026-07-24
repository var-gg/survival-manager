using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

public static class DropGradeEconomy
{
    public const double FirstClearReferenceKappa = 0.08143976362639153d;

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
        int seed,
        int heat = 0)
        => RollGradeObserved(
            table,
            chapterId,
            fallbackBracket,
            seed,
            heat).Grade;

    internal static DropGradeRollObservation RollGradeObserved(
        DropTableTemplate table,
        string chapterId,
        RarityBracketValue fallbackBracket,
        int seed,
        int heat = 0)
        => RollGradeObserved(
            table,
            chapterId,
            fallbackBracket,
            seed,
            heat,
            EndlessCycleService.HeatDropLatentMeanNumerator,
            EndlessCycleService.HeatDropJackpotWeightStep);

    internal static DropGradeRollObservation RollGradeObserved(
        DropTableTemplate table,
        string chapterId,
        RarityBracketValue fallbackBracket,
        int seed,
        int heat,
        double heatDropLatentMeanNumerator,
        double heatDropJackpotWeightStep)
    {
        var profile = table.GradeProfiles?
            .FirstOrDefault(candidate =>
                string.Equals(candidate.ChapterId, chapterId, StringComparison.Ordinal));
        if (profile == null
            || profile.StandardDeviation <= 0d
            || table.GradePowerKappa <= 0d)
        {
            return FallbackObservation(table, fallbackBracket);
        }

        if (!HasValidJackpot(table))
        {
            return FallbackObservation(table, fallbackBracket);
        }

        var expectedPower = CampaignExpectedItemPower(table);
        var targetPower = Math.Exp(
            FirstClearReferenceKappa * (int)MapRarityBracket(fallbackBracket));
        if (Math.Abs(expectedPower - targetPower) > 0.0005d)
        {
            return FallbackObservation(table, fallbackBracket);
        }

        var ordinaryMean = profile.MeanPreservingLatentMean;
        var jackpotMean = table.GradeJackpotLatentMean;
        var jackpotWeight = table.GradeJackpotWeight;
        var heatShift = EndlessCycleService.DropLatentMeanShift(
            heat,
            heatDropLatentMeanNumerator);
        if (heatShift > 0d)
        {
            ordinaryMean += heatShift;
            jackpotMean += heatShift;
            jackpotWeight = EndlessCycleService.DropJackpotWeight(
                table.GradeJackpotWeight,
                heat,
                heatDropJackpotWeightStep);
        }

        var probabilities = GradeProbabilities(
            ordinaryMean,
            profile.StandardDeviation,
            jackpotWeight,
            jackpotMean,
            table.GradeJackpotStandardDeviation);
        var random = new Random(CampaignEncounterSeed.Derive(seed, "drop-grade"));
        var roll = random.NextDouble();
        var cursor = 0d;
        for (var grade = 0; grade < probabilities.Count; grade++)
        {
            cursor += probabilities[grade];
            if (roll < cursor)
            {
                return BuildObservation(
                    (ItemRarityTierValue)grade,
                    roll,
                    cursor - probabilities[grade],
                    ordinaryMean,
                    profile.StandardDeviation,
                    table.GradeJackpotWeight,
                    jackpotWeight,
                    jackpotMean,
                    table.GradeJackpotStandardDeviation,
                    probabilities);
            }
        }

        return BuildObservation(
            ItemRarityTierValue.Legendary,
            roll,
            cursor - probabilities[^1],
            ordinaryMean,
            profile.StandardDeviation,
            table.GradeJackpotWeight,
            jackpotWeight,
            jackpotMean,
            table.GradeJackpotStandardDeviation,
            probabilities);
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

    public static IReadOnlyList<double> GradeProbabilities(
        double mean,
        double standardDeviation,
        double jackpotWeight,
        double jackpotMean,
        double jackpotStandardDeviation)
    {
        if (!double.IsFinite(jackpotWeight)
            || jackpotWeight < 0d
            || jackpotWeight >= 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(jackpotWeight),
                jackpotWeight,
                "Jackpot weight must be finite and in [0, 1).");
        }

        var ordinary = GradeProbabilities(mean, standardDeviation);
        if (jackpotWeight == 0d)
        {
            return ordinary;
        }

        var jackpot = GradeProbabilities(jackpotMean, jackpotStandardDeviation);
        return ordinary
            .Select((probability, grade) =>
                ((1d - jackpotWeight) * probability)
                + (jackpotWeight * jackpot[grade]))
            .ToArray();
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

    public static double ExpectedItemPower(
        double mean,
        double standardDeviation,
        double kappa,
        double jackpotWeight,
        double jackpotMean,
        double jackpotStandardDeviation)
    {
        return GradeProbabilities(
                mean,
                standardDeviation,
                jackpotWeight,
                jackpotMean,
                jackpotStandardDeviation)
            .Select((probability, grade) => probability * Math.Exp(kappa * grade))
            .Sum();
    }

    public static double CampaignExpectedItemPower(DropTableTemplate table)
    {
        if (table.GradeProfiles is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"Drop table '{table.Id}' has no campaign grade profiles.");
        }

        return table.GradeProfiles
            .Average(profile => ExpectedItemPower(
                profile.MeanPreservingLatentMean,
                profile.StandardDeviation,
                table.GradePowerKappa,
                table.GradeJackpotWeight,
                table.GradeJackpotLatentMean,
                table.GradeJackpotStandardDeviation));
    }

    public static double CalibrateMean(
        ItemRarityTierValue baselineGrade,
        double standardDeviation,
        double kappa)
    {
        return CalibrateMean(
            baselineGrade,
            standardDeviation,
            kappa,
            kappa);
    }

    public static double CalibrateMean(
        ItemRarityTierValue baselineGrade,
        double standardDeviation,
        double kappa,
        double referenceKappa)
    {
        return CalibrateMean(
            baselineGrade,
            standardDeviation,
            kappa,
            referenceKappa,
            jackpotWeight: 0d,
            jackpotMean: 4.25d,
            jackpotStandardDeviation: 0.25d);
    }

    public static double CalibrateMean(
        ItemRarityTierValue baselineGrade,
        double standardDeviation,
        double kappa,
        double referenceKappa,
        double jackpotWeight,
        double jackpotMean,
        double jackpotStandardDeviation)
    {
        var target = Math.Exp(referenceKappa * (int)baselineGrade);
        var low = -12d;
        var high = 12d;
        for (var iteration = 0; iteration < 96; iteration++)
        {
            var midpoint = low + ((high - low) / 2d);
            if (ExpectedItemPower(
                    midpoint,
                    standardDeviation,
                    kappa,
                    jackpotWeight,
                    jackpotMean,
                    jackpotStandardDeviation) < target)
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

    private static bool HasValidJackpot(DropTableTemplate table)
    {
        return double.IsFinite(table.GradeJackpotWeight)
               && table.GradeJackpotWeight >= 0d
               && table.GradeJackpotWeight < 1d
               && (table.GradeJackpotWeight == 0d
                   || (double.IsFinite(table.GradeJackpotLatentMean)
                       && double.IsFinite(table.GradeJackpotStandardDeviation)
                       && table.GradeJackpotStandardDeviation > 0d));
    }

    private static DropGradeRollObservation BuildObservation(
        ItemRarityTierValue grade,
        double roll,
        double gradeIntervalStart,
        double ordinaryMean,
        double ordinaryStandardDeviation,
        double baseJackpotWeight,
        double effectiveJackpotWeight,
        double jackpotMean,
        double jackpotStandardDeviation,
        IReadOnlyList<double> probabilities)
    {
        var gradeIndex = (int)grade;
        var ordinaryProbability = GradeProbabilities(
            ordinaryMean,
            ordinaryStandardDeviation)[gradeIndex];
        var ordinaryMass = (1d - effectiveJackpotWeight) * ordinaryProbability;
        var jackpotSelected = effectiveJackpotWeight > 0d
                              && roll >= gradeIntervalStart + ordinaryMass;
        return new DropGradeRollObservation(
            grade,
            jackpotSelected,
            baseJackpotWeight,
            effectiveJackpotWeight,
            probabilities,
            roll,
            UsedFallback: false);
    }

    private static DropGradeRollObservation FallbackObservation(
        DropTableTemplate table,
        RarityBracketValue fallbackBracket)
    {
        var grade = MapRarityBracket(fallbackBracket);
        var probabilities = new double[5];
        probabilities[(int)grade] = 1d;
        var baseWeight = double.IsFinite(table.GradeJackpotWeight)
            ? table.GradeJackpotWeight
            : 0d;
        return new DropGradeRollObservation(
            grade,
            JackpotComponentSelected: false,
            BaseJackpotWeight: baseWeight,
            EffectiveJackpotWeight: baseWeight,
            GradeProbabilities: probabilities,
            RandomRoll: double.NaN,
            UsedFallback: true);
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
