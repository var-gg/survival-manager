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

[Category("FastUnit")]
public sealed class GeneratedItemAffixPoolFastTests
{
    [Test]
    public void Selector_UsesExactItemFamilyPoolBeforeEveryNaturalRoll()
    {
        const string itemId = "pool-filter-item";
        var baseLookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var itemCatalog = new Dictionary<string, ItemTemplate>(StringComparer.Ordinal)
        {
            [itemId] = new ItemTemplate(
                itemId,
                Array.Empty<string>(),
                "blade",
                "Weapon",
                Array.Empty<string>(),
                ItemRarityTierValue.Common,
                ItemIdentityValue.Baseline,
                AffixPoolTag: "pool_blade",
                AllowedCraftOperations: new[]
                {
                    CraftOperationKindValue.Reforge,
                    CraftOperationKindValue.Seal,
                }),
        };
        var affixCatalog = new Dictionary<string, AffixTemplate>(StringComparer.Ordinal)
        {
            ["blade_implicit"] = Affix(
                "blade_implicit", "Implicit", "Weapon", "pool_blade"),
            ["blade_prefix"] = Affix(
                "blade_prefix", "Prefix", "Weapon", "pool_blade"),
            ["bow_implicit"] = Affix(
                "bow_implicit", "Implicit", "Weapon", "pool_bow"),
            ["reserved_blade"] = Affix(
                "reserved_blade", "Prefix", "Weapon", "pool_blade", itemLevelMin: 999),
            ["armor_blade"] = Affix(
                "armor_blade", "Suffix", "Armor", "pool_blade"),
        };
        var canonical = affixCatalog.Keys
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var lookup = new FakeCombatContentLookup(
            snapshot: baseLookup.Snapshot with
            {
                ItemCatalog = itemCatalog,
                AffixCatalog = affixCatalog,
            },
            firstPlayableSlice: new FirstPlayableSliceDefinition
            {
                AffixIds = canonical,
            });

        Assert.That(
            GeneratedItemAffixSelector.GetEligibleAffixIds(lookup, itemId),
            Is.EqualTo(new[] { "blade_implicit", "blade_prefix" }));
        for (var seed = 0; seed < 128; seed++)
        {
            Assert.That(
                GeneratedItemAffixSelector.Select(lookup, itemId, seed),
                Is.EqualTo(new[] { "blade_implicit", "blade_prefix" }));
        }
    }

    private static AffixTemplate Affix(
        string id,
        string tier,
        string slot,
        string poolTag,
        int itemLevelMin = 0)
    {
        return new AffixTemplate(
            id,
            new[] { poolTag },
            Array.Empty<string>(),
            Array.Empty<string>(),
            null,
            new[] { slot },
            BudgetScore: 8f,
            SpawnWeight: 1f,
            Tier: tier,
            ItemLevelMin: itemLevelMin,
            ValueMin: 0f,
            ValueMax: 1f);
    }
}
