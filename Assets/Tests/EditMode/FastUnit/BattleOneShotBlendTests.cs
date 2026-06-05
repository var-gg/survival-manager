using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 4 (GPT Pro "blend driver", J5). Locks the three agreed done-criteria for the one-shot crossfade
/// envelope that replaces the old instant <c>1↔0</c> weight swap (D1 popping):
/// <list type="number">
/// <item><b>BlendContinuity</b> — the mixer weight never jumps (except an authored instant-on at t=0);</item>
/// <item><b>ContactFullWeightDuringBlend</b> — weight is full across <c>[contact−lead, contact+hold]</c>;</item>
/// <item><b>ZeroBudgetFallbackRequiresExplicitProfile</b> — a sub-lead swing that is not authored
/// instant-on flags <see cref="BattleBlendEnvelope.RequiresExplicitFallback"/> instead of silently snapping.</item>
/// </list>
/// </summary>
[Category("FastUnit")]
public sealed class BattleOneShotBlendTests
{
    // ---- BlendContinuity (J5) -------------------------------------------------------------------

    [Test]
    public void Strike_WeightCurve_IsContinuous_NoInstantJump()
    {
        var timing = BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.None); // melee strike, contact 0.40
        var envelope = BattleOneShotBlendResolver.Resolve(clipLengthSeconds: 1.2f, playbackSpeed: 1f, timing);

        var blendInWindow = envelope.BlendInEndSeconds - envelope.BlendInStartSeconds;
        var blendOutWindow = envelope.BlendOutEndSeconds - envelope.BlendOutStartSeconds;
        var maxRate = 1f / System.Math.Min(blendInWindow, blendOutWindow);

        const float dt = 0.001f;
        var previous = envelope.WeightAt(0f);
        Assert.That(previous, Is.EqualTo(0f).Within(1e-4f), "a non-instant clip must start blended out.");
        for (var t = dt; t <= 1.2f + 1e-6f; t += dt)
        {
            var current = envelope.WeightAt(t);
            Assert.That(current, Is.InRange(-1e-4f, 1f + 1e-4f), $"weight out of [0,1] at t={t}.");
            var delta = System.Math.Abs(current - previous);
            Assert.That(delta, Is.LessThanOrEqualTo((maxRate * dt) + 1e-4f),
                $"weight step {delta} at t={t} exceeds the ramp rate (instant jump / pose pop).");
            previous = current;
        }

