using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class AffixQualityProfilePhase1Tests
{
    private const int SamplesPerProfile = 100_000;
    private const double SimultaneousSigma = 6d;

    [Test]
    public void ShippedRefitBalance_IsSerializedWithTheAuthoritativeFloorSchedule()
    {
        var balance = new RuntimeCombatContentLookup().Snapshot.RefitBalance;

        Assert.That(balance, Is.Not.Null);
        Assert.That(balance!.MaximumFloorNumerator, Is.EqualTo(70));
        Assert.That(balance.MaximumFloorDenominator, Is.EqualTo(100));
        Assert.That(balance.FloorDecayNumerator, Is.EqualTo(55));
        Assert.That(balance.FloorDecayDenominator, Is.EqualTo(100));
        Assert.That(balance.SealCostMultiplierPerLockedAffix, Is.EqualTo(0.50d));
        Assert.That(RefitFloorSchedule.ToDouble(balance.FloorScheduleQ64[0]), Is.EqualTo(0.315d).Within(1e-12));
        Assert.That(RefitFloorSchedule.ToDouble(balance.FloorScheduleQ64[1]), Is.EqualTo(0.48825d).Within(1e-12));
        Assert.That(RefitFloorSchedule.ToDouble(balance.FloorScheduleQ64[2]), Is.EqualTo(0.5835375d).Within(1e-12));
    }

    [Test]
    [Timeout(30 * 60 * 1000)]
    public void EveryShippedProfile_MatchesProductionSelectorWithinBinomialBounds()
    {
        var lookup = new RuntimeCombatContentLookup();
        var gradeStepBudget = lookup.Snapshot.DropTables!.Values
            .Select(table => table.GradeStepBudgetScore)
            .Distinct()
            .Single();
        var compiler = new AffixQualityProfileCompiler();
        var disagreements = new List<string>();
        var profilesCompiled = 0;
        var q70EqualsQ80 = 0;
        var aggregateCompileSeconds = 0d;

        foreach (var itemBaseId in lookup.GetCanonicalItemIds()
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            foreach (var grade in Enum.GetValues(typeof(ItemRarityTierValue))
                         .Cast<ItemRarityTierValue>()
                         .OrderBy(value => (int)value))
            {
                var profile = compiler.Compile(
                    lookup,
                    itemBaseId,
                    grade,
                    gradeStepBudget,
                    "shipped-affix-catalog-v1",
                    out var metrics);
                profilesCompiled++;
                aggregateCompileSeconds += metrics.Elapsed.TotalSeconds;
                ValidateFixedPointProfile(profile);

                var observed = SampleProductionScores(
                    lookup,
                    profile,
                    gradeStepBudget);
                CompareAgainstBinomialBounds(
                    profile,
                    observed,
                    disagreements);

                var q10 = Quantile(profile, 1, 10);
                var q50 = Quantile(profile, 1, 2);
                var q70 = Quantile(profile, 7, 10);
                var q80 = Quantile(profile, 4, 5);
                var q90 = Quantile(profile, 9, 10);
                if (q70 == q80)
                {
                    q70EqualsQ80++;
                }

                TestContext.Out.WriteLine(
                    "REFIT_A1_PROFILE "
                    + $"profile={itemBaseId}|{profile.Key.SlotType}|{grade} "
                    + $"states={metrics.DistinctMemoizedStates} "
                    + $"seconds={metrics.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} "
                    + $"support={profile.SupportScoreQ.Count} "
                    + $"q10={FormatScore(q10)} "
                    + $"q50={FormatScore(q50)} "
                    + $"q70={FormatScore(q70)} "
                    + $"q80={FormatScore(q80)} "
                    + $"q90={FormatScore(q90)} "
                    + $"mass_q70={FormatMass(profile.GetMassQ64(q70))} "
                    + $"mass_q80={FormatMass(profile.GetMassQ64(q80))} "
                    + $"q70_eq_q80={(q70 == q80 ? "true" : "false")}");
            }
        }

        TestContext.Out.WriteLine(
            "REFIT_A1_PHASE1 "
            + $"profiles={profilesCompiled} "
            + $"samples_per_profile={SamplesPerProfile} "
            + $"compile_seconds={aggregateCompileSeconds.ToString("F6", CultureInfo.InvariantCulture)} "
            + $"q70_eq_q80={q70EqualsQ80} "
            + $"disagreements={disagreements.Count}");
        foreach (var disagreement in disagreements)
        {
            TestContext.Out.WriteLine($"REFIT_A1_DISAGREEMENT {disagreement}");
        }

        Assert.That(
            disagreements,
            Is.Empty,
            "Compiled CDF disagreed with production selector. See REFIT_A1_DISAGREEMENT output.");
        Assert.That(profilesCompiled, Is.EqualTo(210), "shipped matrix remains 42 items x 5 grades");
        Assert.That(
            q70EqualsQ80,
            Is.EqualTo(144),
            "the family-filtered shipped affix catalog retains its measured q70=q80 collapse count");
    }

    private static void ValidateFixedPointProfile(AffixQualityProfile profile)
    {
        Assert.That(profile.SupportScoreQ, Is.Not.Empty);
        Assert.That(profile.SupportScoreQ, Is.Ordered.Ascending);
        Assert.That(
            profile.SupportScoreQ.Distinct().Count(),
            Is.EqualTo(profile.SupportScoreQ.Count));
        Assert.That(profile.CdfQ64, Is.Ordered.Ascending);
        Assert.That(profile.CdfQ64[^1], Is.EqualTo(ulong.MaxValue));
        Assert.That(
            profile.MassQ64.Aggregate(
                System.Numerics.BigInteger.Zero,
                (sum, mass) => sum + mass),
            Is.EqualTo(new System.Numerics.BigInteger(ulong.MaxValue)));
    }

    private static Dictionary<int, int> SampleProductionScores(
        RuntimeCombatContentLookup lookup,
        AffixQualityProfile profile,
        float gradeStepBudget)
    {
        var observed = new Dictionary<int, int>();
        for (var seed = 0; seed < SamplesPerProfile; seed++)
        {
            var selected = GeneratedItemAffixSelector.Select(
                lookup,
                profile.Key.ItemBaseId,
                seed,
                profile.Key.Grade,
                gradeStepBudget);
            var totalScoreQ = selected.Sum(affixId =>
                AffixQualityProfileCompiler.ToBudgetScoreQ(
                    lookup.Snapshot.AffixCatalog![affixId].BudgetScore));
            observed.TryGetValue(totalScoreQ, out var count);
            observed[totalScoreQ] = count + 1;
        }

        return observed;
    }

    private static void CompareAgainstBinomialBounds(
        AffixQualityProfile profile,
        IReadOnlyDictionary<int, int> observed,
        ICollection<string> disagreements)
    {
        foreach (var score in profile.SupportScoreQ)
        {
            observed.TryGetValue(score, out var actual);
            var probability = profile.GetMassQ64(score) / (double)ulong.MaxValue;
            var expected = SamplesPerProfile * probability;
            var standardDeviation = Math.Sqrt(
                SamplesPerProfile * probability * (1d - probability));
            var tolerance = (SimultaneousSigma * standardDeviation) + 4d;
            if (Math.Abs(actual - expected) > tolerance)
            {
                disagreements.Add(
                    $"profile={profile.Key.ItemBaseId}|{profile.Key.SlotType}|{profile.Key.Grade} "
                    + $"score={FormatScore(score)} expected={expected.ToString("F3", CultureInfo.InvariantCulture)} "
                    + $"observed={actual} tolerance={tolerance.ToString("F3", CultureInfo.InvariantCulture)}");
            }
        }

        foreach (var (score, actual) in observed)
        {
            if (profile.GetMassQ64(score) == 0UL)
            {
                disagreements.Add(
                    $"profile={profile.Key.ItemBaseId}|{profile.Key.SlotType}|{profile.Key.Grade} "
                    + $"unexpected_score={FormatScore(score)} observed={actual}");
            }
        }
    }

    private static int Quantile(
        AffixQualityProfile profile,
        ulong numerator,
        ulong denominator)
    {
        return profile.GetQuantileScoreQ(
            AffixQualityProfile.ProbabilityFromFraction(numerator, denominator));
    }

    private static string FormatScore(int scoreQ)
    {
        return (scoreQ / (double)AffixQualityProfileCompiler.ScoreScale)
            .ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatMass(ulong massQ64)
    {
        return (massQ64 / (double)ulong.MaxValue)
            .ToString("R", CultureInfo.InvariantCulture);
    }
}
