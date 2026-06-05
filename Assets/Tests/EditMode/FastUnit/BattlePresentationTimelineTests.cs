using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Stage 7 / I11 (GPT Pro review): the presentation timeline must be frame-rate independent — the same
/// total presentation time yields the same step index and blend alpha regardless of how frame deltas are
/// chunked. This also guards the lazy-stepping save-truth leak path (#1): playback position is a function
/// of elapsed presentation time, not of frame cadence.
/// </summary>
[Category("FastUnit")]
public sealed class BattlePresentationTimelineTests
{
    [TestCase(7)]
    [TestCase(42)]
    public void Timeline_PlaybackPosition_IsFrameRateIndependent(int seed)
    {
        var coarse = BuildTimeline(seed);
        var fine = BuildTimeline(seed);

        // Advance both to ~0.53s of presentation time (mid-tick: step 5, alpha ~0.3) via different frame
        // schedules — 6 coarse frames vs 53 fine frames.
        AdvanceTimes(coarse, 0.1f, 5);
        AdvanceTimes(coarse, 0.03f, 1);
        AdvanceTimes(fine, 0.01f, 53);

        Assert.That(fine.CurrentIndex, Is.EqualTo(coarse.CurrentIndex),
            $"seed={seed}: timeline step index diverged across frame schedules.");

        coarse.TryAdvance(0f, out _, out _, out var coarseAlpha);
        fine.TryAdvance(0f, out _, out _, out var fineAlpha);
        Assert.That(fineAlpha, Is.EqualTo(coarseAlpha).Within(1e-2f),
            $"seed={seed}: blend alpha diverged across frame schedules.");
    }

    private static void AdvanceTimes(BattleTimelineController timeline, float dt, int count)
    {
        for (var i = 0; i < count; i++)
        {
            timeline.TryAdvance(dt, out _, out _, out _);
        }
    }

    private static BattleTimelineController BuildTimeline(int seed)
    {
        var ally = CombatTestFactory.CreateUnit("ally_blade", anchor: DeploymentAnchorId.FrontCenter, hp: 80f, moveSpeed: 2.0f, attackRange: 1.2f, attackWindup: 0.1f, attackCooldown: 0.5f);
        var enemy = CombatTestFactory.CreateUnit("enemy_blade", race: "undead", anchor: DeploymentAnchorId.FrontCenter, hp: 80f, moveSpeed: 1.8f, attackRange: 1.2f, attackWindup: 0.1f, attackCooldown: 0.5f);
        var simulator = new BattleSimulator(CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy }, seed: seed), 300);
        var timeline = new BattleTimelineController();
        timeline.Initialize(simulator, simulator.CurrentStep, 300);
        return timeline;
    }
}
