using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

public static class BuildSpaceCensusAnalyzer
{
    public static BuildSpaceSummary Analyze(
        IReadOnlyList<BuildCombination> combinations,
        IReadOnlyList<FormationPlacement> formations,
        IReadOnlyList<FormationMedoid> medoids)
    {
        var raceTier2 = combinations.Count(build => build.Synergy.RaceTier2Count > 0);
        var classTier2 = combinations.Count(build => build.Synergy.ClassTier2Count > 0);
        var classTier3 = combinations.Count(build => build.Synergy.ClassTier3Count > 0);
        var raceTier4 = combinations.Count(build => build.Synergy.RaceTier4Count > 0);
        var upperDoctrine = combinations.Count(build => build.Synergy.ClassTier3Count > 0 || build.Synergy.RaceTier4Count > 0);
        var raceThree = combinations.Count(build => build.HasExactRaceThree);
        var raceTwoPlusTwo = combinations.Count(build => build.IsRaceTwoPlusTwo);
        var classTwoPlusTwo = combinations.Count(build => build.IsClassTwoPlusTwo);
        var roleComplete = combinations.Count(build => build.Roles.IsRoleComplete);
        var flags = new List<BuildSpaceFlag>();

        if (raceTier2 == combinations.Count)
        {
            flags.Add(new BuildSpaceFlag(
                "race-tier2-automatic",
                "warning",
                raceTier2,
                "race@2 activates in every four-member build and is not a scarce choice."));
        }

        if (raceThree > 0)
        {
            flags.Add(new BuildSpaceFlag(
                "race-three-dead-zone",
                "warning",
                raceThree,
                "Exactly-three-race builds sit between race@2 and race@4 without a distinct upper breakpoint."));
        }

        if (classTier3 > raceTier4)
        {
            flags.Add(new BuildSpaceFlag(
                "upper-doctrine-rarity-asymmetry",
                "warning",
                upperDoctrine,
                $"class@3 has {classTier3} builds while race@4 has {raceTier4}."));
        }

        return new BuildSpaceSummary(
            combinations.Count,
            formations.Count,
            checked(combinations.Count * formations.Count),
            raceTier2,
            classTier2,
            classTier3,
            raceTier4,
            upperDoctrine,
            raceThree,
            raceTwoPlusTwo,
            classTwoPlusTwo,
            roleComplete,
            medoids.Count,
            flags.OrderBy(flag => flag.Id, StringComparer.Ordinal).ToArray());
    }
}
