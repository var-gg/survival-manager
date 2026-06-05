using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 5 (C3 strict tick-sync / I2 / I4): transform travel never exceeds the sim fixed step, and the
/// trace alpha converges to 1 (presented == truth) at the tick boundary with no overshoot. Pure math,
/// so the popping/teleport fix is pinned at the code-test level; the visual feel is the human PlayMode check.
/// </summary>
[Category("FastUnit")]
public sealed class BattleTravelTraceTests
{
    [TestCase(BattleAnimationSemantic.DashEngage)]
    [TestCase(BattleAnimationSemantic.BackstepDisengage)]
    [TestCase(BattleAnimationSemantic.LateralStrafe)]
    [TestCase(BattleAnimationSemantic.None)]
    public void ResolveDuration_NeverExceedsFixedStep_AcrossDistances(BattleAnimationSemantic semantic)
    {
        foreach (var distance in new[] { 0.0f, 0.4f, 1.0f, 3.0f, 10.0f })
        {
            Assert.That(
                BattleTravelTrace.ResolveDuration(distance, semantic),
                Is.LessThanOrEqualTo(BattleTravelTrace.StrictMaxSeconds + 1e-5f),
                $"semantic={semantic} distance={distance}: transform travel must not outlive one sim tick.");
        }
    }

    [Test]
    public void ResolveAlpha_ReachesOne_AtTickBoundary_WhenTimerDrained()
    {
        // Tick boundary: timelineAlpha == 1 and the trace timer has drained -> presented == truth (alpha 1).
        Assert.That(BattleTravelTrace.ResolveAlpha(1f, 0f, 0.1f), Is.EqualTo(1f));
    }

    [Test]
    public void ResolveAlpha_NeverOvershootsTimelineAlpha()
    {
        // The trace may lag the tick alpha but must never lead it, so the presented position stays
        // bounded by the truth interpolation (no leading pop).
        foreach (var timelineAlpha in new[] { 0.1f, 0.4f, 0.7f, 1.0f })
        {
            foreach (var timer in new[] { 0.1f, 0.05f, 0.01f, 0f })
            {
                var alpha = BattleTravelTrace.ResolveAlpha(timelineAlpha, timer, 0.1f);
                Assert.That(alpha, Is.LessThanOrEqualTo(timelineAlpha + 1e-5f),
                    $"timelineAlpha={timelineAlpha} timer={timer}: trace alpha overshoot the timeline.");
                Assert.That(alpha, Is.GreaterThanOrEqualTo(0f));
            }
        }
    }

    [Test]
    public void ResolveAlpha_IsMonotonic_AsTimerDrains()
    {
        // As the trace timer drains (progress increases) at a fixed timeline alpha of 1, alpha is non-decreasing.
        var previous = -1f;
        foreach (var timer in new[] { 0.1f, 0.08f, 0.05f, 0.02f, 0f })
        {
            var alpha = BattleTravelTrace.ResolveAlpha(1f, timer, 0.1f);
            Assert.That(alpha, Is.GreaterThanOrEqualTo(previous - 1e-5f));
            previous = alpha;
        }
    }
}
