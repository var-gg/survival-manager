using System;

namespace SM.Unity;

/// <summary>
/// Contact-domain hitstop as an OUTPUT-POSE-ONLY remap (GPT Pro Stage 5, J6/J15/J25). On contact the
/// sampled choreography time is held at the contact frame for a short "punch", then catch-up blended back
/// to the live schedule time. Crucially this is the third of three clocks and the only one that ever
/// pauses output:
/// <list type="bullet">
/// <item><b>SimClock</b> — never touched (gameplay byte-identical, J6);</item>
/// <item><b>ChoreographyClock</b> — keeps advancing; pinned contact / release ticks never move (J15);</item>
/// <item><b>HitstopPoseHold</b> — remaps only the <em>output</em> time fed to the clip sampler (J25).</item>
/// </list>
/// Because <see cref="ResolveOutputTime"/> is a pure function of the (un-frozen) choreography time, hitstop
/// can never feed presentation timing back into the sim or schedule, and is framerate-independent.
/// </summary>
public readonly record struct BattleHitstopWindow(double ContactTime, double HoldSeconds, double CatchUpSeconds)
{
    public static readonly BattleHitstopWindow None = new(double.NegativeInfinity, 0d, 0d);

    public double HoldEnd => ContactTime + Math.Max(0d, HoldSeconds);
    public double CatchUpEnd => HoldEnd + Math.Max(0d, CatchUpSeconds);

    /// <summary>True while this window still alters the output (holding or catching up) at <paramref name="now"/>.</summary>
    public bool IsActiveAt(double now) => now >= ContactTime && now < CatchUpEnd;
}

/// <summary>
/// Pure hitstop resolver. The choreography time <paramref name="now"/> is an INPUT that always advances —
/// hitstop never stops it (J25). The returned output time is held at the contact frame during the hold,
/// then chases the live time back over the catch-up span, rejoining it exactly at <see cref="BattleHitstopWindow.CatchUpEnd"/>.
/// </summary>
public static class BattleHitstop
{
    /// <summary>The choreography time to actually sample, given the live time and an active window.</summary>
    public static double ResolveOutputTime(double now, BattleHitstopWindow window)
    {
        if (window.HoldSeconds <= 0d && window.CatchUpSeconds <= 0d)
        {
            return now;
        }

        if (now < window.ContactTime || now >= window.CatchUpEnd)
        {
            return now; // before contact or after catch-up: fully live
        }

        if (now < window.HoldEnd)
        {
            return window.ContactTime; // hold: frozen on the contact frame
        }

        // Catch-up: blend the frozen contact frame back toward the live time, rejoining it at CatchUpEnd.
        var span = Math.Max(1e-6, window.CatchUpSeconds);
        var progress = (now - window.HoldEnd) / span; // 0 -> 1
        return window.ContactTime + (progress * (now - window.ContactTime));
    }

    /// <summary>
    /// Merge a freshly arriving contact into a (possibly active) window. A new pinned contact always wins:
    /// the old hold is truncated and a new one begins at the new contact time (GPT Pro: "next pinned contact가
    /// hold 안에 들어오면 truncate/merge, contact pin 우선"). Deterministic — newer contact time replaces older.
    /// </summary>
    public static BattleHitstopWindow Merge(BattleHitstopWindow current, BattleHitstopWindow incoming)
    {
        if (incoming.HoldSeconds <= 0d && incoming.CatchUpSeconds <= 0d)
        {
            return current;
        }

        return incoming.ContactTime >= current.ContactTime ? incoming : current;
    }
}

/// <summary>
/// Authored graybox hitstop durations keyed by impact intensity (GPT Pro Stage 5). Heavier contacts hold
/// longer for more "punch". A version travels with the contract so a recording made under an older table is
/// detected rather than silently re-timed (mirrors the clip-timing catalog, J26).
/// </summary>
public static class BattleHitstopCatalog
{
    public const int CatalogVersion = 1;

    public const float DefaultCatchUpSeconds = 0.05f;

    /// <summary>Hold duration (seconds) for the given impact intensity; <see cref="BattleAnimationIntensity.Any"/>
    /// means no contact emphasis and yields no hitstop.</summary>
    public static float HoldSecondsFor(BattleAnimationIntensity intensity)
    {
        return intensity switch
        {
            BattleAnimationIntensity.Light => 0.03f,
            BattleAnimationIntensity.Medium => 0.05f,
            BattleAnimationIntensity.Heavy => 0.07f,
            _ => 0f,
        };
    }

    /// <summary>Build a window from an authored intensity at the given contact time, or
    /// <see cref="BattleHitstopWindow.None"/> when the intensity carries no hitstop.</summary>
    public static BattleHitstopWindow ResolveWindow(double contactTime, BattleAnimationIntensity intensity)
    {
        var hold = HoldSecondsFor(intensity);
        return hold <= 0f ? BattleHitstopWindow.None : new BattleHitstopWindow(contactTime, hold, DefaultCatchUpSeconds);
    }
}
