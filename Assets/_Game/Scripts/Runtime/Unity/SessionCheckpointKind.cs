namespace SM.Unity;

public enum SessionCheckpointKind
{
    StartupLoad = 0,
    ManualSave = 1,
    ManualLoad = 2,
    TownExit = 3,
    BattleResolved = 4,
    RewardApplied = 5,
    RewardSettled = 6,
    ReturnToStart = 7,
    QuickBattleBootstrap = 8,
    QuickBattleRestore = 9,
}
