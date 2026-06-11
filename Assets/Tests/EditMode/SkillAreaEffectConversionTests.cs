using NUnit.Framework;
using SM.Combat.Model;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Unity.ContentConversion;
using UnityEngine;

namespace SM.Tests.EditMode;

/// <summary>
/// AoE 저작 필드(AreaEffectFamily/Radius/PunishCluster)가 콘텐츠 에셋 → 컴파일된 스킬 스펙으로
/// 손실 없이 흐르는 계약. 이 배선이 끊기면 저작된 AoE가 전부 단일 대상으로 강등된다(잠복 결함 재발 가드).
/// ScriptableObject 생성이 필요해 FastUnit 밖(EditMode 루트)에 둔다.
/// </summary>
[Category("BatchOnly")]
public sealed class SkillAreaEffectConversionTests
{
    [Test]
    public void Converter_CarriesAuthoredAreaEffect_IntoSkillSpec()
    {
        var asset = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        try
        {
            asset.Id = "skill_test_nova";
            asset.NameKey = "content.skill.skill_test_nova.name";
            asset.Kind = SkillKindValue.Strike;
            asset.Power = 4f;
            asset.Range = 1.5f;
            asset.AreaEffectFamily = AreaEffectFamilyValue.GroundAoe;
            asset.Radius = 2.5f;
            asset.PunishCluster = true;

            var spec = SkillConverter.BuildSkillSpec(asset);

            Assert.That(spec.AreaEffectFamily, Is.EqualTo(BattleAreaEffectFamily.GroundAoe));
            Assert.That(spec.AreaRadius, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(spec.PunishCluster, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void Converter_DefaultsToSingleTarget_AndClampsNegativeRadius()
    {
        var asset = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        try
        {
            asset.Id = "skill_test_plain";
            asset.NameKey = "content.skill.skill_test_plain.name";
            asset.Radius = -2f;

            var spec = SkillConverter.BuildSkillSpec(asset);

            Assert.That(spec.AreaEffectFamily, Is.EqualTo(BattleAreaEffectFamily.SingleTarget));
            Assert.That(spec.AreaRadius, Is.Zero, "음수 저작 반경은 0으로 강등");
            Assert.That(spec.PunishCluster, Is.False);
            Assert.That(spec.AllowsEliteFocusCap, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void Converter_DoesNotInferAoeFromRadiusAlone()
    {
        // 기존 콘텐츠의 radius>0 스킬은 전부 비데미지(버프/유틸/상태이상) 스킬 — radius만으로
        // AoE 가족을 추론하면 이들이 데미지 AoE 경로로 끌려 들어간다. 추론 금지 계약.
        var asset = ScriptableObject.CreateInstance<SkillDefinitionAsset>();
        try
        {
            asset.Id = "skill_test_status_zone";
            asset.NameKey = "content.skill.skill_test_status_zone.name";
            asset.Kind = SkillKindValue.Debuff;
            asset.Delivery = SkillDeliveryValue.Zone;
            asset.Radius = 2.75f;

            var spec = SkillConverter.BuildSkillSpec(asset);

            Assert.That(spec.AreaEffectFamily, Is.EqualTo(BattleAreaEffectFamily.SingleTarget));
            Assert.That(spec.AreaRadius, Is.EqualTo(2.75f).Within(0.001f), "반경 값 자체는 보존(가족이 열릴 때만 의미)");
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }
}
