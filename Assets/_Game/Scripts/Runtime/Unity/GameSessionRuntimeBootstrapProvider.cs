using System;

namespace SM.Unity;

internal sealed class GameSessionRuntimeBootstrapProvider
{
    internal static readonly GameSessionRuntimeBootstrapProvider Resources = new();

    private GameSessionRuntimeBootstrapProvider()
    {
    }

    internal NarrativeRuntimeBootstrap CreateNarrativeBootstrap()
    {
        return NarrativeRuntimeBootstrap.LoadFromResources()
            ?? throw new InvalidOperationException("Narrative runtime bootstrap provider returned null.");
    }
}
