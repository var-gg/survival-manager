using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Tests.EditMode.Fakes;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class AffixQualityProfileFastTests
{
    [Test]
    public void Compiler_ProducesExactQ64MassAndDiscretePercentiles()
    {
        var fixture = BuildFixture();
        var profile = new AffixQualityProfileCompiler().Compile(
            fixture.Lookup,
            fixture.ItemId,
            ItemRarityTierValue.Rare,
            2.5f,
            "test-catalog-v1",
            out _);

        Assert.That(profile.SupportScoreQ, Is.Ordered.Ascending);
        Assert.That(profile.SupportScoreQ.Distinct().Count(), Is.EqualTo(profile.SupportScoreQ.Count));
        Assert.That(profile.CdfQ64, Is.Ordered.Ascending);
        Assert.That(profile.CdfQ64[^1], Is.EqualTo(ulong.MaxValue));
        Assert.That(
            profile.MassQ64.Aggregate(
                BigInteger.Zero,
                (sum, mass) => sum + mass),
            Is.EqualTo(new BigInteger(ulong.MaxValue)));

        for (var index = 0; index < profile.SupportScoreQ.Count; index++)
        {
            var score = profile.SupportScoreQ[index];
            Assert.That(
                profile.GetInclusivePercentileQ64(score),
                Is.EqualTo(profile.CdfQ64[index]));
            Assert.That(
                profile.GetExclusivePercentileQ64(score),
                Is.EqualTo(index == 0 ? 0UL : profile.CdfQ64[index - 1]));
            Assert.That(
                profile.GetMassQ64(score),
                Is.EqualTo(profile.MassQ64[index]));
        }

        foreach (var percentile in new[]
                 {
                     AffixQualityProfile.ProbabilityFromFraction(1, 10),
                     AffixQualityProfile.ProbabilityFromFraction(1, 2),
                     AffixQualityProfile.ProbabilityFromFraction(7, 10),
                     AffixQualityProfile.ProbabilityFromFraction(4, 5),
                     AffixQualityProfile.ProbabilityFromFraction(9, 10),
                 })
        {
            var score = profile.GetQuantileScoreQ(percentile);
            Assert.That(profile.SupportScoreQ, Does.Contain(score));
            Assert.That(profile.GetInclusivePercentileQ64(score), Is.GreaterThanOrEqualTo(percentile));
            Assert.That(profile.GetExclusivePercentileQ64(score), Is.LessThan(percentile));
        }
    }

    [Test]
    public void Compiler_RejectsBudgetScoreOutsideScale1000()
    {
        var fixture = BuildFixture(
            new AffixTemplate(
                "implicit_unrepresentable",
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                new[] { "Accessory" },
                BudgetScore: 1.2345f,
                SpawnWeight: 1f,
                Tier: "Implicit"));

        Assert.That(
            () => new AffixQualityProfileCompiler().Compile(
                fixture.Lookup,
                fixture.ItemId,
                ItemRarityTierValue.Common,
                2.5f,
                "test-catalog-v1",
                out _),
            Throws.ArgumentException.With.Message.Contains("scale 1000"));
    }

    [Test]
    public void ConditionedSelector_EndsAtEveryExactSupportScoreAndKeepsLegality()
    {
        var fixture = BuildFixture();
        var profile = new AffixQualityProfileCompiler().Compile(
            fixture.Lookup,
            fixture.ItemId,
            ItemRarityTierValue.Rare,
            2.5f,
            "test-catalog-v1",
            out _);
        var selector = new AffixQualityConditionedSelector();

        foreach (var exactScoreQ in profile.SupportScoreQ)
        {
            for (var seed = 0; seed < 24; seed++)
            {
                var selected = selector.SelectBudgetWeightedConditioned(
                    profile,
                    exactScoreQ,
                    seed);
                var selectedTemplates = selected
                    .Select(id => fixture.Lookup.Snapshot.AffixCatalog![id])
                    .ToArray();

                Assert.That(
                    selectedTemplates.Sum(template =>
                        AffixQualityProfileCompiler.ToBudgetScoreQ(template.BudgetScore)),
                    Is.EqualTo(exactScoreQ));
                Assert.That(
                    selected.Distinct(StringComparer.Ordinal).Count(),
                    Is.EqualTo(selected.Count));
                Assert.That(
                    selectedTemplates
                        .Where(template => !string.IsNullOrWhiteSpace(template.ExclusiveGroupId))
                        .Select(template => template.ExclusiveGroupId)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    Is.EqualTo(selectedTemplates.Count(template =>
                        !string.IsNullOrWhiteSpace(template.ExclusiveGroupId))));
                Assert.That(
                    selectedTemplates.All(template =>
                        template.AllowedSlotTypes!.Contains("Accessory", StringComparer.Ordinal)),
                    Is.True);
            }
        }
    }

    [Test]
    public void ConditionedSelector_RejectsScoreOutsideCompiledSupport()
    {
        var fixture = BuildFixture();
        var profile = new AffixQualityProfileCompiler().Compile(
            fixture.Lookup,
            fixture.ItemId,
            ItemRarityTierValue.Magic,
            2.5f,
            "test-catalog-v1",
            out _);

        Assert.That(
            () => new AffixQualityConditionedSelector().SelectBudgetWeightedConditioned(
                profile,
                exactFinalScoreQ: int.MaxValue,
                seed: 1),
            Throws.TypeOf<ArgumentOutOfRangeException>());
    }

    private static Fixture BuildFixture(params AffixTemplate[] replacements)
    {
        const string itemId = "item_quality_test";
        var baseSnapshot = EditorFreeCombatContentFixture.CreateRunLoopLookup().Snapshot;
        var affixes = replacements.Length > 0
            ? replacements
            : new[]
            {
                BuildAffix("implicit_low", "Implicit", 1f, "implicit.low"),
                BuildAffix("implicit_high", "Implicit", 2f, "implicit.high"),
                BuildAffix("prefix_low", "Prefix", 1f, "prefix.low"),
                BuildAffix("prefix_mid", "Prefix", 2f, "prefix.shared"),
                BuildAffix("prefix_high", "Prefix", 3f, "prefix.shared"),
                BuildAffix("suffix_low", "Suffix", 1f, "suffix.low"),
                BuildAffix("suffix_mid", "Suffix", 2f, "suffix.mid"),
                BuildAffix("suffix_high", "Suffix", 3f, "suffix.high"),
            };
        var item = new ItemTemplate(
            itemId,
            Array.Empty<string>(),
            string.Empty,
            "Accessory");
        var snapshot = baseSnapshot with
        {
            ItemCatalog = new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
            {
                [itemId] = item,
            },
            AffixCatalog = affixes.ToDictionary(affix => affix.Id, StringComparer.Ordinal),
            SessionContentOrder = new SessionContentOrder(
                Array.Empty<string>(),
                new[] { itemId },
                affixes.Select(affix => affix.Id).ToArray(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
        };
        return new Fixture(itemId, new SnapshotSessionContentLookup(snapshot));
    }

    private static AffixTemplate BuildAffix(
        string id,
        string tier,
        float budgetScore,
        string exclusiveGroupId)
    {
        return new AffixTemplate(
            id,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            new[] { "Accessory" },
            budgetScore,
            SpawnWeight: 1f,
            Tier: tier,
            ItemLevelMin: 0,
            ExclusiveGroupId: exclusiveGroupId);
    }

    private sealed record Fixture(string ItemId, SnapshotSessionContentLookup Lookup);
}
