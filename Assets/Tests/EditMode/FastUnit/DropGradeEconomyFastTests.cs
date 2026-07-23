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
    private const double MeasuredKappa = 0.081439763626391534d;

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

    [TestCase(ItemRarityTierValue.Magic, 0.9425875d)]
    [TestCase(ItemRarityTierValue.Rare, 1.9719403d)]
    [TestCase(ItemRarityTierValue.Epic, 3.0037623d)]
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

    [Test]
    public void RollGrade_IsDeterministicAndUsesAuthoredVariance()
    {
        var table = BuildTable(
            ItemRarityTierValue.Rare,
            DropGradeEconomy.CalibrateMean(
                ItemRarityTierValue.Rare,
                0.78d,
                MeasuredKappa));
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
    {
        return new DropTableTemplate(
            "drop_table_test",
            "reward_source_test",
            Array.Empty<LootBundleEntryTemplate>(),
            (float)MeasuredKappa,
            8f,
            new[]
            {
                new DropGradeProfileTemplate(
                    "chapter_alpha",
                    InitialLatentMean: 0.3d,
                    InitialStandardDeviation: 0.78d,
                    MeanPreservingLatentMean: mean,
                    StandardDeviation: 0.78d),
            });
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
