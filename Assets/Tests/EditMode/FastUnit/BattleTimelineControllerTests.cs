using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattleTimelineControllerTests
{
    private static (BattleSimulator simulator, BattleTimelineController timeline) CreateTimeline()
    {
        var ally = CombatTestFactory.CreateUnit("ally_1", anchor: DeploymentAnchorId.FrontCenter);
        var enemy = CombatTestFactory.CreateUnit("enemy_1", anchor: DeploymentAnchorId.FrontCenter);
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy });
        var sim = new BattleSimulator(state, BattleSimulator.DefaultMaxSteps);
        var timeline = new BattleTimelineController();
        timeline.Initialize(sim, sim.CurrentStep, BattleSimulator.DefaultMaxSteps);
        return (sim, timeline);
    }

    [Test]
    public void Initialize_Sets_CurrentIndex_Zero_And_Not_Finished()
    {
        var (_, timeline) = CreateTimeline();

        Assert.That(timeline.CurrentIndex, Is.EqualTo(0));
        Assert.That(timeline.IsFinished, Is.False);
        Assert.That(timeline.IsPaused, Is.False);
        Assert.That(timeline.CurrentStep, Is.Not.Null);
    }

    [Test]
    public void TryAdvance_Steps_Forward_When_Enough_Time()
    {
        var (_, timeline) = CreateTimeline();
        var fixedStep = timeline.FixedStepSeconds;

        var stepped = timeline.TryAdvance(fixedStep + 0.001f,
            out var prev, out var curr, out var alpha);

        Assert.That(stepped, Is.True);
        Assert.That(timeline.CurrentIndex, Is.EqualTo(1));
        Assert.That(curr, Is.Not.Null);
        Assert.That(curr!.StepIndex, Is.EqualTo(1));
    }

    [Test]
    public void TryAdvance_Does_Not_Step_When_Paused()
    {
        var (_, timeline) = CreateTimeline();
        timeline.TogglePause();

        var stepped = timeline.TryAdvance(1f,
            out _, out _, out _);

        Assert.That(stepped, Is.False);
        Assert.That(timeline.CurrentIndex, Is.EqualTo(0));
    }

    [Test]
    public void TryAdvance_WhenPaused_PreservesCurrentBlendAlpha()
    {
        var (_, timeline) = CreateTimeline();
        var fixedStep = timeline.FixedStepSeconds;

        var advanced = timeline.TryAdvance(
            fixedStep + (fixedStep * 0.4f),
            out _,
            out _,
            out var alphaBeforePause);
        timeline.SetPaused(true);

        var stepped = timeline.TryAdvance(
            fixedStep * 5f,
            out _,
            out _,
            out var pausedAlpha);

        Assert.That(advanced, Is.True);
        Assert.That(timeline.CurrentIndex, Is.EqualTo(1));
        Assert.That(stepped, Is.False);
        Assert.That(pausedAlpha, Is.EqualTo(alphaBeforePause).Within(0.001f));
        Assert.That(pausedAlpha, Is.LessThan(0.995f));
    }

    [Test]
    public void SeekToStep_Forward_Records_Steps()
    {
        var (_, timeline) = CreateTimeline();

        timeline.SeekToStep(10);

        Assert.That(timeline.CurrentIndex, Is.EqualTo(10));
        Assert.That(timeline.FurthestIndex, Is.GreaterThanOrEqualTo(10));
        Assert.That(timeline.CurrentStep!.StepIndex, Is.EqualTo(10));
    }

    [Test]
    public void SeekToStep_Backward_After_Forward()
    {
        var (_, timeline) = CreateTimeline();
        timeline.SeekToStep(20);
        Assert.That(timeline.CurrentIndex, Is.EqualTo(20));

        timeline.SeekToStep(5);

        Assert.That(timeline.CurrentIndex, Is.EqualTo(5));
        Assert.That(timeline.FurthestIndex, Is.GreaterThanOrEqualTo(20));
        Assert.That(timeline.CurrentStep!.StepIndex, Is.EqualTo(5));
    }

    [Test]
    public void SeekToStep_Backward_Clears_IsFinished()
    {
        var (_, timeline) = CreateTimeline();
        timeline.SeekToStep(BattleSimulator.DefaultMaxSteps);
        Assert.That(timeline.IsFinished, Is.True);

        timeline.SeekToStep(5);

        Assert.That(timeline.IsFinished, Is.False);
    }

    [Test]
    public void SetSpeed_Clamps_To_Valid_Range()
    {
        var (_, timeline) = CreateTimeline();

        timeline.SetSpeed(0.1f);
        Assert.That(timeline.PlaybackSpeed, Is.EqualTo(0.25f));

        timeline.SetSpeed(100f);
        Assert.That(timeline.PlaybackSpeed, Is.EqualTo(8f));

        timeline.SetSpeed(4f);
        Assert.That(timeline.PlaybackSpeed, Is.EqualTo(4f));
    }

    [Test]
    public void StepOnce_Advances_By_One()
    {
        var (_, timeline) = CreateTimeline();

        timeline.StepOnce();

        Assert.That(timeline.CurrentIndex, Is.EqualTo(1));
    }

    [Test]
    public void Reset_Clears_State()
    {
        var (sim, timeline) = CreateTimeline();
        timeline.SeekToStep(50);
        timeline.TogglePause();
        timeline.SetSpeed(4f);

        var ally = CombatTestFactory.CreateUnit("ally_new", anchor: DeploymentAnchorId.FrontCenter);
        var enemy = CombatTestFactory.CreateUnit("enemy_new", anchor: DeploymentAnchorId.FrontCenter);
        var newState = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { enemy });
        var newSim = new BattleSimulator(newState, BattleSimulator.DefaultMaxSteps);

        timeline.Reset(newSim, newSim.CurrentStep, BattleSimulator.DefaultMaxSteps);

        Assert.That(timeline.CurrentIndex, Is.EqualTo(0));
        Assert.That(timeline.IsPaused, Is.False);
        Assert.That(timeline.PlaybackSpeed, Is.EqualTo(1f));
        Assert.That(timeline.IsFinished, Is.False);
    }

    [Test]
    public void NormalizedProgress_Reflects_Current_Position()
    {
        var (_, timeline) = CreateTimeline();

        Assert.That(timeline.NormalizedProgress, Is.EqualTo(0f).Within(0.001f));

        timeline.SeekToStep(10);
        var expected = 10f / BattleSimulator.DefaultMaxSteps;
        Assert.That(timeline.NormalizedProgress, Is.EqualTo(expected).Within(0.01f));
    }
}
