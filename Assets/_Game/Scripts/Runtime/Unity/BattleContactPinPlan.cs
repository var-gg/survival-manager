using System;

namespace SM.Unity;

/// <summary>
/// Presentation-owned plan that pins one commit (strike) clip's contact frame onto the sim's canonical
/// damage tick (GPT Pro D2 firing review, RECONCILIATION_CHOICE = a: offset + pre-roll hold). The strike
/// fires at the action's <c>WindupStartTick</c> and must reach its authored contact frame exactly when the
/// <c>ContactTick</c> arrives — never warping the clip's authored speed (D2-R4), so follow-through,
/// recovery, and the Stage 4 blend envelope keep their authored timing.
/// </summary>
/// <remarks>
/// All quantities live in the shared scaled clock where one sim-tick advances <c>FixedStepSeconds</c> in
/// BOTH the timeline's step dispatch and the clip's playback (so PlaybackSpeed cancels and the plan is a
/// pure function of integer ticks + authored clip timing — framerate/speed independent, GPT Pro Q5).
/// <para>
/// <c>budget = (ContactTick − WindupStartTick) · dt</c>, <c>tc = ContactNorm · clipLength</c>:
/// <list type="bullet">
/// <item><c>OffsetSeconds = max(0, tc − budget)</c> — skip this much anticipation when the windup is
/// shorter than the clip's pre-contact span;</item>
/// <item><c>HoldSeconds = max(0, budget − tc)</c> — hold clip frame 0 this long when the windup is longer;</item>
/// <item><c>ClipLocalTimeAt(budget) ≡ tc</c> for every case (short / exact / long / zero budget).</item>
/// </list>
/// Offset and hold are presentation-derived caches, never sim or save truth (D2-R3).
/// </para>
/// </remarks>
public readonly record struct BattleContactPinPlan(
    double OffsetSeconds,
    double HoldSeconds,
    double BudgetSeconds,
    double ContactClipSeconds)
{
    /// <summary>An un-pinned plan (no windup budget): play the clip from frame 0 with no hold.</summary>
    public static readonly BattleContactPinPlan None = new(0d, 0d, 0d, 0d);

    /// <summary>Clip-local time to sample at one-shot-local elapsed <paramref name="elapsedSeconds"/>:
    /// hold frame 0 for <see cref="HoldSeconds"/>, then advance at base rate from <see cref="OffsetSeconds"/>.</summary>
    public double ClipLocalTimeAt(double elapsedSeconds)
    {
        return OffsetSeconds + Math.Max(0d, elapsedSeconds - HoldSeconds);
    }
}

/// <summary>
/// Pure planner for <see cref="BattleContactPinPlan"/> (GPT Pro D2, choice a). Deterministic in tick +
/// authored-clip space; never reads render framerate, and the contact-pin evaluation is anchored to the
/// fixed-step clock (<see cref="ElapsedAtStep"/>) rather than a free-running accumulator — so a single
/// render frame whose catch-up loop consumes both the windup and the contact step still lands the contact
/// frame exactly on the damage tick (GPT Pro Q5 guard B).
/// </summary>
public static class BattleContactPinPlanner
{
    /// <summary>Build the pin plan from the sim's windup/contact ticks and the authored clip timing.</summary>
    public static BattleContactPinPlan Resolve(
        float contactNorm,
        float clipLength,
        int windupStartTick,
        int contactTick,
        float fixedStepSeconds)
    {
        var dt = Math.Max(0d, fixedStepSeconds);
        var tickSpan = Math.Max(0, contactTick - windupStartTick); // D2-R1: ContactTick >= WindupStartTick
        var budget = tickSpan * dt;                                // D2-R2: integer tick diff * dt only
        var norm = contactNorm < 0f ? 0f : (contactNorm > 1f ? 1f : contactNorm);
        var contactClip = norm * Math.Max(0f, clipLength);
        var hold = Math.Max(0d, budget - contactClip);
        var offset = Math.Max(0d, contactClip - budget);
        return new BattleContactPinPlan(offset, hold, budget, contactClip);
    }

    /// <summary>
    /// Fixed-step-anchored one-shot-local elapsed at the given timeline position (GPT Pro Q5 guard B):
    /// <c>((currentStepTick − windupStartTick) + alpha) · dt</c>. Evaluating from the absolute step index +
    /// intra-step alpha (instead of accumulating render deltas) keeps the pin correct across any framerate,
    /// catch-up batching, pause/resume, and save/seek reconstruction.
    /// </summary>
    public static double ElapsedAtStep(int windupStartTick, int currentStepTick, float alpha, float fixedStepSeconds)
    {
        var dt = Math.Max(0d, fixedStepSeconds);
        var clampedAlpha = alpha < 0f ? 0d : (alpha > 1f ? 1d : alpha);
        var steps = (currentStepTick - windupStartTick) + clampedAlpha;
        return Math.Max(0d, steps) * dt;
    }
}
