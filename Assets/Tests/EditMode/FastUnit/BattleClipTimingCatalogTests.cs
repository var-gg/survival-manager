using System;
using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 3 (GPT Pro J18 metadata completeness / J26 catalog version). The graybox timing table must
/// resolve every animation semantic to a sane, deterministic <see cref="BattleClipTiming"/> — no gaps,
/// no runtime heuristics — and carry a stable version for the replay/save contract.
/// </summary>
[Category("FastUnit")]
public sealed class BattleClipTimingCatalogTests
{
    [Test]
    public void EverySemantic_ResolvesToTiming_WithNormsInRangeAndOrdered()
    {
        foreach (BattleAnimationSemantic semantic in Enum.GetValues(typeof(BattleAnimationSemantic)))
        {
            var timing = BattleClipTimingCatalog.Resolve(semantic);
            Assert.That(timing.ContactNorm, Is.InRange(0f, 1f), $"{semantic} ContactNorm out of range.");
            Assert.That(timing.ReleaseNorm, Is.InRange(0f, 1f), $"{semantic} ReleaseNorm out of range.");
            Assert.That(timing.RecoveryNorm, Is.InRange(0f, 1f), $"{semantic} RecoveryNorm out of range.");
            Assert.That(timing.RecoveryNorm, Is.GreaterThanOrEqualTo(timing.ContactNorm), $"{semantic} recovery cannot precede contact.");
        }
    }

    [Test]
    public void Resolve_IsDeterministic()
    {
        foreach (BattleAnimationSemantic semantic in Enum.GetValues(typeof(BattleAnimationSemantic)))
        {
            Assert.That(BattleClipTimingCatalog.Resolve(semantic), Is.EqualTo(BattleClipTimingCatalog.Resolve(semantic)));
        }
    }

    [Test]
    public void HitReactions_PinToContactFrameZero()
    {
        foreach (var semantic in new[]
                 {
                     BattleAnimationSemantic.HitLight,
                     BattleAnimationSemantic.HitHeavy,
                     BattleAnimationSemantic.CriticalImpact,
                     BattleAnimationSemantic.BlockImpact,
                     BattleAnimationSemantic.Knockdown,
                 })
        {
            var timing = BattleClipTimingCatalog.Resolve(semantic);
            Assert.That(timing.CanContactPin, Is.True, $"{semantic} must be contact-pinnable.");
            Assert.That(timing.ContactNorm, Is.EqualTo(0f), $"{semantic} hit-reaction must start on the contact frame.");
        }
    }

    [Test]
    public void RangedReleaseIsEarlyInSplitReleaseClip()
    {
        // v2 계약: commit에 배선되는 활/시전 클립은 Load/Hold/Release 분할 팩의 Release 전용
        // 클립이다 — 시위를 놓는 순간은 그 클립의 초반부에 온다(노름이 크면 windup 예산이
        // 클립 앞부분을 통째로 건너뛰어 '움찔 트윗치'로 보인다). 근접 스윙 미드포인트(0.40)보다
        // 빨라야 한다.
        Assert.That(
            BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.BowShot).ReleaseNorm,
            Is.LessThan(BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.None).ContactNorm),
            "a split Release-only bow clip looses early in the clip.");
        Assert.That(
            BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.ProjectileCast).ReleaseNorm,
            Is.LessThanOrEqualTo(0.35f),
            "a split Cast-only clip releases early as well.");
    }

    [Test]
    public void LocomotionAndGuard_AreNotContactPinnable()
    {
        foreach (var semantic in new[]
                 {
                     BattleAnimationSemantic.BackstepDisengage,
                     BattleAnimationSemantic.LateralStrafe,
                     BattleAnimationSemantic.GuardPose,
                 })
        {
            Assert.That(BattleClipTimingCatalog.Resolve(semantic).CanContactPin, Is.False, $"{semantic} must not be contact-pinned.");
        }
    }

    [Test]
    public void CatalogVersion_IsStableAndPositive()
    {
        Assert.That(BattleClipTimingCatalog.CatalogVersion, Is.GreaterThan(0));
        Assert.That(BattleClipTimingCatalog.CatalogVersion, Is.EqualTo(2), "bump deliberately when the default table changes (J26). v2 = 분할 클립 release 노름 보정.");
    }
}
