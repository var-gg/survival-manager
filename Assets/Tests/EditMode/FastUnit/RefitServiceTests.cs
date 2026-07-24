using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Core.Content;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class RefitServiceTests
{
    private static readonly object[] ProfileCases =
    {
        new object[] { RefitTestFixture.WeaponItemId, ItemRarityTierValue.Epic },
        new object[] { RefitTestFixture.WeaponItemId, ItemRarityTierValue.Legendary },
        new object[] { RefitTestFixture.ArmorItemId, ItemRarityTierValue.Epic },
        new object[] { RefitTestFixture.ArmorItemId, ItemRarityTierValue.Legendary },
        new object[] { RefitTestFixture.AccessoryItemId, ItemRarityTierValue.Epic },
        new object[] { RefitTestFixture.AccessoryItemId, ItemRarityTierValue.Legendary },
    };

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

    [TestCaseSource(nameof(ProfileCases))]
    public void Refit_UsesConditionedProfile_AndNeverWorsens(
        string itemBaseId,
        ItemRarityTierValue grade)
    {
        var lookup = RefitTestFixture.CreateLookup();
        var profile = RefitTestFixture.CompileProfile(lookup, itemBaseId, grade);
        Assert.That(profile.SupportScoreQ.Count, Is.GreaterThan(3));
        var oldAffixes = RefitTestFixture.SelectAtSupportIndex(lookup, itemBaseId, grade, 0);
        var item = new RefitItemState(itemBaseId, $"stable:{itemBaseId}", grade, oldAffixes, 0);
        var service = RefitTestFixture.CreateService(lookup);

        var result = service.RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            stableCommandSeed: 0xA2UL);

        Assert.That(result.Applied, Is.True, result.Error);
        Assert.That(result.InvariantFailure, Is.False);
        var newScore = RefitTestFixture.Score(lookup, result.AffixIds);
        Assert.That(newScore, Is.EqualTo(result.Quote.TargetScoreQ));
        Assert.That(newScore, Is.GreaterThanOrEqualTo(result.Quote.CurrentScoreQ));
        Assert.That(
            profile.GetInclusivePercentileQ64(newScore),
            Is.GreaterThanOrEqualTo(result.Quote.TargetFloorQ64));
        AssertLegal(lookup, itemBaseId, result.AffixIds);
    }

    [TestCase(ItemRarityTierValue.Common)]
    [TestCase(ItemRarityTierValue.Magic)]
    [TestCase(ItemRarityTierValue.Rare)]
    public void BelowEpic_IsInertAndNeverCharges(ItemRarityTierValue grade)
    {
        var lookup = RefitTestFixture.CreateLookup();
        var oldAffixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            grade,
            0);
        var service = RefitTestFixture.CreateService(lookup);
        var item = new RefitItemState(
            RefitTestFixture.WeaponItemId,
            "below-epic",
            grade,
            oldAffixes,
            0);

        var quote = service.QuoteNextEffective(item, RefitTestFixture.CreateEconomy(lookup));
        var result = service.RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            stableCommandSeed: 1UL);

        Assert.That(quote.CanPurchase, Is.False);
        Assert.That(quote.RefitMaxed, Is.True);
        Assert.That(quote.EchoCost, Is.Zero);
        Assert.That(result.Applied, Is.False);
        Assert.That(result.AffixIds, Is.EqualTo(oldAffixes));
    }

    [Test]
    public void AlreadyAtMaximumSupport_IsRefitMaxedWithNoPaidNoOp()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var profile = RefitTestFixture.CompileProfile(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic);
        var oldAffixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            profile.SupportScoreQ.Count - 1);
        var service = RefitTestFixture.CreateService(lookup);

        var quote = service.QuoteNextEffective(
            new RefitItemState(
                RefitTestFixture.WeaponItemId,
                "maximum-support",
                ItemRarityTierValue.Epic,
                oldAffixes,
                0),
            RefitTestFixture.CreateEconomy(lookup));

        Assert.That(quote.CanPurchase, Is.False);
        Assert.That(quote.RefitMaxed, Is.True);
        Assert.That(quote.EchoCost, Is.Zero);
        Assert.That(quote.TargetScoreQ, Is.EqualTo(quote.CurrentScoreQ));
    }

    [Test]
    public void Quote_SkipsNominalNoOps_AndSumsIndividuallyRoundedLevels()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var profile = RefitTestFixture.CompileProfile(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic);
        var oldScore = profile.GetQuantileScoreQ(
            AffixQualityProfile.ProbabilityFromFraction(40, 100));
        var oldIndex = profile.SupportScoreQ.ToList().IndexOf(oldScore);
        var oldAffixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            oldIndex);
        var service = RefitTestFixture.CreateService(lookup);
        var economy = RefitTestFixture.CreateEconomy(lookup);

        var quote = service.QuoteNextEffective(
            new RefitItemState(
                RefitTestFixture.WeaponItemId,
                "skip-no-op",
                ItemRarityTierValue.Epic,
                oldAffixes,
                0),
            economy);

        Assert.That(quote.CanPurchase, Is.True, quote.Reason);
        Assert.That(quote.TargetRefitLevel, Is.GreaterThan(1));
        Assert.That(quote.TargetScoreQ, Is.GreaterThan(quote.CurrentScoreQ));
        Assert.That(quote.EchoCost, Is.EqualTo(RefitCostCurve.GetBundleCost(
            lookup.Snapshot.RefitBalance!,
            economy.FirstFarmRunEcho,
            0,
            quote.TargetRefitLevel,
            ItemRarityTierValue.Epic,
            economy.MeanGrade)));
    }

    [Test]
    public void LegacyOffSupportScore_UsesFirstAttainableScoreAtOrAboveFloor()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var profile = RefitTestFixture.CompileProfile(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic);
        var oneLegacyAffix = lookup.Snapshot.AffixCatalog!.Values
            .Where(affix => affix.AllowedSlotTypes!.Contains("Weapon"))
            .OrderBy(affix => affix.BudgetScore)
            .Select(affix => affix.Id)
            .Take(1)
            .ToArray();
        var legacyScore = RefitTestFixture.Score(lookup, oneLegacyAffix);
        Assert.That(profile.SupportScoreQ.Contains(legacyScore), Is.False);
        var service = RefitTestFixture.CreateService(lookup);

        var quote = service.QuoteNextEffective(
            new RefitItemState(
                RefitTestFixture.WeaponItemId,
                "legacy-off-support",
                ItemRarityTierValue.Epic,
                oneLegacyAffix,
                0),
            RefitTestFixture.CreateEconomy(lookup));

        Assert.That(quote.CanPurchase, Is.True, quote.Reason);
        Assert.That(profile.SupportScoreQ.Contains(quote.TargetScoreQ), Is.True);
        Assert.That(quote.TargetScoreQ, Is.GreaterThan(legacyScore));
    }

    [Test]
    public void Refit_IsStableForSameSaveAndCommandSeed()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var oldAffixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.ArmorItemId,
            ItemRarityTierValue.Legendary,
            0);
        var item = new RefitItemState(
            RefitTestFixture.ArmorItemId,
            "stable-save-item",
            ItemRarityTierValue.Legendary,
            oldAffixes,
            0);
        var serviceA = RefitTestFixture.CreateService(lookup);
        var serviceB = RefitTestFixture.CreateService(lookup);

        var first = serviceA.RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            0xC0FFEEUL);
        var second = serviceB.RefitNextEffective(
            item,
            RefitTestFixture.CreateEconomy(lookup),
            0xC0FFEEUL);

        Assert.That(first.Applied, Is.True, first.Error);
        Assert.That(second.Applied, Is.True, second.Error);
        Assert.That(second.AffixIds, Is.EqualTo(first.AffixIds));
        Assert.That(second.Quote, Is.EqualTo(first.Quote));
    }

    [Test]
    public void ConditionedSelection_UsesWeightedNaturalSequences_NotIdRanking()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var profile = RefitTestFixture.CompileProfile(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Legendary);
        var selector = new AffixQualityConditionedSelector();
        var foundMultipleSequencesAtOneExactScore = profile.SupportScoreQ.Any(score =>
            Enumerable.Range(0, 64)
                .Select(seed => string.Join("|",
                    selector.SelectBudgetWeightedConditioned(profile, score, seed)))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count() > 1);

        Assert.That(
            foundMultipleSequencesAtOneExactScore,
            Is.True,
            "an exact score must preserve conditioned natural-sequence weights instead of choosing one ID-ranked row");
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
    public void MonotonicInvariant_RejectsARegressedConditionedResult()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var service = RefitTestFixture.CreateService(lookup);
        var profile = RefitTestFixture.CompileProfile(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic);
        var low = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
        var high = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            profile.SupportScoreQ.Count - 1);
        var lowScore = RefitTestFixture.Score(lookup, low);
        var highScore = RefitTestFixture.Score(lookup, high);
        var item = new RefitItemState(
            RefitTestFixture.WeaponItemId,
            "mutation-guard",
            ItemRarityTierValue.Epic,
            high,
            0);
        var quote = new RefitQuote(
            true,
            false,
            string.Empty,
            highScore,
            ulong.MaxValue,
            0,
            1,
            0UL,
            lowScore,
            1);
        var method = typeof(RefitService).GetMethod(
            "ValidatePostconditions",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var arguments = new object[] { item, profile, quote, low, string.Empty };

        var valid = (bool)method!.Invoke(service, arguments)!;

        Assert.That(valid, Is.False);
        Assert.That((string)arguments[4], Does.Contain("regressed"));
    }

    [Test]
    public void SessionTransaction_AppliesAfterValidationAndDeductsQuotedCost()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup, echo: 10_000);
        var item = session.Profile.Inventory.Single();
        var oldAffixes = item.AffixIds.ToArray();
        var quote = session.GetRefitQuote(item.ItemInstanceId);
        var echoBefore = session.Profile.Currencies.Echo;

        var result = session.RefitItem(item.ItemInstanceId, 0x1234UL);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(item.RefitLevel, Is.EqualTo(quote.TargetRefitLevel));
        Assert.That(session.Profile.Currencies.Echo, Is.EqualTo(echoBefore - quote.EchoCost));
        Assert.That(item.AffixIds, Is.Not.EqualTo(oldAffixes));
        Assert.That(
            RefitTestFixture.Score(lookup, item.AffixIds),
            Is.GreaterThanOrEqualTo(quote.CurrentScoreQ));
    }

    [Test]
    public void SessionTransaction_InsufficientEchoLeavesItemLevelAndCurrencyUntouched()
    {
        var lookup = RefitTestFixture.CreateLookup();
        var session = CreateSession(lookup, echo: 0);
        var item = session.Profile.Inventory.Single();
        var oldAffixes = item.AffixIds.ToArray();
        var oldLevel = item.RefitLevel;

        var result = session.RefitItem(item.ItemInstanceId, 0x1234UL);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Error, Does.Contain("잔향"));
        Assert.That(session.Profile.Currencies.Echo, Is.Zero);
        Assert.That(item.RefitLevel, Is.EqualTo(oldLevel));
        Assert.That(item.AffixIds, Is.EqualTo(oldAffixes));
    }

    private static GameSessionState CreateSession(FakeCombatContentLookup lookup, int echo)
    {
        var affixes = RefitTestFixture.SelectAtSupportIndex(
            lookup,
            RefitTestFixture.WeaponItemId,
            ItemRarityTierValue.Epic,
            0);
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
                },
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        return session;
    }

    private static void AssertLegal(
        FakeCombatContentLookup lookup,
        string itemBaseId,
        IReadOnlyList<string> affixIds)
    {
        var item = lookup.Snapshot.ItemCatalog![itemBaseId];
        Assert.That(affixIds, Is.Unique);
        Assert.That(
            affixIds.All(id =>
                lookup.Snapshot.AffixCatalog!.ContainsKey(id)
                && lookup.Snapshot.AffixCatalog[id].AllowedSlotTypes!.Contains(item.SlotType)),
            Is.True);
        var exclusiveGroups = affixIds
            .Select(id => lookup.Snapshot.AffixCatalog![id].ExclusiveGroupId)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .ToArray();
        Assert.That(exclusiveGroups, Is.Unique);
    }
}
