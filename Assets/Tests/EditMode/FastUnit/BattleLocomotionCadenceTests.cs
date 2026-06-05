using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 4 (C2 / I3): the locomotion cadence invariant — clip playback speed is proportional to the
/// unit's measured world speed, clamped. Pure math, so it pins the treadmill fix at the code-test
/// level (visual foot lock is the human PlayMode check).
/// </summary>
[Category("FastUnit")]
public sealed class BattleLocomotionCadenceTests
{
    [Test]
    public void WorldSpeed_IsDisplacementOverStep()
    {
        Assert.That(BattleLocomotionCadence.WorldSpeed(0.2f, 0.1f), Is.EqualTo(2f).Within(1e-4f));
    }

    [Test]
    public void WorldSpeed_ZeroStep_ReturnsZero()
    {
        Assert.That(BattleLocomotionCadence.WorldSpeed(0.2f, 0f), Is.EqualTo(0f));
    }

    [Test]
    public void PlaybackSpeed_IsProportionalToWorldSpeed_WithinClamp()
    {
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(1.6f, 1.6f), Is.EqualTo(1f).Within(1e-4f));
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(0.8f, 1.6f), Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(3.2f, 1.6f), Is.EqualTo(2f).Within(1e-4f));
    }

    [Test]
    public void PlaybackSpeed_ClampsToBounds()
    {
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(100f, 1.6f), Is.EqualTo(BattleLocomotionCadence.MaxPlaybackSpeed));
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(0.001f, 1.6f), Is.EqualTo(BattleLocomotionCadence.MinPlaybackSpeed));
    }

    [Test]
    public void PlaybackSpeed_NonPositiveAuthored_DefaultsToOne()
    {
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(2f, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void IsLocomoting_TrueOnlyAboveThreshold()
    {
        Assert.That(BattleLocomotionCadence.IsLocomoting(2f), Is.True);
        Assert.That(BattleLocomotionCadence.IsLocomoting(0.05f), Is.False);
    }

    [TestCase(0.5f)]
    [TestCase(2.0f)]
    [TestCase(5.0f)]
    public void PlaybackSpeed_MatchesClampedRatio_Invariant(float worldSpeed)
    {
        const float authored = 1.6f;
        var expected = System.Math.Clamp(
            worldSpeed / authored,
            BattleLocomotionCadence.MinPlaybackSpeed,
            BattleLocomotionCadence.MaxPlaybackSpeed);
        Assert.That(BattleLocomotionCadence.ResolvePlaybackSpeed(worldSpeed, authored), Is.EqualTo(expected).Within(1e-5f));
    }
}
