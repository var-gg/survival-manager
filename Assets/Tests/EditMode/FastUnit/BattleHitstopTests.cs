using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 5 — pure hitstop math (GPT Pro J6/J15/J25). Locks that the pose-hold is an OUTPUT-time remap only:
/// the choreography clock <c>now</c> is an input that always advances (never frozen), the output is held at
/// the contact frame for the hold then catch-up blends back to live, a newer pinned contact wins a merge,
/// and the whole thing is a pure function (framerate-independent, no sim/schedule feedback).
/// </summary>
[Category("FastUnit")]
public sealed class BattleHitstopTests
{
    private const double Eps = 1e-9;
    private static BattleHitstopWindow Window() => new(ContactTime: 0.5, HoldSeconds: 0.07, CatchUpSeconds: 0.05);

    // ---- PoseHoldOutputOnly (J25) --------------------------------------------------------------

    [Test]
    public void Output_IsLive_OutsideTheWindow()
    {
        var w = Window();
        Assert.That(BattleHitstop.ResolveOutputTime(0.40, w), Is.EqualTo(0.40).Within(Eps), "before contact: live.");
        Assert.That(BattleHitstop.ResolveOutputTime(0.70, w), Is.EqualTo(0.70).Within(Eps), "after catch-up: live.");
    }

    [Test]
    public void Hold_FreezesOnContactFrame_WithoutStoppingTheClock()
    {
        var w = Window();
        // now advances 0.50 -> 0.57 (the clock is NOT frozen), but output stays pinned on the contact frame.
        foreach (var now in new[] { 0.50, 0.53, 0.5699 })
        {
            Assert.That(BattleHitstop.ResolveOutputTime(now, w), Is.EqualTo(0.50).Within(Eps), $"held at contact frame at now={now}.");
        }
    }

    [Test]
    public void CatchUp_RejoinsLiveExactly_AtCatchUpEnd()
    {
        var w = Window();
        Assert.That(BattleHitstop.ResolveOutputTime(w.CatchUpEnd, w), Is.EqualTo(w.CatchUpEnd).Within(Eps), "rejoins live at catch-up end.");
        Assert.That(BattleHitstop.ResolveOutputTime(w.HoldEnd, w), Is.EqualTo(0.50).Within(Eps), "catch-up starts continuous from the hold.");
    }

    [Test]
    public void Output_IsContinuous_Monotonic_AndNeverAheadOfLive()
    {
        var w = Window();
        var prev = BattleHitstop.ResolveOutputTime(0.0, w);
        for (var now = 0.0; now <= 1.0 + 1e-9; now += 0.001)
        {
            var output = BattleHitstop.ResolveOutputTime(now, w);
            Assert.That(output, Is.LessThanOrEqualTo(now + 1e-9), $"output must never run ahead of live time at now={now}.");
            Assert.That(output, Is.GreaterThanOrEqualTo(prev - 1e-6), $"output must be monotonic at now={now}.");
            Assert.That(output - prev, Is.LessThanOrEqualTo(0.01 + 1e-6), $"no output jump at now={now} (continuous).");
            prev = output;
        }
    }

    // ---- FrameRatePermutationChoreography (J16) -------------------------------------------------

    [Test]
    public void Output_IsPureFunctionOfNow_IndependentOfSampleCadence()
    {
        var w = Window();
        // A dense sweep and a single coarse jump must agree at the same `now`: hitstop carries no state,
        // so render cadence (or a catch-up frame skipping the hold) cannot change the sampled output.
        const double probe = 0.59;
        double dense = 0;
        for (var now = 0.50; now <= probe + 1e-9; now += 0.0005)
        {
            dense = BattleHitstop.ResolveOutputTime(now, w);
        }

        var coarse = BattleHitstop.ResolveOutputTime(probe, w);
        Assert.That(dense, Is.EqualTo(coarse).Within(Eps));
    }

    // ---- HitstopDomainMerge --------------------------------------------------------------------

    [Test]
    public void Merge_NewerContact_TruncatesAndReplaces()
    {
        var current = Window();                                  // contact 0.50
        var incoming = new BattleHitstopWindow(0.55, 0.07, 0.05); // a second contact lands during the hold
        var merged = BattleHitstop.Merge(current, incoming);
        Assert.That(merged.ContactTime, Is.EqualTo(0.55).Within(Eps), "the newer pinned contact wins (contact pin priority).");
    }

    [Test]
    public void Merge_EmptyIncoming_KeepsCurrent()
    {
        var current = Window();
        Assert.That(BattleHitstop.Merge(current, BattleHitstopWindow.None).ContactTime, Is.EqualTo(0.50).Within(Eps));
    }

    // ---- Catalog -------------------------------------------------------------------------------

    [Test]
    public void Catalog_HeavierIntensity_HoldsLonger()
    {
        var light = BattleHitstopCatalog.HoldSecondsFor(BattleAnimationIntensity.Light);
        var medium = BattleHitstopCatalog.HoldSecondsFor(BattleAnimationIntensity.Medium);
        var heavy = BattleHitstopCatalog.HoldSecondsFor(BattleAnimationIntensity.Heavy);
        Assert.That(light, Is.GreaterThan(0f));
        Assert.That(medium, Is.GreaterThan(light));
        Assert.That(heavy, Is.GreaterThan(medium));
    }

    [Test]
    public void Catalog_AnyIntensity_HasNoHitstop()
    {
        Assert.That(BattleHitstopCatalog.HoldSecondsFor(BattleAnimationIntensity.Any), Is.EqualTo(0f));
        Assert.That(BattleHitstopCatalog.ResolveWindow(0.5, BattleAnimationIntensity.Any), Is.EqualTo(BattleHitstopWindow.None));
    }

    [Test]
    public void Catalog_ResolveWindow_UsesContactTimeAndDefaultCatchUp()
    {
        var w = BattleHitstopCatalog.ResolveWindow(0.5, BattleAnimationIntensity.Heavy);
        Assert.That(w.ContactTime, Is.EqualTo(0.5).Within(Eps));
        Assert.That(w.HoldSeconds, Is.EqualTo(BattleHitstopCatalog.HoldSecondsFor(BattleAnimationIntensity.Heavy)).Within(Eps));
        Assert.That(w.CatchUpSeconds, Is.EqualTo(BattleHitstopCatalog.DefaultCatchUpSeconds).Within(Eps));
    }

    [Test]
    public void Catalog_Version_IsStableAndPositive()
    {
        Assert.That(BattleHitstopCatalog.CatalogVersion, Is.EqualTo(1));
    }
}
