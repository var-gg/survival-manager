using SM.Unity;

namespace SM.Tests.EditMode.Fakes;

internal static class GameSessionTestFactory
{
    internal static GameSessionState Create(ICombatContentLookup? lookup = null)
    {
        return new GameSessionState(
            lookup ?? new FakeCombatContentLookup(),
            NarrativeRuntimeBootstrap.CreateEmpty());
    }
}
