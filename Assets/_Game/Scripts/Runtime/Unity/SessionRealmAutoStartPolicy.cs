using System;

namespace SM.Unity;

public static class SessionRealmAutoStartPolicy
{
    public static bool ShouldForceOfflineLocalForQuickBattle(bool quickBattleRequested)
    {
        return quickBattleRequested;
    }

    public static bool ShouldForceOfflineLocalForScene(string sceneName)
    {
        return !string.Equals(sceneName, SceneNames.Boot, StringComparison.Ordinal);
    }
}
