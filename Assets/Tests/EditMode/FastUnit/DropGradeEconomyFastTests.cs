using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Tests.EditMode.Fakes;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("FastUnit")]
public sealed class DropGradeEconomyFastTests
{
    private const double MeasuredKappa = 0.15490852440732852d;

    [TestCase(RarityBracketValue.Common, ItemRarityTierValue.Common)]
    [TestCase(RarityBracketValue.Advanced, ItemRarityTierValue.Magic)]
    [TestCase(RarityBracketValue.Elite, ItemRarityTierValue.Rare)]
    [TestCase(RarityBracketValue.Boss, ItemRarityTierValue.Epic)]
    public void RarityBracket_MapsToFiveGradeBaseline(
        RarityBracketValue bracket,
        ItemRarityTierValue expected)
    {
        Assert.That(DropGradeEconomy.MapRarityBracket(bracket), Is.EqualTo(expected));
    }

    [TestCase(ItemRarityTierValue.Magic, 0.9183295d)]
    [TestCase(ItemRarityTierValue.Rare, 1.9466282d)]
    [TestCase(ItemRarityTierValue.Epic, 2.9796247d)]
    public void MeanCalibration_PreservesExpectedItemPower(
        ItemRarityTierValue baseline,
        double expectedMean)
    {
        var calibrated = DropGradeEconomy.CalibrateMean(baseline, 0.78d, MeasuredKappa);
        var expectedPower = Math.Exp(MeasuredKappa * (int)baseline);
        var actualPower = DropGradeEconomy.ExpectedItemPower(
            calibrated,
            0.78d,
            MeasuredKappa);

        Assert.That(calibrated, Is.EqualTo(expectedMean).Within(0.000001d));
        Assert.That(actualPower, Is.EqualTo(expectedPower).Within(0.000000001d));
    }

    [TestCase(ItemRarityTierValue.Magic, 0.25d, 0.5039858d)]
    [TestCase(ItemRarityTierValue.Rare, 0.25d, 1.0999721d)]
    [TestCase(ItemRarityTierValue.Epic, 0.25d, 1.5365781d)]
    [TestCase(ItemRarityTierValue.Magic, 0.5d, 0.4780046d)]
    [TestCase(ItemRarityTierValue.Rare, 0.5d, 1.0256955d)]
    [TestCase(ItemRarityTierValue.Epic, 0.5d, 1.5500932d)]
    public void ReferenceMeanCalibration_PreservesPreKappaFirstClearPower(
        ItemRarityTierValue baseline,
        double standardDeviation,
        double expectedMean)
    {
        var calibrated = DropGradeEconomy.CalibrateMean(
            baseline,
            standardDeviation,
            MeasuredKappa,
            DropGradeEconomy.FirstClearReferenceKappa);
        var expectedPower = Math.Exp(
            DropGradeEconomy.FirstClearReferenceKappa * (int)baseline);
        var actualPower = DropGradeEconomy.ExpectedItemPower(
            calibrated,
            standardDeviation,
            MeasuredKappa);

        Assert.That(calibrated, Is.EqualTo(expectedMean).Within(0.000001d));
        Assert.That(actualPower, Is.EqualTo(expectedPower).Within(0.000000001d));
    }

