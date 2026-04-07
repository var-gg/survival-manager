using System;

namespace SM.Unity;

public static class SessionRealmAutoStartPolicy
{
    public static bool ShouldForceOfflineLocalForQuickBattle(bool quickBattleRequested)
    {
        // Quick Battle remains a developer-only smoke lane.
        return quickBattleRequested;
    }

    public static bool ShouldForceOfflineLocalForScene(string sceneName)
    {
        // Direct scene play is a debug fallback and bypasses the authored Boot loop.
        return !string.Equals(sceneName, SceneNames.Boot, StringComparison.Ordinal);
    }
}
