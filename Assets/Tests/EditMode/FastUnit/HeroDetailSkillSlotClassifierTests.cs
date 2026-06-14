using NUnit.Framework;
using SM.Core.Contracts;
using SM.Unity.UI.HeroDetail;

namespace SM.Tests.EditMode.FastUnit;

/// <summary>
/// HeroDetail 4-slot 위계 도출 규칙 회귀. 런타임에 단일 권위 필드가 없으므로 도출 규칙 자체가 계약.
/// </summary>
[Category("FastUnit")]
public sealed class HeroDetailSkillSlotClassifierTests
{
    [Test]
    public void SignatureActive_AlwaysSignatureLock()
    {
        Assert.AreEqual(
            HeroDetailSlotKind.SignatureLock,
            HeroDetailSkillSlotClassifier.Classify(
                ActionSlotKind.SignatureActive,
                heroHasExplicitFlexChoice: true,
                isUnlockedViaProgression: true));
    }

    [Test]
    public void SignaturePassive_AlwaysSignatureLock()
    {
        Assert.AreEqual(
            HeroDetailSlotKind.SignatureLock,
            HeroDetailSkillSlotClassifier.Classify(
                ActionSlotKind.SignaturePassive,
                heroHasExplicitFlexChoice: false,
                isUnlockedViaProgression: false));
    }

    [Test]
    public void FlexSlot_ProgressionUnlock_WinsOverExplicitChoice()
    {
        Assert.AreEqual(
            HeroDetailSlotKind.LateUnlock,
            HeroDetailSkillSlotClassifier.Classify(
                ActionSlotKind.FlexActive,
                heroHasExplicitFlexChoice: true,
                isUnlockedViaProgression: true));
    }

    [Test]
    public void FlexSlot_ExplicitChoice_IsFlexActive()
    {
        Assert.AreEqual(
            HeroDetailSlotKind.FlexActive,
            HeroDetailSkillSlotClassifier.Classify(
                ActionSlotKind.FlexActive,
                heroHasExplicitFlexChoice: true,
                isUnlockedViaProgression: false));
    }

    [Test]
    public void FlexSlot_BaselineDefault_IsFlexRetrain()
    {
        Assert.AreEqual(
            HeroDetailSlotKind.FlexRetrain,
            HeroDetailSkillSlotClassifier.Classify(
                ActionSlotKind.FlexPassive,
                heroHasExplicitFlexChoice: false,
                isUnlockedViaProgression: false));
    }
}
