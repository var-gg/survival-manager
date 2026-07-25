using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RefitServiceTests
{
    [Test]
    public void FloorSchedule_UsesAuthoritativeClosedFormValues()
    {
        var schedule = RefitFloorSchedule.Generate(70, 100, 55, 100);

        Assert.That(RefitFloorSchedule.ToDouble(schedule[0]), Is.EqualTo(0.315d).Within(1e-12));
        Assert.That(RefitFloorSchedule.ToDouble(schedule[1]), Is.EqualTo(0.48825d).Within(1e-12));
        Assert.That(RefitFloorSchedule.ToDouble(schedule[2]), Is.EqualTo(0.5835375d).Within(1e-12));
        Assert.That(schedule, Is.Ordered);
        Assert.That(schedule[^1], Is.EqualTo(
            AffixQualityProfile.ProbabilityFromFraction(70, 100)));
    }

    [Test]
    public void CostCurve_CeilsEachLevelBeforeSummingBundle()
    {
        var balance = RefitTestFixture.CreateBalance();
        const int firstFarmEcho = 40;
        const double meanGrade = 2.1d;
        var expected = Enumerable.Range(1, 3)
            .Sum(level => checked((int)Math.Ceiling(
                0.60d
                * firstFarmEcho
                * Math.Pow(1.70d, level - 1)
                * Math.Pow(1.25d, (int)ItemRarityTierValue.Epic - meanGrade))));

        var actual = RefitCostCurve.GetBundleCost(
            balance,
            firstFarmEcho,
            currentRefitLevel: 0,
            targetRefitLevel: 3,
            ItemRarityTierValue.Epic,
            meanGrade);

        Assert.That(actual, Is.EqualTo(expected));
        Assert.That(actual, Is.Not.EqualTo(15), "retired flat-15 pricing must not survive");
    }

    [Test]
    public void RollQuality_IsBudgetScoreWeightedMeanOfPerAffixPositions()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixes = lookup.Snapshot.AffixCatalog!.Values
            .Where(value => value.AllowedSlotTypes!.Contains("Weapon"))
            .OrderBy(value => value.BudgetScore)
            .Take(2)
            .ToArray();
        var magnitudes = new Dictionary<string, float>(StringComparer.Ordinal)
        {
            [affixes[0].Id] = affixes[0].ValueMin,
            [affixes[1].Id] = affixes[1].ValueMax,
        };

        var measured = RefitRollQuality.Measure(
            lookup.Snapshot,
            affixes.Select(value => value.Id).ToArray(),
            magnitudes);

        var expected = affixes[1].BudgetScore
                       / (affixes[0].BudgetScore + affixes[1].BudgetScore);
        Assert.That(measured, Is.EqualTo(expected).Within(1e-12d));
    }

    [Test]
    public void RollQuality_DegenerateRangeCountsAsFullySatisfied()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var source = lookup.Snapshot.AffixCatalog!.Values.First();
        var fixedAffix = source with { ValueMin = 2f, ValueMax = 2f };
        var catalog = new Dictionary<string, AffixTemplate>(StringComparer.Ordinal)
        {
            [fixedAffix.Id] = fixedAffix,
        };

        var measured = RefitRollQuality.Measure(
            lookup.Snapshot with { AffixCatalog = catalog },
            new[] { fixedAffix.Id },
            new Dictionary<string, float>(StringComparer.Ordinal)
            {
                [fixedAffix.Id] = 2f,
            });

        Assert.That(measured, Is.EqualTo(1d));
    }

    [Test]
    public void Reroll_FloatMaterializationNeverFallsBelowPurchasedFloor()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var source = lookup.Snapshot.AffixCatalog!.Values.First();
        var wideOffsetAffix = source with
        {
            ValueMin = 1_000_000f,
            ValueMax = 1_000_001f,
        };
        var snapshot = lookup.Snapshot with
        {
            AffixCatalog = new Dictionary<string, AffixTemplate>(StringComparer.Ordinal)
            {
                [wideOffsetAffix.Id] = wideOffsetAffix,
            },
        };
        var floorQ64 = RefitTestFixture.CreateBalance().FloorScheduleQ64[0];

        for (var seed = 0; seed < 256; seed++)
        {
            var magnitudes = RefitRollQuality.RerollToFloor(
                snapshot,
                new[] { wideOffsetAffix.Id },
                seed,
                floorQ64);
            var measuredQ64 = RefitRollQuality.ToQ64(
                RefitRollQuality.Measure(
                    snapshot,
                    new[] { wideOffsetAffix.Id },
                    magnitudes));

            Assert.That(measuredQ64, Is.GreaterThanOrEqualTo(floorQ64));
        }
    }

    [TestCase(ItemRarityTierValue.Common)]
    [TestCase(ItemRarityTierValue.Magic)]
    [TestCase(ItemRarityTierValue.Rare)]
    [TestCase(ItemRarityTierValue.Epic)]
    [TestCase(ItemRarityTierValue.Legendary)]
    public void Refit_AllGradesPreserveIdentityAndLandAtMonotoneFloor(
        ItemRarityTierValue grade)
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixIds = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            grade,
            0);
        var oldMagnitudes = RefitTestFixture.CreateMagnitudes(lookup, affixIds, 0.05d);
        var item = new RefitItemState(
            RefitTestFixture.WeaponItemId,
            $"all-grades:{grade}",
            grade,
            affixIds,
            oldMagnitudes,
            0);
        var service = RefitTestFixture.CreateService(lookup);

        var result = service.RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            stableCommandSeed: 0x1234UL);

        Assert.That(result.Applied, Is.True, result.Error);
        Assert.That(result.InvariantFailure, Is.False);
        Assert.That(result.AffixIds, Is.EqualTo(affixIds));
        Assert.That(result.AffixMagnitudes.Keys, Is.EquivalentTo(affixIds));
        Assert.That(
            result.ResultPercentileQ64,
            Is.GreaterThanOrEqualTo(result.Quote.CurrentPercentileQ64));
        Assert.That(
            result.ResultPercentileQ64,
            Is.GreaterThanOrEqualTo(result.Quote.TargetFloorQ64));
        foreach (var affixId in affixIds)
        {
            var affix = lookup.Snapshot.AffixCatalog![affixId];
            Assert.That(
                result.AffixMagnitudes[affixId],
                Is.InRange(affix.ValueMin, affix.ValueMax));
        }
    }

    [Test]
    public void Quote_SkipsNominalNoOps_AndSumsIndividuallyRoundedLevels()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixIds = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
        var item = new RefitItemState(
            RefitTestFixture.WeaponItemId,
            "skip-no-op",
            ItemRarityTierValue.Epic,
            affixIds,
            RefitTestFixture.CreateMagnitudes(lookup, affixIds, 0.40d),
            0);
        var service = RefitTestFixture.CreateService(lookup);
        var economy = RefitTestFixture.CreateEconomy(lookup);

        var quote = service.QuoteNextEffective(item, economy);

        Assert.That(quote.CanPurchase, Is.True, quote.Reason);
        Assert.That(quote.TargetRefitLevel, Is.EqualTo(2));
        Assert.That(quote.TargetFloorQ64, Is.GreaterThan(quote.CurrentPercentileQ64));
        Assert.That(quote.EchoCost, Is.EqualTo(RefitCostCurve.GetBundleCost(
            lookup.Snapshot.RefitBalance!,
            economy.FirstFarmRunEcho,
            0,
            quote.TargetRefitLevel,
            ItemRarityTierValue.Epic,
            economy.MeanGrade)));
    }

    [Test]
    public void AboveMaximumFloor_IsRefitMaxedWithNoPaidNoOp()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixIds = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
        var service = RefitTestFixture.CreateService(lookup);

        var quote = service.QuoteNextEffective(
            new RefitItemState(
                RefitTestFixture.WeaponItemId,
                "maximum-roll-quality",
                ItemRarityTierValue.Epic,
                affixIds,
                RefitTestFixture.CreateMagnitudes(lookup, affixIds, 0.80d),
                0),
            RefitTestFixture.CreateEconomy(lookup));

        Assert.That(quote.CanPurchase, Is.False);
        Assert.That(quote.RefitMaxed, Is.True);
        Assert.That(quote.EchoCost, Is.Zero);
        Assert.That(quote.TargetFloorQ64, Is.EqualTo(quote.CurrentPercentileQ64));
    }

    [Test]
    public void LegacyMissingMagnitude_UsesSharedPackageBaseline()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixIds = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.AccessoryItemId,
            ItemRarityTierValue.Legendary,
            0);
        var measured = RefitRollQuality.Measure(
            lookup.Snapshot,
            affixIds,
            new Dictionary<string, float>(StringComparer.Ordinal));

        Assert.That(measured, Is.EqualTo(0.5d).Within(1e-12d));
        var result = RefitTestFixture.CreateService(lookup).RefitNextEffective(
            new RefitItemState(
                RefitTestFixture.AccessoryItemId,
                "legacy-magnitude",
                ItemRarityTierValue.Legendary,
                affixIds,
                new Dictionary<string, float>(StringComparer.Ordinal),
                0),
            RefitTestFixture.CreateEconomy(lookup),
            0xBEEFUL);
        Assert.That(result.Applied, Is.True, result.Error);
    }

    [Test]
    public void Refit_IsStableForSameSaveAndCommandSeed()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixIds = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.ArmorItemId,
            ItemRarityTierValue.Legendary,
            0);
        var item = new RefitItemState(
            RefitTestFixture.ArmorItemId,
            "stable-save-item",
            ItemRarityTierValue.Legendary,
            affixIds,
            RefitTestFixture.CreateMagnitudes(lookup, affixIds, 0.10d),
            0);

        var first = RefitTestFixture.CreateService(lookup).RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            0xC0FFEEUL);
        var second = RefitTestFixture.CreateService(lookup).RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            0xC0FFEEUL);

        Assert.That(first.Applied, Is.True, first.Error);
        Assert.That(second.Applied, Is.True, second.Error);
        Assert.That(second.AffixIds, Is.EqualTo(first.AffixIds));
        Assert.That(
            second.AffixMagnitudes.OrderBy(value => value.Key),
            Is.EqualTo(first.AffixMagnitudes.OrderBy(value => value.Key)));
        Assert.That(second.Quote, Is.EqualTo(first.Quote));
    }

    [Test]
    public void SeedDerivation_UsesItemLevelRulesVersionAndRefitDomain()
    {
        var same = RefitSeedDerivation.Derive(17UL, "item-a", 2, 1);
        Assert.That(RefitSeedDerivation.Derive(17UL, "item-a", 2, 1), Is.EqualTo(same));
        Assert.That(RefitSeedDerivation.Derive(17UL, "item-b", 2, 1), Is.Not.EqualTo(same));
        Assert.That(RefitSeedDerivation.Derive(17UL, "item-a", 3, 1), Is.Not.EqualTo(same));
        Assert.That(RefitSeedDerivation.Derive(17UL, "item-a", 2, 2), Is.Not.EqualTo(same));
    }

    [Test]
    public void MonotonicInvariant_RejectsRegressedMagnitudeQuality()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var affixIds = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
        var item = new RefitItemState(
            RefitTestFixture.WeaponItemId,
            "mutation-guard",
            ItemRarityTierValue.Epic,
            affixIds,
            RefitTestFixture.CreateMagnitudes(lookup, affixIds, 0.90d),
            0);
        var quote = new RefitQuote(
            true,
            false,
            string.Empty,
            RefitRollQuality.ToQ64(0.90d),
            0,
            1,
            0UL,
            1);
        var method = typeof(RefitService).GetMethod(
            "ValidatePostconditions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var arguments = new object[]
        {
            item,
            quote,
            affixIds,
            RefitTestFixture.CreateMagnitudes(lookup, affixIds, 0.10d),
            0UL,
            string.Empty,
        };

        var valid = (bool)method!.Invoke(RefitTestFixture.CreateService(lookup), arguments)!;

        Assert.That(valid, Is.False);
        Assert.That((string)arguments[5], Does.Contain("regressed"));
    }

    [Test]
    public void SessionTransaction_AtomicallyPreservesIdsUpdatesRollsAndDeductsCost()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup, echo: 10_000);
        var item = session.Profile.Inventory.Single();
        var oldAffixes = item.AffixIds.ToArray();
        var oldRollBits = item.AffixMagnitudeRolls
            .Select(value => BitConverter.SingleToInt32Bits(value.Magnitude))
            .ToArray();
        var quote = session.GetRefitQuote(item.ItemInstanceId);
        var echoBefore = session.Profile.Currencies.Echo;

        var result = session.RefitItem(item.ItemInstanceId, 0x1234UL);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(item.RefitLevel, Is.EqualTo(quote.TargetRefitLevel));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBefore - quote.EchoCost));
        Assert.That(item.AffixIds, Is.EqualTo(oldAffixes));
        Assert.That(
            item.AffixMagnitudeRolls.Select(value => value.AffixId),
            Is.EqualTo(oldAffixes));
        Assert.That(
            item.AffixMagnitudeRolls.Select(value => BitConverter.SingleToInt32Bits(value.Magnitude)),
            Is.Not.EqualTo(oldRollBits));
        var quality = RefitRollQuality.ToQ64(RefitRollQuality.Measure(
            lookup.Snapshot,
            item.AffixIds,
            item.AffixMagnitudeRolls.ToDictionary(
                value => value.AffixId,
                value => value.Magnitude,
                StringComparer.Ordinal)));
        Assert.That(quality, Is.GreaterThanOrEqualTo(quote.TargetFloorQ64));
    }

    [Test]
    public void SessionTransaction_InsufficientEchoLeavesBothAffixListsLevelAndCurrencyUntouched()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup, echo: 0);
        var item = session.Profile.Inventory.Single();
        var oldAffixes = item.AffixIds.ToArray();
        var oldRolls = item.AffixMagnitudeRolls
            .Select(value => (value.AffixId, BitConverter.SingleToInt32Bits(value.Magnitude)))
            .ToArray();
        var oldLevel = item.RefitLevel;

        var result = session.RefitItem(item.ItemInstanceId, 0x1234UL);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("잔향"));
        Assert.That(session.Profile.Currencies.Echo, Is.Zero);
        Assert.That(item.RefitLevel, Is.EqualTo(oldLevel));
        Assert.That(item.AffixIds, Is.EqualTo(oldAffixes));
        Assert.That(
            item.AffixMagnitudeRolls
                .Select(value => (value.AffixId, BitConverter.SingleToInt32Bits(value.Magnitude))),
            Is.EqualTo(oldRolls));
    }

    private static GameSessionState CreateSession(FakeCombatContentLookup lookup, int echo)
    {
        var affixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
        var magnitudes = RefitTestFixture.CreateMagnitudes(lookup, affixes, 0.05d);
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            ProfileId = "refit-session-test",
            Currencies = new CurrencyRecord { Echo = echo },
            Heroes = new List<HeroInstanceRecord>
            {
                new()
                {
                    HeroId = "refit-hero",
                    Name = "Refit Hero",
                    ArchetypeId = "refit_archetype",
                    RaceId = "human",
                    ClassId = "vanguard",
                    EquippedItemIds = new List<string>(),
                },
            },
            CampaignProgress = new CampaignProgressRecord
            {
                SelectedChapterId = RefitTestFixture.ChapterId,
                SelectedSiteId = "site_alpha_gate",
            },
            Inventory = new List<InventoryItemRecord>
            {
                new()
                {
                    ItemInstanceId = "refit-item-1",
                    ItemBaseId = RefitTestFixture.WeaponItemId,
                    RolledRarityTier = (int)ItemRarityTierValue.Epic,
                    AffixIds = affixes.ToList(),
                    AffixMagnitudeRolls = affixes.Select(affixId =>
                        new InventoryAffixMagnitudeRecord
                        {
                            AffixId = affixId,
                            Magnitude = magnitudes[affixId],
                        }).ToList(),
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }
}
