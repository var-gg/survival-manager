namespace SM.Unity;

public enum BattlePlaybackMode
{
    QuickBattle,
    InGame,
}

public sealed class BattlePlaybackPolicy
{
    public BattlePlaybackMode Mode { get; }

    public BattlePlaybackPolicy(BattlePlaybackMode mode)
    {
        Mode = mode;
    }

    public bool CanPause(bool isFinished) => Mode == BattlePlaybackMode.QuickBattle || isFinished;

    public bool CanSeek(bool isFinished) => Mode == BattlePlaybackMode.QuickBattle || isFinished;

    public bool CanControlSpeed(bool isFinished) => Mode == BattlePlaybackMode.QuickBattle || isFinished;

    public bool CanReplay(bool isFinished) => Mode == BattlePlaybackMode.QuickBattle || isFinished;
}
