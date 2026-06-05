using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// D2 firing — pure contact-pin math (GPT Pro RECONCILIATION_CHOICE = a: offset + pre-roll hold). Locks
/// that the strike clip's authored contact frame (<c>tc = ContactNorm·clipLength</c>) is sampled exactly
/// when the windup budget elapses (<c>clipLocal(budget) ≡ tc</c>) for short / exact / long / zero budgets,
/// without warping the clip speed (D2-R4), and that the elapsed clock is fixed-step anchored (Q5 guard B).
/// </summary>
[Category("FastUnit")]
public sealed class BattleContactPinPlanTests
{
    private const float Dt = 0.1f;
    private const double Eps = 1e-9;

    // windupStartTick, contactTick, contactNorm, clipLength, expectedOffset, expectedHold
    [TestCase(10, 13, 0.40f, 1.2f, 0.18f, 0.00f, TestName = "ShortBudget_SkipsAnticipation")]
    [TestCase(10, 15, 0.50f, 1.0f, 0.00f, 0.00f, TestName = "ExactBudget_NoOffsetNoHold")]
    [TestCase(10, 18, 0.40f, 1.2f, 0.00f, 0.32f, TestName = "LongBudget_PreRollHolds")]
    [TestCase(10, 10, 0.40f, 1.2f, 0.48f, 0.00f, TestName = "ZeroBudget_StartsOnContactFrame")]
    public void ContactFrame_LandsOnDamageTick(
        int windupStartTick, int contactTick, float contactNorm, float clipLength, float expectedOffset, float expectedHold)
    {
        var plan = BattleContactPinPlanner.Resolve(contactNorm, clipLength, windupStartTick, contactTick, Dt);

        var tc = (double)contactNorm * clipLength;
        var budget = (contactTick - windupStartTick) * (double)Dt;

        Assert.That(plan.OffsetSeconds, Is.EqualTo(expectedOffset).Within(1e-6), "offset");
        Assert.That(plan.HoldSeconds, Is.EqualTo(expectedHold).Within(1e-6), "hold");
        Assert.That(plan.BudgetSeconds, Is.EqualTo(budget).Within(Eps), "budget");
        Assert.That(plan.ContactClipSeconds, Is.EqualTo(tc).Within(1e-6), "tc");

        // The decisive invariant: the contact frame is sampled exactly at the windup budget.
        Assert.That(plan.ClipLocalTimeAt(plan.BudgetSeconds), Is.EqualTo(plan.ContactClipSeconds).Within(Eps),
            "clipLocal(budget) must equal the authored contact frame tc.");
    }

    [Test]
    public void PreRollHold_DoesNotWarpFollowThrough()
    {
        // Long budget: hold > 0. Frame 0 is held through the hold, then the clip advances at base rate
        // (slope 1 in the scaled clock) before AND after contact — no uniform speed warp (D2-R4).
        var plan = BattleContactPinPlanner.Resolve(0.40f, 1.2f, windupStartTick: 10, contactTick: 18, Dt);
        Assert.That(plan.HoldSeconds, Is.GreaterThan(0d));
        Assert.That(plan.OffsetSeconds, Is.EqualTo(0d).Within(Eps));

        // During the hold: clip pinned to frame 0.
        Assert.That(plan.ClipLocalTimeAt(plan.HoldSeconds * 0.5), Is.EqualTo(0d).Within(Eps));

        // After the hold (pre-contact) and after contact: unit slope.
        AssertUnitSlope(plan, plan.HoldSeconds + 0.05);
        AssertUnitSlope(plan, plan.BudgetSeconds + 0.10);
    }

    private static void AssertUnitSlope(BattleContactPinPlan plan, double around)
    {
        const double h = 1e-3;
        var slope = (plan.ClipLocalTimeAt(around + h) - plan.ClipLocalTimeAt(around - h)) / (2d * h);
        Assert.That(slope, Is.EqualTo(1d).Within(1e-6), $"clip must advance at base rate (no warp) near {around}.");
    }