        Assert.That(envelope.WeightAt(1.2f), Is.EqualTo(0f).Within(1e-4f), "the clip must blend back to the loop at its end.");
        Assert.That(envelope.RequiresExplicitFallback, Is.False);
    }

    // ---- ContactFullWeightDuringBlend ----------------------------------------------------------

    [Test]
    public void Strike_FullWeight_AcrossContactPlateau()
    {
        var timing = BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.None);
        const float clipLen = 1.2f;
        var envelope = BattleOneShotBlendResolver.Resolve(clipLen, playbackSpeed: 1f, timing);

        var contact = timing.ContactNorm * clipLen;
        var lead = timing.RequiredFullWeightLeadSeconds;
        var hold = timing.RequiredFullWeightHoldSeconds;

        Assert.That(envelope.WeightAt(contact), Is.GreaterThanOrEqualTo(1f - 1e-4f),
            "the strike must land at full one-shot weight (J5 oneShotWeight(contact) >= 1-eps).");

        // Full weight is held across the entire protected window.
        for (var t = contact - lead; t <= contact + hold + 1e-6f; t += 0.002f)
        {
            Assert.That(envelope.WeightAt(t), Is.GreaterThanOrEqualTo(1f - 1e-4f), $"weight dipped below full at t={t}.");
        }

        Assert.That(envelope.BlendInEndSeconds, Is.LessThanOrEqualTo((contact - lead) + 1e-4f),
            "blend-in must complete at least `lead` before contact (J5).");
        Assert.That(envelope.BlendOutStartSeconds, Is.GreaterThanOrEqualTo((contact + hold) - 1e-4f),
            "blend-out must not start until at least `hold` after contact (J5).");
    }

    [Test]
    public void TightButValidBudget_ShrinksBlendIn_StillReachesFullWeightAtContact()
    {
        // contact 0.10s, lead 0.02s -> plateauStart 0.08s < default blend-in 0.08s boundary: blend-in
        // compresses into [0, 0.08] and still hits full weight before contact (no fallback).
        var envelope = BattleOneShotBlendResolver.Resolve(
            contactSeconds: 0.10f,
            oneShotDurationSeconds: 0.5f,
            fullWeightLeadSeconds: 0.02f,
            fullWeightHoldSeconds: 0.04f,
            authoredInstantOn: false);

        Assert.That(envelope.RequiresExplicitFallback, Is.False);
        Assert.That(envelope.BlendInStartSeconds, Is.GreaterThanOrEqualTo(0f));
        Assert.That(envelope.BlendInEndSeconds, Is.LessThanOrEqualTo(0.08f + 1e-4f));
        Assert.That(envelope.WeightAt(0.10f), Is.GreaterThanOrEqualTo(1f - 1e-4f));
    }

    // ---- ZeroBudgetFallbackRequiresExplicitProfile ---------------------------------------------

    [Test]
    public void SubLeadSwing_NotInstantOn_RequiresExplicitFallback()
    {
        // contact 0.01s sits inside the 0.02s lead: a swing clip cannot reach full weight in time and is
        // NOT authored instant-on -> the resolver flags it for an explicit fallback profile (no silent snap).
        var envelope = BattleOneShotBlendResolver.Resolve(
            contactSeconds: 0.01f,
            oneShotDurationSeconds: 0.5f,
            fullWeightLeadSeconds: 0.02f,
            fullWeightHoldSeconds: 0.04f,
            authoredInstantOn: false);

        Assert.That(envelope.RequiresExplicitFallback, Is.True);
    }

    [Test]
    public void AuthoredInstantOn_AtZeroContact_IsAllowed_FullWeightFromStart()
    {
        var envelope = BattleOneShotBlendResolver.Resolve(
            contactSeconds: 0f,
            oneShotDurationSeconds: 0.5f,
            fullWeightLeadSeconds: 0.02f,
            fullWeightHoldSeconds: 0.04f,
            authoredInstantOn: true);

        Assert.That(envelope.RequiresExplicitFallback, Is.False);
        Assert.That(envelope.WeightAt(0f), Is.EqualTo(1f).Within(1e-4f), "an authored instant-on reads full from frame 0.");
    }

    [Test]
    public void CatalogHitReaction_IsInstantOn_NeverFallback()
    {
        foreach (var semantic in new[]
                 {
                     BattleAnimationSemantic.HitLight,
                     BattleAnimationSemantic.HitHeavy,
                     BattleAnimationSemantic.CriticalImpact,
                     BattleAnimationSemantic.BlockImpact,
                     BattleAnimationSemantic.Knockdown,
                 })
        {
            var timing = BattleClipTimingCatalog.Resolve(semantic);
            var envelope = BattleOneShotBlendResolver.Resolve(clipLengthSeconds: 0.5f, playbackSpeed: 1f, timing);
            Assert.That(envelope.RequiresExplicitFallback, Is.False, $"{semantic} is authored instant-on, never a fallback.");
            Assert.That(envelope.WeightAt(0f), Is.EqualTo(1f).Within(1e-4f), $"{semantic} must read full from frame 0.");
        }
    }

    [Test]
    public void NoCatalogSemantic_EverRequiresFallback()
    {
        // The graybox catalog is well-authored: every semantic blends without an explicit fallback at the
        // common clip lengths. The fallback path exists only for pathological authoring, asserted above.
        foreach (BattleAnimationSemantic semantic in System.Enum.GetValues(typeof(BattleAnimationSemantic)))
        {
            var timing = BattleClipTimingCatalog.Resolve(semantic);
            var envelope = BattleOneShotBlendResolver.Resolve(clipLengthSeconds: 0.9f, playbackSpeed: 1f, timing);
            Assert.That(envelope.RequiresExplicitFallback, Is.False, $"{semantic} should not need a fallback at a normal clip length.");
        }
    }

    // ---- Non-pinnable (locomotion loop / held pose) --------------------------------------------

    [Test]
    public void NonPinnable_UsesSymmetricCrossfade_NoContactPlateau()
    {
        var timing = BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.BackstepDisengage); // NonPinnable
        var envelope = BattleOneShotBlendResolver.Resolve(clipLengthSeconds: 1.0f, playbackSpeed: 1f, timing);

        Assert.That(envelope.RequiresExplicitFallback, Is.False);
        Assert.That(envelope.WeightAt(0f), Is.EqualTo(0f).Within(1e-4f), "fades in from the loop.");
        Assert.That(envelope.WeightAt(0.5f), Is.GreaterThanOrEqualTo(1f - 1e-4f), "reaches full weight mid-clip.");
        Assert.That(envelope.WeightAt(1.0f), Is.EqualTo(0f).Within(1e-4f), "fades back to the loop at the end.");
    }

    [Test]
    public void Speed_ScalesContactInWallClock()
    {
        var timing = BattleClipTimingCatalog.Resolve(BattleAnimationSemantic.None); // contact 0.40
        var slow = BattleOneShotBlendResolver.Resolve(clipLengthSeconds: 1.2f, playbackSpeed: 1f, timing);
        var fast = BattleOneShotBlendResolver.Resolve(clipLengthSeconds: 1.2f, playbackSpeed: 2f, timing);

        // Playing twice as fast halves the wall-clock contact instant.
        Assert.That(fast.ContactSeconds, Is.EqualTo(slow.ContactSeconds * 0.5f).Within(1e-4f));
        Assert.That(fast.WeightAt(fast.ContactSeconds), Is.GreaterThanOrEqualTo(1f - 1e-4f));
    }
}
