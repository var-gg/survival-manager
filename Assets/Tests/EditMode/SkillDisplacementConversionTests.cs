using System.Linq;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Unity.ContentConversion;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// P3 강제이동 저작 필드가 콘텐츠 에셋 → 컴파일된 스킬 스펙으로 손실 없이 흐르는 계약.
/// ScriptableObject 생성이 필요해 FastUnit 밖(EditMode 루트)에 둔다.
/// </summary>
[Category("BatchOnly")]
public sealed class SkillDisplacementConversionTests
{
    [Test]
    public void Converter_CarriesAuthoredDisplacement_IntoSkillSpec()
    {
        var asset = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        try
        {
            asset.Id = "skill_test_charge";
            asset.NameKey = "content.skill.skill_test_charge.name";
            asset.Power = 4f;
            asset.Range = 1.5f;
            asset.DisplacementKind = SkillDisplacementKind.SelfTowardTarget;
            asset.DisplacementDistance = 2f;

            var spec = SkillConverter.BuildSkillSpec(asset);

            Assert.That(spec.DisplacementKind, Is.EqualTo(SkillDisplacementKind.SelfTowardTarget));
            Assert.That(spec.DisplacementDistance, Is.EqualTo(2f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void Converter_DefaultsToNoDisplacement_AndClampsNegativeDistance()
    {
        var asset = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        try
        {
            asset.Id = "skill_test_plain";
            asset.NameKey = "content.skill.skill_test_plain.name";
            asset.DisplacementDistance = -3f;

            var spec = SkillConverter.BuildSkillSpec(asset);

            Assert.That(spec.DisplacementKind, Is.EqualTo(SkillDisplacementKind.None));
            Assert.That(spec.DisplacementDistance, Is.Zero, "음수 저작 거리는 0으로 강등");
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void ShippedVeilBreach_CarriesBlinkOpeningLockSelfExposureAndRecruitDistribution()
    {
        const string skillPath = "Assets/Resources/_Game/Content/Definitions/Skills/skill_veil_breach.asset";
        var asset = AssetDatabase.LoadAssetAtPath<SkillDefinitionAsset>(skillPath);

        Assert.That(asset, Is.Not.Null, "the shipped recruit-flex skill asset must exist");
        var spec = SkillConverter.BuildSkillSpec(asset!);

        Assert.That(spec.Id, Is.EqualTo("skill_veil_breach"));
        Assert.That(spec.Range, Is.EqualTo(8f).Within(0.001f));
        Assert.That(spec.CastWindupSeconds, Is.EqualTo(0.9f).Within(0.001f));
        Assert.That(spec.BaseCooldownSeconds, Is.EqualTo(10f).Within(0.001f));
        Assert.That(spec.StartsOnCooldown, Is.True);
        Assert.That(spec.OpeningLockSeconds, Is.EqualTo(10f).Within(0.001f));
        Assert.That(spec.DisplacementKind, Is.EqualTo(SkillDisplacementKind.SelfBlinkToTarget));
        Assert.That(spec.DisplacementDistance, Is.EqualTo(8.5f).Within(0.001f));
        Assert.That(spec.TargetRuleData?.PrimarySelector, Is.EqualTo(TargetSelector.CurrentTarget));
        Assert.That(spec.TargetRuleData?.FallbackPolicy, Is.EqualTo(TargetFallbackPolicy.Abort));
        Assert.That(spec.AppliedStatuses, Has.Count.EqualTo(1));
        Assert.That(spec.AppliedStatuses![0].StatusId, Is.EqualTo("exposed"));
        Assert.That(spec.AppliedStatuses[0].Scope, Is.EqualTo(EffectScope.Self));
        Assert.That(spec.AppliedStatuses[0].Magnitude, Is.EqualTo(0.25f).Within(0.001f));

        foreach (var archetypeId in new[] { "slayer", "reaver", "raider" })
        {
            var archetype = AssetDatabase.LoadAssetAtPath<UnitArchetypeDefinition>(
                $"Assets/Resources/_Game/Content/Definitions/Archetypes/archetype_{archetypeId}.asset");
            Assert.That(archetype, Is.Not.Null, $"missing shipped archetype {archetypeId}");
            Assert.That(archetype!.RecruitFlexActivePool.Any(skill => skill != null && skill.Id == spec.Id), Is.True,
                $"{archetypeId} must distribute Veil Breach through RecruitFlexActivePool");
        }
    }
}
