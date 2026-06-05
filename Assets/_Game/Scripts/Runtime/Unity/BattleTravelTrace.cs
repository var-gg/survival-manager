using UnityEngine;

namespace SM.Unity;

/// <summary>
/// Pure travel-trace timing math (GPT Pro review C3, strict tick-sync mode). The original trace ran a
/// presentation-owned 0.16–0.34s transit that outlived the 0.1s sim tick, so when the timeline advanced
/// to the next snapshot pair the trace was cut off and the actor popped. Strict tick-sync caps transform
/// travel to the sim fixed step, so the visual reaches sim truth exactly at the next tick boundary
/// (presented == truth, no cut-off pop). The longer dash "feel" is delegated to VFX / anticipation /
/// follow-through, never to transform travel.
/// </summary>
public static class BattleTravelTrace
{
    public const float DistanceThreshold = 0.35f;
    public const float MinDuration = 0.16f;
    public const float MaxDuration = 0.34f;

    /// <summary>Sim fixed step. Transform travel is capped to this so it never spans more than one tick.</summary>
    public const float StrictMaxSeconds = 0.1f;

    public static float ResolveDuration(float distance, BattleAnimationSemantic semantic)
    {
        var baseDuration = semantic switch
        {
            BattleAnimationSemantic.DashEngage => 0.22f,
            BattleAnimationSemantic.BackstepDisengage => 0.20f,
            BattleAnimationSemantic.LateralStrafe => 0.18f,
            _ => MinDuration,
        };
        var distanceBonus = Mathf.Clamp(distance - DistanceThreshold, 0f, 1.4f) * 0.06f;
        var requested = Mathf.Clamp(baseDuration + distanceBonus, MinDuration, MaxDuration);
        return Mathf.Min(requested, StrictMaxSeconds);
    }

    public static float ResolveAlpha(float timelineAlpha, float traceTimer, float traceDuration)
    {
        var clampedTimelineAlpha = Mathf.Clamp01(timelineAlpha);
        if (traceTimer <= 0f || traceDuration <= 0f)
        {
            return clampedTimelineAlpha;
        }

        var traceProgress = 1f - Mathf.Clamp01(traceTimer / traceDuration);
        var traceAlpha = Mathf.SmoothStep(0f, 1f, traceProgress);
        // Never overshoot the timeline: the trace can lag the tick alpha but never lead it, so the
        // presented position is bounded by the truth interpolation and converges to truth at the boundary.
        return Mathf.Min(clampedTimelineAlpha, traceAlpha);
    }
}
