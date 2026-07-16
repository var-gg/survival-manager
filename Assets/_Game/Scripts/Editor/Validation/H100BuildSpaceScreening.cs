using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.Unity;

namespace SM.Editor.Validation;

/// <summary>자동 medoid 전체와 소수의 구조 stratum build/seed만 실행하는 Stage 3 pipeline smoke.</summary>
internal static class H100BuildSpaceScreening
{
    private const string PolicyId = "census-medoid-smoke-v1";

    public static H100BuildSpaceScreeningSummary Run(
        RuntimeCombatContentLookup lookup,
        BuildSpaceCensus census,
        H100BuildSpaceCensusSettings settings,
        float targetBattleSeconds,
        string outputDirectory)
    {
        var selectedBuilds = SelectBuilds(census.Combinations, settings.ScreeningBuildCount);
        var medoids = census.Medoids.OrderBy(medoid => medoid.Placement.Signature, StringComparer.Ordinal).ToArray();
        var cases = new List<H100BattleScreeningCase>(
            selectedBuilds.Count * medoids.Length * settings.ScreeningSeedCount);
        for (var buildIndex = 0; buildIndex < selectedBuilds.Count; buildIndex++)
        {
            var build = selectedBuilds[buildIndex];
            for (var medoidIndex = 0; medoidIndex < medoids.Length; medoidIndex++)
            {
                var placement = medoids[medoidIndex].Placement;
                for (var seedIndex = 0; seedIndex < settings.ScreeningSeedCount; seedIndex++)
                {
                    var seed = H100SessionDriver.DeriveSeed(
                        $"census|{build.BuildId}|{placement.Signature}",
                        settings.SeedBase + seedIndex);
                    var members = build.FormationMembers.Select((member, memberIndex) => new H100BattleScreeningMember(
                        member.ArchetypeId,
                        placement.AnchorsByMemberIndex[memberIndex])).ToArray();
                    cases.Add(new H100BattleScreeningCase(
                        $"build-{build.BuildIndex:D3}-medoid-{medoidIndex + 1:D2}-seed-{seedIndex:D2}",
                        build.BuildId,
                        placement.Signature,
                        seed,
                        members));
                }
            }
        }

        var records = H100BattleCorpusRunner.RunScreening(
            lookup,
            settings.RunId,
            PolicyId,
            cases,
            settings.MaxBattleSteps,
            targetBattleSeconds);
        return H100BuildSpaceScreeningArtifactWriter.Write(
            outputDirectory,
            records,
            selectedBuilds.Select(build => build.BuildId).ToArray(),
            medoids.Select(medoid => medoid.Placement.Signature).ToArray(),
            settings.ScreeningSeedCount);
    }

    private static IReadOnlyList<BuildCombination> SelectBuilds(
        IReadOnlyList<BuildCombination> combinations,
        int requestedCount)
    {
        var selected = new List<BuildCombination>(requestedCount);
        AddFirst(combinations.Where(build => build.Synergy.RaceTier4Count > 0));
        AddFirst(combinations.Where(build => build.Synergy.ClassTier3Count > 0));
        AddFirst(combinations.Where(build => build.Synergy.RaceTier4Count == 0
                                             && build.Synergy.ClassTier3Count == 0
                                             && build.Roles.IsRoleComplete));

        while (selected.Count < requestedCount)
        {
            var next = combinations.Where(candidate => selected.All(value => value.BuildIndex != candidate.BuildIndex))
                .Select(candidate => new
                {
                    Build = candidate,
                    NearestIndexDistance = selected.Count == 0
                        ? int.MaxValue
                        : selected.Min(value => Math.Abs(value.BuildIndex - candidate.BuildIndex)),
                })
                .OrderByDescending(candidate => candidate.NearestIndexDistance)
                .ThenBy(candidate => candidate.Build.BuildId, StringComparer.Ordinal)
                .First();
            selected.Add(next.Build);
        }

        return selected.OrderBy(build => build.BuildIndex).ToArray();

        void AddFirst(IEnumerable<BuildCombination> candidates)
        {
            if (selected.Count >= requestedCount)
            {
                return;
            }

            var candidate = candidates.OrderBy(build => build.BuildId, StringComparer.Ordinal).FirstOrDefault();
            if (candidate != null && selected.All(value => value.BuildIndex != candidate.BuildIndex))
            {
                selected.Add(candidate);
            }
        }
    }
}
