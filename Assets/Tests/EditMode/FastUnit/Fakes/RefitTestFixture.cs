using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode.Fakes;

public static class RefitTestFixture
{
    public const string WeaponItemId = "refit_test_weapon";
    public const string ArmorItemId = "refit_test_armor";
    public const string AccessoryItemId = "refit_test_accessory";
    public const string ChapterId = "chapter_alpha";
    public const float GradeStepBudgetScore = 2f;

    public static RefitBalanceTemplate CreateBalance()
        => new(
            RulesVersion: 1,
            AffixCatalogVersion: "refit-test-catalog-v1",
            MaximumFloorNumerator: 70,
            MaximumFloorDenominator: 100,
            FloorDecayNumerator: 55,
            FloorDecayDenominator: 100,
            FloorScheduleQ64: RefitFloorSchedule.Generate(70, 100, 55, 100),
            CostBaseFirstFarmEchoMultiplier: 0.60d,
            CostGrowthPerLevel: 1.70d,
            GradeCostRatio: 1.25d,
            SealCostMultiplierPerLockedAffix: 0.50d);

    public static FakeCombatContentLookup CreateLookup()
    {
        var baseLookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var affixes = CreateAffixes();
        var affixPackages = baseLookup.Snapshot.AffixPackages.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        foreach (var affixId in affixes.Keys)
        {
            affixPackages[affixId] = CreateAffixPackage(affixId);
        }

        var itemCatalog = new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
        {
            [WeaponItemId] = CreateItem(WeaponItemId, "Weapon"),
            [ArmorItemId] = CreateItem(ArmorItemId, "Armor"),
            [AccessoryItemId] = CreateItem(AccessoryItemId, "Accessory"),
        };
        var firstPlayableSlice = new FirstPlayableSliceDefinition
        {
            AffixIds = affixes.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
        };
        var snapshot = baseLookup.Snapshot with
        {
            AffixCatalog = affixes,
            AffixPackages = affixPackages,
            ItemCatalog = itemCatalog,
            SessionContentOrder = new SessionContentOrder(
                Array.Empty<string>(),
                itemCatalog.Keys.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                firstPlayableSlice.AffixIds,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            DropTables = CreateDropTables(),
            RefitBalance = CreateBalance(),
        };
        return new FakeCombatContentLookup(
            snapshot: snapshot,
            firstPlayableSlice: firstPlayableSlice);
    }

    public static RefitService CreateService(FakeCombatContentLookup lookup)
        => new(lookup, lookup.Snapshot.RefitBalance!);

    public static IReadOnlyDictionary<string, float> CreateMagnitudes(
        FakeCombatContentLookup lookup,
        IReadOnlyList<string> affixIds,
        double position)
    {
        if (!double.IsFinite(position) || position < 0d || position > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return affixIds.ToDictionary(
            affixId => affixId,
            affixId =>
            {
                var affix = lookup.Snapshot.AffixCatalog![affixId];
                return (float)(affix.ValueMin
                               + ((affix.ValueMax - affix.ValueMin) * position));
            },
            StringComparer.Ordinal);
    }

    public static AffixQualityProfile CompileProfile(
        FakeCombatContentLookup lookup,
        string itemBaseId,
        ItemRarityTierValue grade)
        => new AffixQualityProfileCompiler().Compile(
            lookup,
            itemBaseId,
            grade,
            GradeStepBudgetScore,
            lookup.Snapshot.RefitBalance!.AffixCatalogVersion,
            out _);

    public static IReadOnlyList<string> SelectAtSupportIndex(
        FakeCombatContentLookup lookup,
        string itemBaseId,
        ItemRarityTierValue grade,
        int supportIndex,
        int seed = 17)
    {
        var profile = CompileProfile(lookup, itemBaseId, grade);
        var boundedIndex = Math.Clamp(supportIndex, 0, profile.SupportScoreQ.Count - 1);
        return new AffixQualityConditionedSelector().SelectBudgetWeightedConditioned(
            profile,
            profile.SupportScoreQ[boundedIndex],
            seed);
    }

    public static RefitChapterEconomy CreateEconomy(FakeCombatContentLookup lookup)
        => new(
            ChapterId,
            CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(lookup.Snapshot, ChapterId),
            CampaignRecoveryRewardPolicy.ResolveFirstFarmRunMeanGrade(lookup.Snapshot, ChapterId));

    public static int Score(FakeCombatContentLookup lookup, IReadOnlyList<string> affixIds)
        => affixIds.Sum(id =>
            AffixQualityProfileCompiler.ToBudgetScoreQ(lookup.Snapshot.AffixCatalog![id].BudgetScore));

    private static ItemTemplate CreateItem(string id, string slotType)
        => new(
            id,
            Array.Empty<string>(),
            string.Empty,
            slotType,
            Array.Empty<string>(),
            ItemRarityTierValue.Epic,
            ItemIdentityValue.Baseline,
            AllowedCraftOperations: new[]
            {
                CraftOperationKindValue.Reforge,
                CraftOperationKindValue.Seal,
            });

    private static IReadOnlyDictionary<string, AffixTemplate> CreateAffixes()
    {
        var result = new Dictionary<string, AffixTemplate>(StringComparer.Ordinal);
        foreach (var slotType in new[] { "Weapon", "Armor", "Accessory" })
        {
            var slotKey = slotType.ToLowerInvariant();
            AddTier(result, slotKey, slotType, "Implicit", new[] { 0.75f, 1.25f, 2f });
            AddTier(result, slotKey, slotType, "Prefix", new[] { 0.5f, 0.75f, 1f, 1.25f, 1.75f, 2.25f });
            AddTier(result, slotKey, slotType, "Suffix", new[] { 0.5f, 0.8f, 1.1f, 1.4f, 1.8f, 2.3f });
        }

        return result;
    }

    private static void AddTier(
        IDictionary<string, AffixTemplate> result,
        string slotKey,
        string slotType,
        string tier,
        IReadOnlyList<float> scores)
    {
        for (var index = 0; index < scores.Count; index++)
        {
            var id = $"{slotKey}_{tier.ToLowerInvariant()}_{index + 1}";
            result[id] = new AffixTemplate(
                id,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                null,
                new[] { slotType },
                scores[index],
                SpawnWeight: index + 1,
                Tier: tier,
                ExclusiveGroupId: index == 0 ? $"{slotKey}_exclusive_alpha" : string.Empty,
                ValueMin: 1f,
                ValueMax: 3f);
        }
    }

    private static CombatModifierPackage CreateAffixPackage(string affixId)
        => new(
            affixId,
            ModifierSource.Item,
            new[]
            {
                new StatModifier(
                    StatKey.PhysPower,
                    ModifierOp.Flat,
                    2f,
                    ModifierSource.Item,
                    affixId),
            });

    private static IReadOnlyDictionary<string, DropTableTemplate> CreateDropTables()
    {
        return new Dictionary<string, DropTableTemplate>(StringComparer.Ordinal)
        {
            ["drop.extract"] = new DropTableTemplate(
                "drop.extract",
                "reward_source_extract",
                new[]
                {
                    new LootBundleEntryTemplate(
                        "refit_test_echo",
                        RewardType.Echo,
                        40,
                        RarityBracketValue.Advanced,
                        1,
                        true,
                        Array.Empty<string>()),
                },
                GradeStepBudgetScore: GradeStepBudgetScore,
                GradeProfiles: new[]
                {
                    new DropGradeProfileTemplate(
                        ChapterId,
                        InitialLatentMean: 2.1d,
                        InitialStandardDeviation: 0.65d,
                        MeanPreservingLatentMean: 2.1d,
                        StandardDeviation: 0.65d),
                },
                GradeJackpotWeight: 0.10d,
                GradeJackpotLatentMean: 4.25d,
                GradeJackpotStandardDeviation: 0.25d),
        };
    }
}
