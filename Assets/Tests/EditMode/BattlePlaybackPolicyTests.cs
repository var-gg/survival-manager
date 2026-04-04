using NUnit.Framework;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattlePlaybackPolicyTests
{
    [Test]
    public void QuickBattle_All_Controls_Always_Available()
    {
        var policy = new BattlePlaybackPolicy(BattlePlaybackMode.QuickBattle);

        Assert.That(policy.CanPause(isFinished: false), Is.True);
        Assert.That(policy.CanSeek(isFinished: false), Is.True);
        Assert.That(policy.CanControlSpeed(isFinished: false), Is.True);
        Assert.That(policy.CanReplay(isFinished: false), Is.True);
    }

    [Test]
    public void QuickBattle_All_Controls_Available_After_Finished()
    {
        var policy = new BattlePlaybackPolicy(BattlePlaybackMode.QuickBattle);

        Assert.That(policy.CanPause(isFinished: true), Is.True);
        Assert.That(policy.CanSeek(isFinished: true), Is.True);
        Assert.That(policy.CanControlSpeed(isFinished: true), Is.True);
        Assert.That(policy.CanReplay(isFinished: true), Is.True);
    }

    [Test]
    public void InGame_No_Controls_During_Battle()
    {
        var policy = new BattlePlaybackPolicy(BattlePlaybackMode.InGame);

        Assert.That(policy.CanPause(isFinished: false), Is.False);
        Assert.That(policy.CanSeek(isFinished: false), Is.False);
        Assert.That(policy.CanControlSpeed(isFinished: false), Is.False);
        Assert.That(policy.CanReplay(isFinished: false), Is.False);
    }

    [Test]
    public void InGame_All_Controls_After_Finished()
    {
        var policy = new BattlePlaybackPolicy(BattlePlaybackMode.InGame);

        Assert.That(policy.CanPause(isFinished: true), Is.True);
        Assert.That(policy.CanSeek(isFinished: true), Is.True);
        Assert.That(policy.CanControlSpeed(isFinished: true), Is.True);
        Assert.That(policy.CanReplay(isFinished: true), Is.True);
    }

    [Test]
    public void Mode_Property_Returns_Configured_Mode()
    {
        var quick = new BattlePlaybackPolicy(BattlePlaybackMode.QuickBattle);
        var inGame = new BattlePlaybackPolicy(BattlePlaybackMode.InGame);

        Assert.That(quick.Mode, Is.EqualTo(BattlePlaybackMode.QuickBattle));
        Assert.That(inGame.Mode, Is.EqualTo(BattlePlaybackMode.InGame));
    }
}