    [Test]
    public void RollGrade_IsDeterministicAndUsesAuthoredVariance()
    {
        var table = BuildTable(
            ItemRarityTierValue.Rare,
            DropGradeEconomy.CalibrateMean(
                ItemRarityTierValue.Rare,
                0.78d,
                MeasuredKappa,
                DropGradeEconomy.FirstClearReferenceKappa));
        var first = Enumerable.Range(0, 512)
            .Select(seed => DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                RarityBracketValue.Elite,
                seed))
            .ToArray();
        var second = Enumerable.Range(0, 512)
            .Select(seed => DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                RarityBracketValue.Elite,
                seed))
            .ToArray();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.Distinct().Count(), Is.GreaterThanOrEqualTo(4));
    }

    [TestCase(RarityBracketValue.Advanced)]
    [TestCase(RarityBracketValue.Elite)]
    [TestCase(RarityBracketValue.Boss)]
    public void RollGrade_HeatFiveRaisesExpectedGradeForEveryNodeType(
        RarityBracketValue bracket)
    {
        const double jackpotWeight = 0.011d;
        const double standardDeviation = 0.5d;
        var baseline = DropGradeEconomy.MapRarityBracket(bracket);
        var mean = DropGradeEconomy.CalibrateMean(
            baseline,
            standardDeviation,
            MeasuredKappa,
            DropGradeEconomy.FirstClearReferenceKappa,
            jackpotWeight,
            jackpotMean: 4.25d,
            jackpotStandardDeviation: 0.25d);
        var table = BuildTable(
            baseline,
            new[] { mean },
            jackpotWeight,
            standardDeviation);
        var shift = EndlessCycleService.DropLatentMeanShift(5);
        var heatZeroProbabilities = DropGradeEconomy.GradeProbabilities(
            mean,
            standardDeviation,
            jackpotWeight,
            jackpotMean: 4.25d,
            jackpotStandardDeviation: 0.25d);
        var heatFiveProbabilities = DropGradeEconomy.GradeProbabilities(
            mean + shift,
            standardDeviation,
            EndlessCycleService.DropJackpotWeight(jackpotWeight, 5),
            jackpotMean: 4.25d + shift,
            jackpotStandardDeviation: 0.25d);
        var expectedAtHeatZero = ExpectedGrade(heatZeroProbabilities);
        var expectedAtHeatFive = ExpectedGrade(heatFiveProbabilities);

        var sampledAtHeatZero = Enumerable.Range(0, 4096)
            .Average(seed => (double)DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                bracket,
                seed,
                heat: 0));
        var sampledAtHeatFive = Enumerable.Range(0, 4096)
            .Average(seed => (double)DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                bracket,
                seed,
                heat: 5));

        Assert.That(expectedAtHeatFive, Is.GreaterThan(expectedAtHeatZero));
        Assert.That(sampledAtHeatFive, Is.GreaterThan(sampledAtHeatZero));
        Assert.That(sampledAtHeatZero, Is.EqualTo(expectedAtHeatZero).Within(0.04d));
        Assert.That(sampledAtHeatFive, Is.EqualTo(expectedAtHeatFive).Within(0.04d));
    }

    [Test]
    public void HeatZero_UsesExactLegacyProbabilitiesAndRolls()
    {
        const double standardDeviation = 0.5d;
        const double jackpotWeight = 0.011d;
        const double jackpotMean = 4.25d;
        const double jackpotStandardDeviation = 0.25d;
        var mean = DropGradeEconomy.CalibrateMean(
            ItemRarityTierValue.Rare,
            standardDeviation,
            MeasuredKappa,
            DropGradeEconomy.FirstClearReferenceKappa,
            jackpotWeight,
            jackpotMean,
            jackpotStandardDeviation);
        var shift = EndlessCycleService.DropLatentMeanShift(0);
        var legacyProbabilities = DropGradeEconomy.GradeProbabilities(
            mean,
            standardDeviation,
            jackpotWeight,
            jackpotMean,
            jackpotStandardDeviation);
        var heatZeroProbabilities = DropGradeEconomy.GradeProbabilities(
            mean + shift,
            standardDeviation,
            jackpotWeight,
            jackpotMean + shift,
            jackpotStandardDeviation);
        var table = BuildTable(
            ItemRarityTierValue.Rare,
            new[] { mean },
            jackpotWeight,
            standardDeviation);
        var defaultRolls = Enumerable.Range(0, 2048)
            .Select(seed => DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                RarityBracketValue.Elite,
                seed))
            .ToArray();
        var explicitHeatZeroRolls = Enumerable.Range(0, 2048)
            .Select(seed => DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                RarityBracketValue.Elite,
                seed,
                heat: 0))
            .ToArray();
        var legacyRolls = Enumerable.Range(0, 2048)
            .Select(seed => LegacyRollGrade(
                table,
                "chapter_alpha",
                RarityBracketValue.Elite,
                seed))
            .ToArray();

        Assert.That(shift, Is.EqualTo(0d));
        Assert.That(heatZeroProbabilities, Is.EqualTo(legacyProbabilities));
        Assert.That(explicitHeatZeroRolls, Is.EqualTo(defaultRolls));
        Assert.That(explicitHeatZeroRolls, Is.EqualTo(legacyRolls));
    }

    [Test]
    public void TryResolveBundle_ForwardsHeatIntoItemGradeRoll()
    {
        const double jackpotWeight = 0.011d;
        const double standardDeviation = 0.5d;
        var mean = DropGradeEconomy.CalibrateMean(
            ItemRarityTierValue.Rare,
            standardDeviation,
            MeasuredKappa,
            DropGradeEconomy.FirstClearReferenceKappa,
            jackpotWeight,
            jackpotMean: 4.25d,
            jackpotStandardDeviation: 0.25d);
        var table = BuildTable(
            ItemRarityTierValue.Rare,
            new[] { mean },
            jackpotWeight,
            standardDeviation) with
        {
            Id = "drop.heat",
            RewardSourceId = "reward.heat",
            Entries = new[]
            {
                new LootBundleEntryTemplate(
                    "item.heat",
                    RewardType.Item,
                    1,
                    RarityBracketValue.Elite,
                    1,
                    true,
                    Array.Empty<string>()),
            },
        };
        var snapshot = EditorFreeCombatContentFixture.CreateSnapshot(
            campaignChapters: new Dictionary<string, CampaignChapterTemplate>(StringComparer.Ordinal)
            {
                ["chapter_alpha"] = new CampaignChapterTemplate(
                    "chapter_alpha",
                    "Chapter Alpha",
                    0,
                    Array.Empty<string>(),
                    false),
            },
            rewardSources: new Dictionary<string, RewardSourceTemplate>(StringComparer.Ordinal)
            {
                ["reward.heat"] = new RewardSourceTemplate(
                    "reward.heat",
                    "Heat",
                    RewardSourceKindValue.Elite,
                    table.Id,
                    true,
                    new[] { RarityBracketValue.Elite }),
            },
            dropTables: new Dictionary<string, DropTableTemplate>(StringComparer.Ordinal)
            {
                [table.Id] = table,
            });
        var service = new LootResolutionService(snapshot);
        var heatZero = Enumerable.Range(0, 2048)
            .Average(seed => ResolveBundleGrade(service, seed, heat: 0));
        var heatFive = Enumerable.Range(0, 2048)
            .Average(seed => ResolveBundleGrade(service, seed, heat: 5));

        Assert.That(heatFive, Is.GreaterThan(heatZero));
    }

    [Test]
    public void GradeProbabilities_JackpotMixtureMovesMassIntoLegendaryTail()
    {
        var ordinary = DropGradeEconomy.GradeProbabilities(0.5d, 0.5d);
        var mixed = DropGradeEconomy.GradeProbabilities(
            0.5d,
            0.5d,
            jackpotWeight: 0.01d,
            jackpotMean: 4.25d,
            jackpotStandardDeviation: 0.25d);

        Assert.That(mixed.Sum(), Is.EqualTo(1d).Within(0.000000000001d));
        Assert.That(mixed[(int)ItemRarityTierValue.Legendary],
            Is.GreaterThan(ordinary[(int)ItemRarityTierValue.Legendary] + 0.009d));
        Assert.That(mixed[(int)ItemRarityTierValue.Common],
            Is.LessThan(ordinary[(int)ItemRarityTierValue.Common]));
    }

    [Test]
    public void CampaignMeanGuard_AllowsProgressionWhenTableAverageIsPreserved()
    {
        const double chapterStep = 0.4d;
        const double jackpotWeight = 0.01d;
        var center = CalibrateCampaignCenter(
            ItemRarityTierValue.Rare,
            chapterStep,
            jackpotWeight);
        var table = BuildTable(
            ItemRarityTierValue.Rare,
            new[] { center - chapterStep, center, center + chapterStep },
            jackpotWeight,
            standardDeviation: 0.5d);

        var target = Math.Exp(
            DropGradeEconomy.FirstClearReferenceKappa * (int)ItemRarityTierValue.Rare);
        Assert.That(
            DropGradeEconomy.CampaignExpectedItemPower(table),
            Is.EqualTo(target).Within(0.00000001d));

        var grades = Enumerable.Range(0, 512)
            .Select(seed => DropGradeEconomy.RollGrade(
                table,
                "chapter_omega",
                RarityBracketValue.Elite,
                seed))
            .ToArray();
        Assert.That(grades.Distinct().Count(), Is.GreaterThan(1));
    }

    [Test]
    public void MeanGuard_FallsBackWhenAuthoredCalibrationDrifts()
    {
        var table = BuildTable(ItemRarityTierValue.Rare, mean: -4d);

        Assert.That(
            DropGradeEconomy.RollGrade(
                table,
                "chapter_alpha",
                RarityBracketValue.Elite,
                seed: 17),
            Is.EqualTo(ItemRarityTierValue.Rare));
    }

    [Test]
    public void GeneratedGrade_SelectsOneBudgetedAffixPerGradeStep()
    {
        var baseSnapshot = EditorFreeCombatContentFixture.CreateRunLoopLookup().Snapshot;
        var item = new ItemTemplate(
            "item_test",
            Array.Empty<string>(),
            string.Empty,
            "Weapon",
            RarityTier: ItemRarityTierValue.Common);
        var affixes = new[]
        {
            BuildAffix("implicit_a", "Implicit"),
            BuildAffix("prefix_a", "Prefix"),
            BuildAffix("prefix_b", "Prefix"),
            BuildAffix("suffix_a", "Suffix"),
            BuildAffix("suffix_b", "Suffix"),
        };
        var snapshot = baseSnapshot with
        {
            ItemCatalog = new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
            {
                [item.Id] = item,
            },
            AffixCatalog = affixes.ToDictionary(value => value.Id, StringComparer.Ordinal),
        };
        var lookup = new SessionLookupStub(snapshot, affixes.Select(value => value.Id).ToArray());

        for (var grade = 0; grade <= 4; grade++)
        {
            var selected = GeneratedItemAffixSelector.Select(
                lookup,
                item.Id,
                seed: 100 + grade,
                rolledGrade: (ItemRarityTierValue)grade,
                gradeStepBudgetScore: 8f);

            Assert.That(selected.Count, Is.EqualTo(grade + 1));
            Assert.That(selected.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(selected.Count));
        }
    }

    private static DropTableTemplate BuildTable(
        ItemRarityTierValue baseline,
        double mean)
        => BuildTable(
            baseline,
            new[] { mean },
            jackpotWeight: 0d,
            standardDeviation: 0.78d);

    private static DropTableTemplate BuildTable(
        ItemRarityTierValue baseline,
        IReadOnlyList<double> means,
        double jackpotWeight,
        double standardDeviation)
    {
        return new DropTableTemplate(
            "drop_table_test",
            "reward_source_test",
            Array.Empty<LootBundleEntryTemplate>(),
            (float)MeasuredKappa,
            8f,
            means.Select((mean, index) =>
                new DropGradeProfileTemplate(
                    index switch
                    {
                        0 => "chapter_alpha",
                        1 => "chapter_middle",
                        _ => "chapter_omega",
                    },
                    InitialLatentMean: 0.3d,
                    InitialStandardDeviation: 0.78d,
                    MeanPreservingLatentMean: mean,
                    StandardDeviation: standardDeviation))
                .ToArray(),
            jackpotWeight,
            GradeJackpotLatentMean: 4.25d,
            GradeJackpotStandardDeviation: 0.25d);
    }

    private static double CalibrateCampaignCenter(
        ItemRarityTierValue baseline,
        double chapterStep,
        double jackpotWeight)
    {
        var target = Math.Exp(
            DropGradeEconomy.FirstClearReferenceKappa * (int)baseline);
        var low = -12d;
        var high = 12d;
        for (var iteration = 0; iteration < 96; iteration++)
        {
            var midpoint = low + ((high - low) / 2d);
            var expected = new[] { midpoint - chapterStep, midpoint, midpoint + chapterStep }
                .Average(mean => DropGradeEconomy.ExpectedItemPower(
                    mean,
                    0.5d,
                    MeasuredKappa,
                    jackpotWeight,
                    4.25d,
                    0.25d));
            if (expected < target)
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

    private static double ExpectedGrade(IReadOnlyList<double> probabilities)
        => probabilities.Select((probability, grade) => probability * grade).Sum();

    private static double ResolveBundleGrade(
        LootResolutionService service,
        int seed,
        int heat)
    {
        Assert.That(
            service.TryResolveBundle(
                "reward.heat",
                seed,
                new[] { "chapter_alpha" },
                out var bundle,
                out var error,
                heat),
            Is.True,
            error);
        return (int)bundle.Entries.Single().ItemGrade!.Value;
    }

    private static ItemRarityTierValue LegacyRollGrade(
        DropTableTemplate table,
        string chapterId,
        RarityBracketValue fallbackBracket,
        int seed)
    {
        var profile = table.GradeProfiles.Single(candidate =>
            string.Equals(candidate.ChapterId, chapterId, StringComparison.Ordinal));
        var probabilities = DropGradeEconomy.GradeProbabilities(
            profile.MeanPreservingLatentMean,
            profile.StandardDeviation,
            table.GradeJackpotWeight,
            table.GradeJackpotLatentMean,
            table.GradeJackpotStandardDeviation);
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

    private static AffixTemplate BuildAffix(string id, string tier)
    {
        return new AffixTemplate(
            id,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            new[] { "Weapon" },
            BudgetScore: 8f,
            SpawnWeight: 1f,
            Tier: tier,
            ItemLevelMin: 0);
    }

    private sealed class SessionLookupStub : ISessionContentLookup
    {
        private readonly IReadOnlyList<string> _affixIds;

        internal SessionLookupStub(CombatContentSnapshot snapshot, IReadOnlyList<string> affixIds)
        {
            Snapshot = snapshot;
            _affixIds = affixIds;
        }

        public CombatContentSnapshot Snapshot { get; }

        public bool TryGetCombatSnapshot(out CombatContentSnapshot snapshot, out string error)
        {
            snapshot = Snapshot;
            error = string.Empty;
            return true;
        }

        public IReadOnlyList<string> GetCanonicalArchetypeIds() => Array.Empty<string>();
        public IReadOnlyList<string> GetCanonicalItemIds() => Snapshot.ItemCatalog?.Keys.ToArray() ?? Array.Empty<string>();
        public IReadOnlyList<string> GetCanonicalAffixIds() => _affixIds;
        public IReadOnlyList<string> GetCanonicalTemporaryAugmentIds() => Array.Empty<string>();
        public IReadOnlyList<string> GetCanonicalPermanentAugmentIds() => Array.Empty<string>();
        public IReadOnlyList<string> GetCanonicalPassiveBoardIds() => Array.Empty<string>();
        public IReadOnlyList<string> GetCanonicalSynergyFamilyIds() => Array.Empty<string>();
        public FirstPlayableSliceDefinition? GetFirstPlayableSlice() => null;

        public bool TryGetTraitIds(
            string archetypeId,
            out IReadOnlyList<string> positiveTraitIds,
            out IReadOnlyList<string> negativeTraitIds)
        {
            positiveTraitIds = Array.Empty<string>();
            negativeTraitIds = Array.Empty<string>();
            return false;
        }

        public string NormalizeArchetypeId(string archetypeId, string raceId, string classId, int fallbackIndex)
            => archetypeId;

        public string NormalizePositiveTraitId(string archetypeId, string traitId, int fallbackIndex)
            => traitId;

        public string NormalizeNegativeTraitId(string archetypeId, string traitId, int fallbackIndex)
            => traitId;

        public string NormalizeItemBaseId(string itemBaseId, int fallbackIndex)
            => itemBaseId;

        public string NormalizeAffixId(string affixId, int fallbackIndex)
            => affixId;

        public string NormalizeTemporaryAugmentId(string augmentId, int fallbackIndex)
            => augmentId;
    }
}