    [Test]
    public void ElapsedAtStep_IsFixedStepAnchored_NotAccumulated()
    {
        // Anchored evaluation: elapsed depends only on (currentStep - windupStart) + alpha, so it is
        // identical regardless of how many render frames or catch-up batches occurred (Q5 guard B).
        // Tolerance is 1e-6: the real sim's FixedStepSeconds is float 0.1f, so elapsed carries float
        // precision (0.1f -> 0.10000000149d) against the double literals below.
        Assert.That(BattleContactPinPlanner.ElapsedAtStep(10, 10, 0f, Dt), Is.EqualTo(0d).Within(1e-6), "windup start = 0 elapsed.");
        Assert.That(BattleContactPinPlanner.ElapsedAtStep(10, 13, 0f, Dt), Is.EqualTo(0.30d).Within(1e-6), "3 ticks = budget for the short case.");
        Assert.That(BattleContactPinPlanner.ElapsedAtStep(10, 18, 0f, Dt), Is.EqualTo(0.80d).Within(1e-6), "8 ticks = budget for the long case.");
        Assert.That(BattleContactPinPlanner.ElapsedAtStep(10, 10, 0.5f, Dt), Is.EqualTo(0.05d).Within(1e-6), "mid-step alpha contributes a fractional tick.");
    }

    [Test]
    public void ElapsedAtStep_ClampsAlphaAndPreWindup()
    {
        Assert.That(BattleContactPinPlanner.ElapsedAtStep(10, 10, 1.5f, Dt), Is.EqualTo(0.10d).Within(1e-6), "alpha clamps to 1.");
        Assert.That(BattleContactPinPlanner.ElapsedAtStep(10, 8, 0f, Dt), Is.EqualTo(0d).Within(1e-6), "a step before the windup never yields negative elapsed.");
    }

    [Test]
    public void StepAnchoredPin_LandsOnContactFrame_RegardlessOfRenderCadence()
    {
        // GPT Pro FrameRatePermutationContactPin: clip-local time is a pure function of (step, alpha), so
        // whether the driver evaluates every tick boundary or a single catch-up frame jumps straight from
        // the windup step to the contact step, the contact frame is sampled exactly on the damage tick.
        // (A literal accumulating `_oneShotElapsed += dt` would diverge here; the step anchor does not.)
        const int windupStart = 10;
        const int contactTick = 18; // long budget (8 ticks)
        var plan = BattleContactPinPlanner.Resolve(0.40f, 1.2f, windupStart, contactTick, Dt);
        var tc = plan.ContactClipSeconds;

        double fineAtContact = 0;
        for (var step = windupStart; step <= contactTick; step++)
        {
            var e = BattleContactPinPlanner.ElapsedAtStep(windupStart, step, 0f, Dt);
            fineAtContact = plan.ClipLocalTimeAt(e);
        }

        var coarseAtContact = plan.ClipLocalTimeAt(BattleContactPinPlanner.ElapsedAtStep(windupStart, contactTick, 0f, Dt));

        Assert.That(fineAtContact, Is.EqualTo(tc).Within(Eps));
        Assert.That(coarseAtContact, Is.EqualTo(tc).Within(Eps));
        Assert.That(fineAtContact, Is.EqualTo(coarseAtContact).Within(Eps), "render cadence must not change the pinned contact frame.");
    }

    [Test]
    public void ContactTickBeforeWindup_ClampsBudgetToZero()
    {
        // D2-R1: ContactTick >= WindupStartTick. A degenerate inversion yields a zero budget, not a negative one.
        var plan = BattleContactPinPlanner.Resolve(0.40f, 1.2f, windupStartTick: 15, contactTick: 10, Dt);
        Assert.That(plan.BudgetSeconds, Is.EqualTo(0d).Within(Eps));
        Assert.That(plan.ClipLocalTimeAt(0d), Is.EqualTo(plan.ContactClipSeconds).Within(Eps), "zero budget starts on the contact frame.");
    }
}
