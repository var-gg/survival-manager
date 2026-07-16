using System;
using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>H100 Q2의 3-race x 4-class 구조값을 fail-closed로 고정한다.</summary>
public static class CanonicalBuildSpaceContract
{
    public static void RequireExpected(BuildSpaceCensus census)
    {
        if (census == null)
        {
            throw new ArgumentNullException(nameof(census));
        }

        var summary = census.Summary;
        var failures = new List<string>();
        Expect(failures, nameof(summary.TotalCombinations), summary.TotalCombinations, 495);
        Expect(failures, nameof(summary.FormationPlacementsPerCombination), summary.FormationPlacementsPerCombination, 360);
        Expect(failures, nameof(summary.TotalStates), summary.TotalStates, 178200);
        Expect(failures, nameof(summary.RaceTier2BuildCount), summary.RaceTier2BuildCount, 495);
        Expect(failures, nameof(summary.ClassTier2BuildCount), summary.ClassTier2BuildCount, 414);
        Expect(failures, nameof(summary.ClassTier3BuildCount), summary.ClassTier3BuildCount, 36);
        Expect(failures, nameof(summary.RaceTier4BuildCount), summary.RaceTier4BuildCount, 3);
        Expect(failures, nameof(summary.UpperDoctrineBuildCount), summary.UpperDoctrineBuildCount, 39);
        Expect(failures, nameof(summary.ExactThreeRaceBuildCount), summary.ExactThreeRaceBuildCount, 96);
        Expect(failures, nameof(summary.RaceTwoPlusTwoBuildCount), summary.RaceTwoPlusTwoBuildCount, 108);
        Expect(failures, nameof(summary.ClassTwoPlusTwoBuildCount), summary.ClassTwoPlusTwoBuildCount, 54);
        Expect(failures, nameof(summary.RoleCompleteBuildCount), summary.RoleCompleteBuildCount, 81);
        Expect(failures, nameof(summary.MedoidCount), summary.MedoidCount, 8);
        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Canonical build-space contract failed: {string.Join("; ", failures)}");
        }
    }

    private static void Expect(ICollection<string> failures, string name, int actual, int expected)
    {
        if (actual != expected)
        {
            failures.Add($"{name} expected={expected} actual={actual}");
        }
    }
}
