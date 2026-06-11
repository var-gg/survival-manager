using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Contracts;
using SM.Unity.ContentConversion;
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
}
