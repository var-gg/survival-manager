using UnityEngine;

namespace SM.Unity;

/// <summary>
/// Pure locomotion cadence math (GPT Pro review C2 / I3). The locomotion clip's playback speed must be
/// proportional to the unit's actual world speed, so foot cadence tracks displacement instead of
/// playing at a fixed 1x while the body moves at MoveSpeed — the "treadmill"/foot-slide. This is an
/// honest code-level invariant (playbackSpeed == clamp(worldSpeed / authoredSpeed)), not a claim of
/// pixel-accurate foot lock, which a graybox in-place clip cannot physically provide.
/// </summary>
public static class BattleLocomotionCadence
{
    public const float MinPlaybackSpeed = 0.35f;
    public const float MaxPlaybackSpeed = 2.5f;
    public const float LocomotionSpeedThreshold = 0.15f;

    /// <summary>World units per second covered this tick (tick displacement over the fixed step).</summary>
    public static float WorldSpeed(float tickDisplacement, float stepSeconds)
    {
        if (stepSeconds <= 0.0001f)
        {
            return 0f;
        }

        return Mathf.Abs(tickDisplacement) / stepSeconds;
    }

    public static float ResolvePlaybackSpeed(float worldSpeed, float authoredLocomotionSpeed)
    {
        if (authoredLocomotionSpeed <= 0.0001f)
        {
            return 1f;
        }

        return Mathf.Clamp(worldSpeed / authoredLocomotionSpeed, MinPlaybackSpeed, MaxPlaybackSpeed);
    }

    public static bool IsLocomoting(float worldSpeed)
    {
        return worldSpeed > LocomotionSpeedThreshold;
    }
}
