using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed record BuildSpaceSummary(
    int TotalCombinations,
    int FormationPlacementsPerCombination,
    int TotalStates,
    int RaceTier2BuildCount,
    int ClassTier2BuildCount,
    int ClassTier3BuildCount,
    int RaceTier4BuildCount,
    int UpperDoctrineBuildCount,
    int ExactThreeRaceBuildCount,
    int RaceTwoPlusTwoBuildCount,
    int ClassTwoPlusTwoBuildCount,
    int RoleCompleteBuildCount,
    int MedoidCount,
    IReadOnlyList<BuildSpaceFlag> Flags);
