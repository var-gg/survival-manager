using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed class BuildSpaceCensus
{
    public BuildSpaceCensus(
        IReadOnlyList<BuildCombination> combinations,
        IReadOnlyList<FormationPlacement> formations,
        IReadOnlyList<FormationMedoid> medoids,
        BuildSpaceSummary summary)
    {
        Combinations = combinations;
        Formations = formations;
        Medoids = medoids;
        Summary = summary;
    }

    public IReadOnlyList<BuildCombination> Combinations { get; }

    public IReadOnlyList<FormationPlacement> Formations { get; }

    public IReadOnlyList<FormationMedoid> Medoids { get; }

    public BuildSpaceSummary Summary { get; }

    public IEnumerable<BuildSpaceState> EnumerateStates()
    {
        foreach (var build in Combinations)
        {
            foreach (var formation in Formations)
            {
                yield return new BuildSpaceState(build, formation);
            }
        }
    }
}
