using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 3 (GPT Pro J3 contact-on-tick / J12 projectile release-contact / J16 frame-rate permutation).
/// The contact-pin math must land the clip's contact frame exactly on <c>contactTick * dt</c> for any
/// clip length and playback speed, separate projectile release from contact within the windup budget,
/// and remain a pure function of ticks/clip (framerate-independent by construction).
/// </summary>
[Category("FastUnit")]
public sealed class BattleContactPinSchedulerTests
{
    private const float Dt = 0.1f;

    [TestCase(5, 0.40f, 1.2f, 1.0f)]
    [TestCase(5, 0.40f, 1.2f, 1.6f)]
    [TestCase(12, 0.70f, 0.8f, 1.0f)]
    [TestCase(3, 0.00f, 0.5f, 1.0f)]
    [TestCase(20, 0.45f, 2.0f, 0.5f)]
    [TestCase(7, 1.00f, 0.9f, 1.2f)]
    public void ContactFrame_LandsExactlyOnContactTick(int contactTick, float contactNorm, float clipLen, float speed)
    {
        var clipStart = BattleContactPinScheduler.ResolveClipStartSeconds(contactTick, contactNorm, clipLen, speed, Dt);
        var contactSeconds = BattleContactPinScheduler.ResolveContactSeconds(clipStart, contactNorm, clipLen, speed);

        Assert.That(contactSeconds, Is.EqualTo(contactTick * (double)Dt).Within(1e-9),
            "the contact frame must be sampled exactly at contactTick * dt (J3).");
    }

    [Test]
    public void Melee_ReleaseTick_EqualsContactTick()
    {
        Assert.That(BattleContactPinScheduler.ResolvePresentationReleaseTick(2, 5, authoredTravelTicks: 0), Is.EqualTo(5));
    }

    [Test]
    public void Projectile_ReleaseTick_IsEarlierThanContact_WithinWindupBudget()
    {
        // windup budget = 5 - 2 = 3 ticks; authored travel 2 -> release at contactTick - 2 = 3.
        Assert.That(BattleContactPinScheduler.ResolvePresentationReleaseTick(2, 5, authoredTravelTicks: 2), Is.EqualTo(3));
    }

    [Test]
    public void Projectile_Travel_IsClampedToWindupBudget()
    {
        // authored travel 10 but the windup budget is only 3 -> release clamped to the windup start (2).
        Assert.That(BattleContactPinScheduler.ResolvePresentationReleaseTick(2, 5, authoredTravelTicks: 10), Is.EqualTo(2));
        Assert.That(BattleContactPinScheduler.ResolvePresentationReleaseTick(2, 5, authoredTravelTicks: 10), Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void RecoveryEndTick_IsAtOrAfterContact()
    {
        var timing = BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.HitHeavy);
        var recoveryEnd = BattleContactPinScheduler.ResolvePresentationRecoveryEndTick(5, timing.RecoveryNorm, timing.ContactNorm, 1.0f, 1.0f, Dt);
        Assert.That(recoveryEnd, Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public void Schedule_IsDeterministic_AndFramerateIndependentByConstruction()
    {
        // The schedule is computed in tick/second space with no render-framerate input, so identical
        // logical inputs always yield an identical schedule regardless of how many frames render between
        // ticks (J16). Pin correctness is covered by ContactFrame_LandsExactlyOnContactTick.
        var timing = BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.None);
        var a = BattleContactPinScheduler.ResolveSchedule(2, 6, timing, 1.2f, 1.4f, 0, Dt);
        var b = BattleContactPinScheduler.ResolveSchedule(2, 6, timing, 1.2f, 1.4f, 0, Dt);

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.ContactTick, Is.EqualTo(6));
        Assert.That(a.PresentationReleaseTick, Is.EqualTo(6), "melee release equals contact.");
        Assert.That(a.PresentationRecoveryEndTick, Is.GreaterThanOrEqualTo(6));
        Assert.That(a.CatalogVersion, Is.EqualTo(BattleClipTimingCatalog.CatalogVersion));
    }
}
