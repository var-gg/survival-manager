using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Persistence.Abstractions.Models;
using SM.Unity;
using SM.Unity.UI.Town.Preview;
using UnityEngine;

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

    [Test]
    public void Presentation_MultiModifierRolledInstance_RendersEveryScaledEffectInItsOperationUnit()
    {
        var definition = CreateTradeoffDefinition();
        try
        {
            var item = new InventoryItemRecord
            {
                AffixMagnitudeRolls = new List<InventoryAffixMagnitudeRecord>
                {
                    new()
                    {
                        AffixId = definition.Id,
                        Magnitude = 0.05f,
                    },
                },
            };
            var text = CreateEnglishPresentationText();

            var readout = AffixMagnitudePresentation.Build(item, definition);
            var renderedEffects = readout.Effects
                .Select(text.FormatAffixEffect)
                .ToArray();

            Assert.That(readout.HasPersistedRoll, Is.True);
            Assert.That(readout.Effects.Count, Is.EqualTo(2));
            Assert.That(readout.Effects[0].Value, Is.EqualTo(0.05f).Within(0.000001f));
            Assert.That(readout.Effects[1].Value, Is.EqualTo(-0.05f).Within(0.000001f));
            Assert.That(renderedEffects, Is.EqualTo(new[]
            {
                "Life Steal +0.05",
                "Max Health -5%",
            }));
            Assert.That(
                text.FormatAffixRollContext(readout),
                Is.EqualTo("Roll quality 100%"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void Presentation_UnpersistedLegacyMagnitude_UsesBaselineWithoutRollQualityClaim()
    {
        var definition = CreateTradeoffDefinition();
        try
        {
            var readout = AffixMagnitudePresentation.Build(
                new InventoryItemRecord(),
                definition);
            var text = CreateEnglishPresentationText();
            var rollContext = text.FormatAffixRollContext(readout);

            Assert.That(readout.HasPersistedRoll, Is.False);
            Assert.That(readout.Effects[0].Value, Is.EqualTo(0.04f).Within(0.000001f));
            Assert.That(readout.Effects[1].Value, Is.EqualTo(-0.04f).Within(0.000001f));
            Assert.That(rollContext, Is.EqualTo("Legacy baseline"));
            Assert.That(rollContext, Does.Not.Contain("quality").IgnoreCase);
            Assert.That(rollContext, Does.Not.Contain("%"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void Presentation_MissingStatDisplayName_UsesLocalizedFallbackWithoutRawStatId()
    {
        var definition = ScriptableObject.CreateInstance<AffixDefinition>();
        try
        {
            definition.Id = "affix_missing_stat_name";
            definition.ValueMin = 1f;
            definition.ValueMax = 2f;
            definition.Modifiers = new List<SerializableStatModifier>
            {
                new()
                {
                    StatId = "skill_haste",
                    Op = ModifierOp.Flat,
                    Value = 1f,
                },
            };
            var text = CreateEnglishPresentationText();
            var readout = AffixMagnitudePresentation.Build(
                new InventoryItemRecord(),
                definition);

            var rendered = text.FormatAffixEffect(readout.Effects.Single());

            Assert.That(rendered, Is.EqualTo("Unknown stat +1"));
            Assert.That(rendered, Does.Not.Contain("skill_haste"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static AffixDefinition CreateTradeoffDefinition()
    {
        var definition = ScriptableObject.CreateInstance<AffixDefinition>();
        definition.Id = "affix_blood_price";
        definition.ValueMin = 0.03f;
        definition.ValueMax = 0.05f;
        definition.Modifiers = new List<SerializableStatModifier>
        {
            new()
            {
                StatId = "lifesteal",
                Op = ModifierOp.Flat,
                Value = 0.04f,
            },
            new()
            {
                StatId = "max_health",
                Op = ModifierOp.Increased,
                Value = -0.04f,
            },
        };
        return definition;
    }

    private static EquipmentRefitText CreateEnglishPresentationText()
    {
        var culture = CultureInfo.GetCultureInfo("en-US");
        return new EquipmentRefitText((_, key, fallback, arguments) =>
        {
            var template = key switch
            {
                "content.stat.lifesteal.name" => "Life Steal",
                "content.stat.max_health.name" => "Max Health",
                _ => fallback,
            };
            return arguments.Length == 0
                ? template
                : string.Format(culture, template, arguments);
        });
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
