using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
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
    public void LegacySavedItem_WithAffixIdsAndNoRolls_BackfillsOneDeterministicRollPerAffix()
    {
        var lookup = new RuntimeCombatContentLookup();
        var itemBaseId = lookup.GetCanonicalItemIds().First();
        var builder = new SessionInventoryItemBuilder(lookup, BuildStableSeed);
        var affixIds = CreateAffixIds(builder, itemBaseId);
        var stranded = CreateStrandedItem(itemBaseId, affixIds);
        var replica = CreateStrandedItem(itemBaseId, affixIds);

        Assert.That(builder.EnsureAffixMagnitudeRolls(stranded, itemBaseId), Is.True);
        builder.EnsureAffixMagnitudeRolls(replica, itemBaseId);

        Assert.That(
            stranded.AffixMagnitudeRolls.Select(roll => roll.AffixId),
            Is.EqualTo(stranded.AffixIds),
            "접사 하나당 굴림 하나가 접사 순서 그대로 있어야 한다.");
        foreach (var roll in stranded.AffixMagnitudeRolls)
        {
            Assert.That(lookup.TryGetAffixDefinition(roll.AffixId, out var definition), Is.True);
            Assert.That(roll.Magnitude, Is.InRange(
                Math.Min(definition.ValueMin, definition.ValueMax),
                Math.Max(definition.ValueMin, definition.ValueMax)));
        }

        Assert.That(
            replica.AffixMagnitudeRolls.Select(roll => BitConverter.SingleToInt32Bits(roll.Magnitude)),
            Is.EqualTo(stranded.AffixMagnitudeRolls.Select(roll => BitConverter.SingleToInt32Bits(roll.Magnitude))),
            "같은 저장 아이템은 몇 번을 로드해도 같은 굴림을 내야 한다.");

        // 두 번째 호출이 값을 건드리면 그건 굴림이 아니라 로드마다 새로 뽑는 난수다.
        var settled = stranded.AffixMagnitudeRolls
            .Select(roll => BitConverter.SingleToInt32Bits(roll.Magnitude))
            .ToArray();
        Assert.That(builder.EnsureAffixMagnitudeRolls(stranded, itemBaseId), Is.False);
        Assert.That(
            stranded.AffixMagnitudeRolls.Select(roll => BitConverter.SingleToInt32Bits(roll.Magnitude)),
            Is.EqualTo(settled));
    }

    [Test]
    public void AffixPadding_FillsMissingRolls_EvenWhenAffixCountAlreadyMeetsTarget()
    {
        var lookup = new RuntimeCombatContentLookup();
        var itemBaseId = lookup.GetCanonicalItemIds().First();
        var builder = new SessionInventoryItemBuilder(lookup, BuildStableSeed);
        var affixIds = CreateAffixIds(builder, itemBaseId);
        var stranded = CreateStrandedItem(itemBaseId, affixIds);

        // 접사 수가 이미 목표치인 record — 패딩이 여기서 빠져나가며 굴림을 영원히 비워 두고 있었다.
        builder.EnsureAffixPadding(stranded, itemBaseId, stranded.AffixIds.Count);

        Assert.That(
            stranded.AffixMagnitudeRolls.Select(roll => roll.AffixId),
            Is.EqualTo(stranded.AffixIds));
    }

    [Test]
    public void ProfileBind_MigratesSavedItemsThatCarryAffixIdsWithoutRolls()
    {
        var lookup = new RuntimeCombatContentLookup();
        var itemBaseId = lookup.GetCanonicalItemIds().First();
        var builder = new SessionInventoryItemBuilder(lookup, BuildStableSeed);
        var affixIds = CreateAffixIds(builder, itemBaseId);
        var session = GameSessionTestFactory.Create(lookup);

        session.BindProfile(new SaveProfile
        {
            ProfileId = "magnitude-legacy-bind",
            // hero 를 비우면 bind 가 데모 프로필을 대신 심어 인벤토리가 이 아이템만 있지 않게 된다.
            Heroes = new List<HeroInstanceRecord>
            {
                new() { HeroId = "magnitude-legacy-hero", EquippedItemIds = new List<string>() },
            },
            Inventory = new List<InventoryItemRecord>
            {
                CreateStrandedItem(itemBaseId, affixIds),
            },
        });

        var bound = session.Profile.Inventory.Single(item =>
            string.Equals(item.ItemInstanceId, "item-instance-legacy", StringComparison.Ordinal));
        Assert.That(bound.AffixIds, Is.Not.Empty);
        Assert.That(
            bound.AffixMagnitudeRolls.Select(roll => roll.AffixId),
            Is.EqualTo(bound.AffixIds),
            "굴림 없는 접사를 남기면 그 아이템은 저작 기준값으로 싸우고 Seal 도 쓸 수 없다.");

        // 로드가 끝난 인벤토리에는 굴림 없는 접사가 한 개도 남아 있으면 안 된다.
        foreach (var item in session.Profile.Inventory)
        {
            Assert.That(
                item.AffixMagnitudeRolls.Select(roll => roll.AffixId),
                Is.EqualTo(item.AffixIds),
                $"item '{item.ItemInstanceId}' 의 접사와 굴림이 어긋난 채로 로드를 통과했다.");
        }
    }

    private static IReadOnlyList<string> CreateAffixIds(
        SessionInventoryItemBuilder builder,
        string itemBaseId)
    {
        var generated = builder.CreateGeneratedInventoryItem(
            new SaveProfile { ProfileId = "magnitude-legacy-source" },
            itemBaseId,
            "item-instance-legacy",
            rolledRarityTier: (int)ItemRarityTierValue.Legendary);
        Assert.That(generated.AffixIds, Is.Not.Empty);
        return generated.AffixIds;
    }

    /// <summary>저장된 레거시 아이템의 지문 — 접사 id는 있는데 굴림 목록이 비어 있다.</summary>
    private static InventoryItemRecord CreateStrandedItem(
        string itemBaseId,
        IReadOnlyList<string> affixIds)
        => new()
        {
            ItemInstanceId = "item-instance-legacy",
            ItemBaseId = itemBaseId,
            AffixIds = affixIds.ToList(),
            AffixMagnitudeRolls = new List<InventoryAffixMagnitudeRecord>(),
        };

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
            // 굴림 문맥 문구는 시드 폴백을 그대로 쓴다 — 한국어 화면 문구가 곧 기대값이다.
            Assert.That(
                text.FormatAffixRollContext(readout),
                Is.EqualTo("굴림 품질 100%"));
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
            Assert.That(rollContext, Is.EqualTo("굴림 없음 · 기본치"));
            Assert.That(rollContext, Does.Not.Contain("품질"));
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
