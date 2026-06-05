using System;

namespace SM.Unity;

/// <summary>
/// Presentation-owned crossfade envelope for one one-shot action clip (GPT Pro Stage 4 "blend driver",
/// J5). It describes how the one-shot mixer input's weight ramps in from the loop, holds full weight
/// across the contact window, and ramps back out — replacing the old instant <c>1↔0</c> hard swap that
/// caused the D1 layer popping. Time is the one-shot's local presentation clock (seconds since the
/// one-shot began), so the envelope is a pure function of authored clip timing and is framerate-free.
/// </summary>
/// <remarks>
/// Invariants (GPT Pro J5, validated in the v2/v3 review):
/// <list type="bullet">
/// <item><c>WeightAt(contact) ≥ 1 − ε</c> — the strike lands at full pose weight;</item>
/// <item><c>BlendInEnd ≤ contact − lead</c> — full weight is reached at least <em>lead</em> before contact;</item>
/// <item><c>BlendOutStart ≥ contact + hold</c> — full weight is held at least <em>hold</em> past contact;</item>
/// <item>no instant weight jump except an explicitly authored instant-on at <c>t = 0</c> (hit reactions).</item>
/// </list>
/// A swing clip whose contact budget cannot fit the lead and is <em>not</em> authored instant-on sets
/// <see cref="RequiresExplicitFallback"/> — the caller must supply an explicit zero-budget profile rather
/// than silently degrade (GPT Pro: "zero-budget는 build/test-time failure").
/// </remarks>
public readonly record struct BattleBlendEnvelope(
    float BlendInStartSeconds,
    float BlendInEndSeconds,
    float BlendOutStartSeconds,
    float BlendOutEndSeconds,
    float ContactSeconds,
    bool RequiresExplicitFallback)
{
    /// <summary>Weight is full from the very first sample (authored instant-on; never time-shifted).</summary>
    public static readonly BattleBlendEnvelope InstantFull = new(0f, 0f, float.MaxValue, float.MaxValue, 0f, false);

    /// <summary>The one-shot mixer input weight (0..1) at local presentation time <paramref name="t"/>.</summary>
    public float WeightAt(float t)
    {
        // Authored instant-on: full weight from the first frame. The only sanctioned discontinuity.
        if (BlendInStartSeconds <= 0f && BlendInEndSeconds <= 0f)
        {
            return t < BlendOutStartSeconds ? 1f : RampDown(t);
        }

        if (t <= BlendInStartSeconds)
        {
            return 0f;
        }

        if (t < BlendInEndSeconds)
        {
            return Clamp01((t - BlendInStartSeconds) / (BlendInEndSeconds - BlendInStartSeconds));
        }

        if (t < BlendOutStartSeconds)
        {
            return 1f;
        }

        return RampDown(t);
    }

    private float RampDown(float t)
    {
        if (t < BlendOutStartSeconds)
        {
            return 1f;
        }

        if (t >= BlendOutEndSeconds || BlendOutEndSeconds <= BlendOutStartSeconds)
        {
            return 0f;
        }

        return Clamp01(1f - ((t - BlendOutStartSeconds) / (BlendOutEndSeconds - BlendOutStartSeconds)));
    }

    private static float Clamp01(float value)
    {
        return value < 0f ? 0f : value > 1f ? 1f : value;
    }
}

/// <summary>
/// Pure resolver that turns an authored <see cref="BattleClipTiming"/> (or raw contact/lead/hold values)
/// into a <see cref="BattleBlendEnvelope"/>. No engine or render-frame input — the envelope is identical
/// at any framerate and is the single place the contact-full-weight priority (GPT Pro Stage 4) is encoded.
/// </summary>
public static class BattleOneShotBlendResolver
{
    /// <summary>Default loop→one-shot fade-in window (seconds) before it is shrunk to fit the contact budget.</summary>
    public const float DefaultBlendInSeconds = 0.08f;

    /// <summary>Default one-shot→loop fade-out window (seconds) at the tail of the clip.</summary>
    public const float DefaultBlendOutSeconds = 0.10f;

    private const float Epsilon = 1e-4f;

