using System;
using System.Linq;
using NUnit.Framework;
using SM.Core.Content;
using SM.Persistence.Abstractions.Models;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class AffixMagnitudeRuntimeTests
{
    [Test]
    public void GeneratedItem_PersistsDeterministicPerInstanceMagnitudesWithinAuthoredRanges()
    {
        var lookup = new RuntimeCombatContentLookup();
        var itemBaseId = lookup.GetCanonicalItemIds().First();
        var firstProfile = new SaveProfile { ProfileId = "magnitude-first" };
        var secondProfile = new SaveProfile { ProfileId = "magnitude-second" };
        var builder = new SessionInventoryItemBuilder(lookup, BuildStableSeed);

        var first = builder.CreateGeneratedInventoryItem(
            firstProfile,
            itemBaseId,
            "item-instance-a",
            rolledRarityTier: (int)ItemRarityTierValue.Legendary);
        var second = builder.CreateGeneratedInventoryItem(
            secondProfile,
            itemBaseId,
            "item-instance-b",
            rolledRarityTier: (int)ItemRarityTierValue.Legendary);

        Assert.That(first.AffixMagnitudeRolls, Has.Count.EqualTo(first.AffixIds.Count));
        Assert.That(second.AffixIds, Is.EqualTo(first.AffixIds));
        Assert.That(
            second.AffixMagnitudeRolls.Select(value => BitConverter.SingleToInt32Bits(value.Magnitude)),
            Is.EqualTo(first.AffixMagnitudeRolls.Select(value => BitConverter.SingleToInt32Bits(value.Magnitude))));

        foreach (var roll in first.AffixMagnitudeRolls)
        {
            Assert.That(lookup.TryGetAffixDefinition(roll.AffixId, out var definition), Is.True);
            Assert.That(roll.Magnitude, Is.InRange(
                Math.Min(definition.ValueMin, definition.ValueMax),
                Math.Max(definition.ValueMin, definition.ValueMax)));
        }

        Assert.That(first.AffixMagnitudeRolls.Any(roll =>
        {
            lookup.TryGetAffixDefinition(roll.AffixId, out var definition);
            return definition.Modifiers.Count > 0
                   && BitConverter.SingleToInt32Bits(roll.Magnitude)
                   != BitConverter.SingleToInt32Bits(definition.Modifiers[0].Value);
        }), Is.True, "The generated item must carry actual magnitude variance, not copied definition constants.");
    }

    private static int BuildStableSeed(string value, int salt)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value ?? string.Empty)
            {
                hash = (hash * 31) + character;
            }

            hash = (hash * 31) + salt;
            return hash & int.MaxValue;
        }
    }
}
