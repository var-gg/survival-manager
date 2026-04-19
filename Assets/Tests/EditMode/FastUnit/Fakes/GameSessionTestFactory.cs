using SM.Unity;

namespace SM.Tests.EditMode.Fakes;

public static class GameSessionTestFactory
{
    public static GameSessionState Create(ICombatContentLookup? lookup = null)
    {
        return new GameSessionState(
            lookup ?? new FakeCombatContentLookup(),
            NarrativeRuntimeBootstrap.CreateEmpty());
    }
}