    /// <summary>Resolve directly from authored clip timing, deriving the contact instant from
    /// <see cref="BattleClipTiming.ContactNorm"/> in the clip's wall-clock duration.</summary>
    public static BattleBlendEnvelope Resolve(
        float clipLengthSeconds,
        float playbackSpeed,
        BattleClipTiming timing,
        float configuredBlendInSeconds = DefaultBlendInSeconds,
        float configuredBlendOutSeconds = DefaultBlendOutSeconds)
    {
        var speed = playbackSpeed <= Epsilon ? 1f : playbackSpeed;
        var duration = Math.Max(0f, clipLengthSeconds) / speed;
        var contactNorm = timing.ContactNorm < 0f ? 0f : (timing.ContactNorm > 1f ? 1f : timing.ContactNorm);
        var contactSeconds = contactNorm * duration;

        // A non-pinnable clip (locomotion loop, held pose) has no contact to protect: a plain symmetric
        // crossfade, never a fallback.
        if (!timing.CanContactPin)
        {
            return ResolveSymmetric(duration, configuredBlendInSeconds, configuredBlendOutSeconds);
        }

        // Instant-on is authored when the contact sits on frame 0 (hit reactions / death react).
        var authoredInstantOn = contactNorm <= Epsilon;
        return Resolve(
            contactSeconds,
            duration,
            timing.RequiredFullWeightLeadSeconds,
            timing.RequiredFullWeightHoldSeconds,
            authoredInstantOn,
            configuredBlendInSeconds,
            configuredBlendOutSeconds);
    }

    /// <summary>Resolve from explicit timing values. <paramref name="contactSeconds"/> is the contact
    /// instant in the one-shot's local clock; <paramref name="authoredInstantOn"/> marks a clip whose
    /// pose is meant to read at full weight from <c>t = 0</c>.</summary>
    public static BattleBlendEnvelope Resolve(
        float contactSeconds,
        float oneShotDurationSeconds,
        float fullWeightLeadSeconds,
        float fullWeightHoldSeconds,
        bool authoredInstantOn,
        float configuredBlendInSeconds = DefaultBlendInSeconds,
        float configuredBlendOutSeconds = DefaultBlendOutSeconds)
    {
        var duration = Math.Max(0f, oneShotDurationSeconds);
        var contact = Math.Clamp(contactSeconds, 0f, duration);
        var lead = Math.Max(0f, fullWeightLeadSeconds);
        var hold = Math.Max(0f, fullWeightHoldSeconds);
        var blendInCfg = Math.Max(0f, configuredBlendInSeconds);
        var blendOutCfg = Math.Max(0f, configuredBlendOutSeconds);

        var plateauStart = contact - lead;        // weight must reach 1 here (blend-in end)
        var plateauEnd = Math.Min(duration, contact + hold);  // weight must stay 1 until here

        if (plateauStart <= Epsilon)
        {
            // No room to reach full weight `lead` before contact.
            if (authoredInstantOn)
            {
                var outStart = Math.Clamp(Math.Max(plateauEnd, duration - blendOutCfg), plateauEnd, duration);
                return new BattleBlendEnvelope(0f, 0f, outStart, duration, contact, false);
            }

            // A swing clip with a sub-lead budget that is NOT authored instant-on: the caller must
            // resolve an explicit zero-budget profile, not silently snap (GPT Pro J5 fallback rule).
            return new BattleBlendEnvelope(0f, 0f, plateauEnd, duration, contact, true);
        }

        // Normal case: fade in promptly from t=0 (so the pose reads at once), fully blended by `lead`
        // before contact at the latest, hold full weight across the contact window, then fade out at the
        // tail. blendInEnd <= contact - lead keeps the J5 contact-full-weight invariant; anchoring the
        // start at 0 (rather than late at plateauStart) avoids an idle-dominant intro and works whether
        // the strike is fired at the contact step or contact-pinned into the windup.
        var blendInEnd = Math.Min(blendInCfg, plateauStart);
        var blendOutStart = Math.Clamp(Math.Max(plateauEnd, duration - blendOutCfg), plateauEnd, duration);

        return new BattleBlendEnvelope(0f, blendInEnd, blendOutStart, duration, contact, false);
    }

    private static BattleBlendEnvelope ResolveSymmetric(float duration, float blendInCfg, float blendOutCfg)
    {
        var half = duration * 0.5f;
        var blendIn = Math.Clamp(Math.Max(0f, blendInCfg), 0f, half);
        var blendOutStart = Math.Clamp(duration - Math.Max(0f, blendOutCfg), blendIn, duration);
        return new BattleBlendEnvelope(0f, blendIn, blendOutStart, duration, 0f, false);
    }
}
