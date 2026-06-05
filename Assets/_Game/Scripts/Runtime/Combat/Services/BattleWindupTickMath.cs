using System;

namespace SM.Combat.Services;

/// <summary>
/// Canonical windup -&gt; contact tick math (GPT Pro J20 / Closure 4). Mirrors the exact float decrement
/// of <c>UnitSnapshot.AdvanceTime</c> (<c>remaining = Max(0, remaining - dt)</c>) plus the resolve gate
/// (<c>remaining &gt; 0</c>), so the integer <c>ContactTick</c> computed once at BeginWindup equals the
/// step the float-timer resolve actually fires. This keeps gameplay byte-identical (Stage 1 only
/// observes; it does not change the resolve trigger) while giving presentation a canonical contact tick
/// to pin the visual contact frame to.
/// </summary>
public static class BattleWindupTickMath
{
    /// <summary>
    /// Number of steps from the windup-begin step to the resolve step (the step where the float timer
    /// reaches zero and the action resolves), given the windup duration in seconds. Always &gt;= 1: the
    /// begin step's AdvanceTime runs <em>before</em> BeginWindup, so the earliest possible resolve is
    /// the next step. The do/while mirrors the sim exactly, so
    /// <c>WindupStartTick + StepsUntilResolve(windup, dt) == actual resolve StepIndex</c>.
    /// </summary>
    public static int StepsUntilResolve(float windupSeconds, float fixedStepSeconds)
    {
        if (fixedStepSeconds <= 0f)
        {
            return 1;
        }

        var remaining = windupSeconds;
        var steps = 0;
        do
        {
            remaining = Math.Max(0f, remaining - fixedStepSeconds);
            steps++;
        }
        while (remaining > 0f);

        return steps;
    }
}
