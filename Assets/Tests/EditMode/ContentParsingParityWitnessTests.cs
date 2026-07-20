using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Editor.SeedData;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// 파일 파서 폴백 레인 vs Resources 정식 레인의 payload parity witness.
/// 폴백 레인은 복구 전용이지만 TriggeredEffects/SupportModifier를 못 읽으면 그 레인에서만
/// 발동형 패시브·서포트 젬·유령 패시브 부여 스킬이 조용히 inert가 된다(2026-07 위생 봉합 대상).
/// 실 committed 콘텐츠 전수로 두 레인의 값을 대조해 parity 갭 재발을 적발한다.
/// </summary>
[Category("BatchOnly")]
public sealed class ContentParsingParityWitnessTests
{
    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(ContentParsingParityWitnessTests));
    }

    [Test]
    public void ParserLane_MatchesResourcesLane_ForSkillEffectPayloads()
    {
        Assert.That(RuntimeCombatContentFileParser.TryLoad(out var parsed, out var error), Is.True,
            $"파일 파서 레인이 실 콘텐츠 루트를 읽을 수 있어야 한다: {error}");
        var parsedSkills = parsed.Skills.ToDictionary(skill => skill.Id, StringComparer.Ordinal);
        var assetSkills = Resources.LoadAll<SkillDefinitionAsset>("_Game/Content/Definitions/Skills");
        Assert.That(assetSkills, Is.Not.Empty);

        var triggeredCarriers = 0;
        var nonIdentitySupports = 0;
        foreach (var asset in assetSkills)
        {
            Assert.That(parsedSkills.TryGetValue(asset.Id, out var fromParser), Is.True,
                $"파서 레인에 스킬 '{asset.Id}'이 존재해야 한다");

            AssertTriggeredEffectsEqual(asset.Id, asset.TriggeredEffects, fromParser!.TriggeredEffects);
            AssertSupportModifierEqual(asset.Id, asset.SupportModifier, fromParser.SupportModifier);

            if (asset.TriggeredEffects.Count > 0)
            {
                triggeredCarriers++;
            }

            if (Math.Abs(asset.SupportModifier.PowerMultiplier - 1f) > float.Epsilon
                || Math.Abs(asset.SupportModifier.CooldownMultiplier - 1f) > float.Epsilon
                || asset.SupportModifier.AddedStatuses.Count > 0
                || asset.SupportModifier.OwnerModifiers.Count > 0)
            {
                nonIdentitySupports++;
            }
        }

        // 공허 통과 가드 — 실효 payload가 실제로 존재하는 상태에서의 parity여야 의미가 있다.
        Assert.That(triggeredCarriers, Is.GreaterThan(0),
            "발동형 payload 보유 스킬이 최소 하나는 있어야 parity 검증이 공허하지 않다");
        Assert.That(nonIdentitySupports, Is.GreaterThan(0),
            "비-identity SupportModifier 젬이 최소 하나는 있어야 parity 검증이 공허하지 않다");
    }

    [Test]
    public void ParserLane_MatchesResourcesLane_ForAugmentTriggeredEffects()
    {
        Assert.That(RuntimeCombatContentFileParser.TryLoad(out var parsed, out var error), Is.True, error);
        var parsedAugments = parsed.Augments.ToDictionary(augment => augment.Id, StringComparer.Ordinal);
        var assetAugments = Resources.LoadAll<AugmentDefinition>("_Game/Content/Definitions/Augments");
        Assert.That(assetAugments, Is.Not.Empty);

        foreach (var asset in assetAugments)
        {
            Assert.That(parsedAugments.TryGetValue(asset.Id, out var fromParser), Is.True,
                $"파서 레인에 증강 '{asset.Id}'이 존재해야 한다");
            AssertTriggeredEffectsEqual(asset.Id, asset.TriggeredEffects, fromParser!.TriggeredEffects);
        }
    }

    [Test]
    public void ParserLane_MatchesResourcesLane_ForCleanseGrantedStatusId()
    {
        Assert.That(RuntimeCombatContentFileParser.TryLoad(out var parsed, out var error), Is.True, error);
        var parsedProfiles = parsed.CleanseProfiles.ToDictionary(profile => profile.Id, StringComparer.Ordinal);
        var assetProfiles = Resources.LoadAll<CleanseProfileDefinition>("_Game/Content/Definitions/CleanseProfiles");
        Assert.That(assetProfiles, Is.Not.Empty);
        Assert.That(assetProfiles.Any(profile => profile.GrantsUnstoppable), Is.True,
            "저지불가 부여 프로필이 최소 하나는 있어야 parity 검증이 공허하지 않다");

        foreach (var asset in assetProfiles)
        {
            Assert.That(parsedProfiles.TryGetValue(asset.Id, out var fromParser), Is.True,
                $"파서 레인에 정화 프로필 '{asset.Id}'이 존재해야 한다");
            Assert.That(fromParser!.GrantedStatusId, Is.EqualTo(asset.GrantedStatusId),
                $"'{asset.Id}' 부여 상태 id(GrantedStatusId)가 레인 간 일치해야 한다");
        }
    }

    [Test]
    public void ParserLane_MatchesResourcesLane_ForSiteEvents()
    {
        Assert.That(RuntimeCombatContentFileParser.TryLoad(out var parsed, out var error), Is.True, error);
        var parsedEvents = parsed.SiteEvents.ToDictionary(siteEvent => siteEvent.Id, StringComparer.Ordinal);
        var assetEvents = Resources.LoadAll<SiteEventDefinition>("_Game/Content/Definitions/SiteEvents");
        Assert.That(assetEvents, Has.Length.EqualTo(6));

        foreach (var expected in assetEvents)
        {
            Assert.That(parsedEvents.TryGetValue(expected.Id, out var actual), Is.True, expected.Id);
            Assert.That(actual!.SiteId, Is.EqualTo(expected.SiteId), $"{expected.Id}.SiteId");
            Assert.That(actual.SetupKey, Is.EqualTo(expected.SetupKey), $"{expected.Id}.SetupKey");
            Assert.That(actual.Choices, Has.Count.EqualTo(expected.Choices.Count), $"{expected.Id}.Choices");
            for (var choiceIndex = 0; choiceIndex < expected.Choices.Count; choiceIndex++)
            {
                var expectedChoice = expected.Choices[choiceIndex];
                var actualChoice = actual.Choices[choiceIndex];
                Assert.That(actualChoice.Id, Is.EqualTo(expectedChoice.Id), $"{expected.Id}.Choices[{choiceIndex}].Id");
                Assert.That(actualChoice.LabelKey, Is.EqualTo(expectedChoice.LabelKey), $"{expected.Id}.Choices[{choiceIndex}].LabelKey");
                Assert.That(actualChoice.Outcomes, Has.Count.EqualTo(expectedChoice.Outcomes.Count), $"{expected.Id}.Choices[{choiceIndex}].Outcomes");
                for (var outcomeIndex = 0; outcomeIndex < expectedChoice.Outcomes.Count; outcomeIndex++)
                {
                    var expectedOutcome = expectedChoice.Outcomes[outcomeIndex];
                    var actualOutcome = actualChoice.Outcomes[outcomeIndex];
                    Assert.That(actualOutcome.Kind, Is.EqualTo(expectedOutcome.Kind));
                    Assert.That(actualOutcome.PayloadId, Is.EqualTo(expectedOutcome.PayloadId));
                    Assert.That(actualOutcome.AuxiliaryId, Is.EqualTo(expectedOutcome.AuxiliaryId));
                    Assert.That(actualOutcome.Amount, Is.EqualTo(expectedOutcome.Amount));
                    Assert.That(actualOutcome.TargetRule, Is.EqualTo(expectedOutcome.TargetRule));
                }
            }
        }
    }

    private static void AssertTriggeredEffectsEqual(
        string contentId,
        IReadOnlyList<TriggeredEffectSpec> fromAsset,
        IReadOnlyList<TriggeredEffectSpec> fromParser)
    {
        Assert.That(fromParser.Count, Is.EqualTo(fromAsset.Count),
            $"'{contentId}' TriggeredEffects 개수가 레인 간 일치해야 한다");
        for (var i = 0; i < fromAsset.Count; i++)
        {
            var expected = fromAsset[i];
            var actual = fromParser[i];
            Assert.That(actual.Trigger, Is.EqualTo(expected.Trigger), $"{contentId}[{i}].Trigger");
            Assert.That(actual.Op, Is.EqualTo(expected.Op), $"{contentId}[{i}].Op");
            Assert.That(actual.Scope, Is.EqualTo(expected.Scope), $"{contentId}[{i}].Scope");
            Assert.That(actual.Magnitude, Is.EqualTo(expected.Magnitude), $"{contentId}[{i}].Magnitude");
            Assert.That(actual.ThresholdRatio, Is.EqualTo(expected.ThresholdRatio), $"{contentId}[{i}].ThresholdRatio");
            Assert.That(actual.StatusId, Is.EqualTo(expected.StatusId), $"{contentId}[{i}].StatusId");
            Assert.That(actual.DurationSeconds, Is.EqualTo(expected.DurationSeconds), $"{contentId}[{i}].DurationSeconds");
            Assert.That(actual.MaxStacks, Is.EqualTo(expected.MaxStacks), $"{contentId}[{i}].MaxStacks");
        }
    }

    private static void AssertSupportModifierEqual(string contentId, SupportModifierSpec expected, SupportModifierSpec actual)
    {
        Assert.That(actual.PowerMultiplier, Is.EqualTo(expected.PowerMultiplier), $"{contentId}.PowerMultiplier");
        Assert.That(actual.CooldownMultiplier, Is.EqualTo(expected.CooldownMultiplier), $"{contentId}.CooldownMultiplier");
        Assert.That(actual.CastWindupMultiplier, Is.EqualTo(expected.CastWindupMultiplier), $"{contentId}.CastWindupMultiplier");
        Assert.That(actual.RangeBonus, Is.EqualTo(expected.RangeBonus), $"{contentId}.RangeBonus");
        Assert.That(actual.StatusDurationMultiplier, Is.EqualTo(expected.StatusDurationMultiplier), $"{contentId}.StatusDurationMultiplier");
        Assert.That(actual.ForceCanCrit, Is.EqualTo(expected.ForceCanCrit), $"{contentId}.ForceCanCrit");
        Assert.That(actual.GrantCleanseProfileId, Is.EqualTo(expected.GrantCleanseProfileId), $"{contentId}.GrantCleanseProfileId");

        Assert.That(actual.AddedStatuses.Count, Is.EqualTo(expected.AddedStatuses.Count), $"{contentId}.AddedStatuses 개수");
        for (var i = 0; i < expected.AddedStatuses.Count; i++)
        {
            var expectedRule = expected.AddedStatuses[i];
            var actualRule = actual.AddedStatuses[i];
            Assert.That(actualRule.Id, Is.EqualTo(expectedRule.Id), $"{contentId}.AddedStatuses[{i}].Id");
            Assert.That(actualRule.StatusId, Is.EqualTo(expectedRule.StatusId), $"{contentId}.AddedStatuses[{i}].StatusId");
            Assert.That(actualRule.DurationSeconds, Is.EqualTo(expectedRule.DurationSeconds), $"{contentId}.AddedStatuses[{i}].DurationSeconds");
            Assert.That(actualRule.Magnitude, Is.EqualTo(expectedRule.Magnitude), $"{contentId}.AddedStatuses[{i}].Magnitude");
            Assert.That(actualRule.MaxStacks, Is.EqualTo(expectedRule.MaxStacks), $"{contentId}.AddedStatuses[{i}].MaxStacks");
            Assert.That(actualRule.RefreshDurationOnReapply, Is.EqualTo(expectedRule.RefreshDurationOnReapply), $"{contentId}.AddedStatuses[{i}].RefreshDurationOnReapply");
            Assert.That(actualRule.StackCap, Is.EqualTo(expectedRule.StackCap), $"{contentId}.AddedStatuses[{i}].StackCap");
            if (string.Equals(contentId, "skill_sunder_rhythm", StringComparison.Ordinal))
            {
                Assert.That(actualRule.StackCap, Is.EqualTo(3), "skill_sunder_rhythm StackCap parser parity");
            }
        }

        Assert.That(actual.OwnerModifiers.Count, Is.EqualTo(expected.OwnerModifiers.Count), $"{contentId}.OwnerModifiers 개수");
        for (var i = 0; i < expected.OwnerModifiers.Count; i++)
        {
            Assert.That(actual.OwnerModifiers[i].StatId, Is.EqualTo(expected.OwnerModifiers[i].StatId), $"{contentId}.OwnerModifiers[{i}].StatId");
            Assert.That(actual.OwnerModifiers[i].Op, Is.EqualTo(expected.OwnerModifiers[i].Op), $"{contentId}.OwnerModifiers[{i}].Op");
            Assert.That(actual.OwnerModifiers[i].Value, Is.EqualTo(expected.OwnerModifiers[i].Value), $"{contentId}.OwnerModifiers[{i}].Value");
        }
    }
}
