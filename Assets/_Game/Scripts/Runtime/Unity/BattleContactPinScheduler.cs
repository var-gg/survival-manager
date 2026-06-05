using System;

namespace SM.Unity;

/// <summary>
/// Presentation-owned choreography schedule for one action instance, derived from the sim's canonical
/// <see cref="ContactTick"/> plus authored clip timing (GPT Pro Closure 1 / J21). These ticks/seconds
/// live in SM.Unity, never in SM.Combat — the sim only owns ContactTick.
/// </summary>
public readonly record struct BattleChoreographySchedule(
    int PresentationReleaseTick,
    int ContactTick,
    int PresentationRecoveryEndTick,
    double ClipStartSeconds,
    float PlaybackSpeed,
    int CatalogVersion);

/// <summary>
/// Pure, deterministic contact-pin math (GPT Pro C2/C3, J3/J12/J16). Given the sim's canonical
/// <c>ContactTick</c> and an authored <see cref="BattleClipTiming"/>, it computes the clip start offset
/// that lands the clip's contact frame on the exact damage tick, and the presentation-owned release /
/// recovery ticks. Everything is expressed in tick- and second-space (never render-frame space), so the
/// schedule is identical at any framerate — only the number of rendered samples differs (J16).
/// </summary>
public static class BattleContactPinScheduler
{
    /// <summary>
    /// Clip start time (seconds, relative to the choreography clock) such that the clip's contact frame
    /// (at <paramref name="contactNorm"/> of its length) is sampled exactly at
    /// <c>contactTick * fixedStepSeconds</c>. May be negative when the contact frame sits past the
    /// windup budget — the caller then falls back per C3.
    /// </summary>
    public static double ResolveClipStartSeconds(int contactTick, float contactNorm, float clipLength, float playbackSpeed, float fixedStepSeconds)
    {
        var contactSeconds = contactTick * (double)fixedStepSeconds;
        var clipContactSeconds = ClipMarkerSeconds(contactNorm, clipLength, playbackSpeed);
        return contactSeconds - clipContactSeconds;
    }

    /// <summary>The time the contact frame is actually sampled given a clip start — the inverse of
    /// <see cref="ResolveClipStartSeconds"/>. Used by the J3 pin invariant test.</summary>
    public static double ResolveContactSeconds(double clipStartSeconds, float contactNorm, float clipLength, float playbackSpeed)
    {
        return clipStartSeconds + ClipMarkerSeconds(contactNorm, clipLength, playbackSpeed);
    }

    /// <summary>
    /// Tick at which the actor's visual release happens. For melee (<paramref name="authoredTravelTicks"/>
    /// = 0) this equals the contact tick. For projectiles the release is pulled earlier so the projectile
    /// can travel and arrive at the contact tick, but never earlier than the windup begins (the travel is
    /// clamped to the windup budget — GPT Pro J12 / A3 adaptation).
    /// </summary>
    public static int ResolvePresentationReleaseTick(int windupStartTick, int contactTick, int authoredTravelTicks)
    {
        var windupBudget = Math.Max(0, contactTick - windupStartTick);
        var travel = Math.Clamp(authoredTravelTicks, 0, windupBudget);
        return contactTick - travel;
    }

    /// <summary>Tick at which the clip's recovery tail ends (presentation-owned; truncatable by a later
    /// action/death/cancel — GPT Pro J2b). Always &gt;= the contact tick.</summary>
    public static int ResolvePresentationRecoveryEndTick(int contactTick, float recoveryNorm, float contactNorm, float clipLength, float playbackSpeed, float fixedStepSeconds)
    {
        var recoverySpanSeconds = ClipMarkerSeconds(Math.Max(0f, recoveryNorm - contactNorm), clipLength, playbackSpeed);
        var step = Math.Max(0.0001f, fixedStepSeconds);
        var spanTicks = (int)Math.Ceiling(recoverySpanSeconds / step);
        return contactTick + Math.Max(0, spanTicks);
    }

    /// <summary>Bundles the full presentation schedule for one action instance.</summary>
    public static BattleChoreographySchedule ResolveSchedule(
        int windupStartTick,
        int contactTick,
        BattleClipTiming timing,
        float clipLength,
        float playbackSpeed,
        int authoredTravelTicks,
        float fixedStepSeconds)
    {
        var releaseTick = ResolvePresentationReleaseTick(windupStartTick, contactTick, authoredTravelTicks);
        var clipStart = ResolveClipStartSeconds(contactTick, timing.ContactNorm, clipLength, playbackSpeed, fixedStepSeconds);
        var recoveryEnd = ResolvePresentationRecoveryEndTick(contactTick, timing.RecoveryNorm, timing.ContactNorm, clipLength, playbackSpeed, fixedStepSeconds);
        return new BattleChoreographySchedule(releaseTick, contactTick, recoveryEnd, clipStart, playbackSpeed, BattleClipTimingCatalog.CatalogVersion);
    }

    private static double ClipMarkerSeconds(float norm, float clipLength, float playbackSpeed)
    {
        var speed = playbackSpeed <= 0.0001f ? 1f : playbackSpeed;
        return (double)norm * clipLength / speed;
    }
}
