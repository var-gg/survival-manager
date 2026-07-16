using System;
using System.IO;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>H100 Stage 3 census + automatic medoid + small real-battle screening 단일 진입점.</summary>
public static class H100BuildSpaceCensusRunner
{
    private const float TargetBattleSeconds = 35f;

    public static void RunFromCli()
    {
        var settings = H100BuildSpaceCensusSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100BuildSpaceCensusRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var roster = H100BuildSpaceContentAdapter.BuildCanonicalRoster(lookup);
        var census = BuildSpaceEnumerator.Generate(roster);
        CanonicalBuildSpaceContract.RequireExpected(census);
        var artifacts = BuildSpaceArtifactWriter.Write(outputDirectory, census);
        var screening = H100BuildSpaceScreening.Run(
            lookup,
            census,
            settings,
            TargetBattleSeconds,
            outputDirectory);
        if (screening.FailureCount > 0 || screening.CrashCount > 0)
        {
            throw new InvalidOperationException(
                $"Census screening smoke failed: failures={screening.FailureCount} crashes={screening.CrashCount}");
        }

        Debug.Log(
            $"[H100BuildSpaceCensus] builds={census.Summary.TotalCombinations} "
            + $"placements={census.Summary.FormationPlacementsPerCombination} states={census.Summary.TotalStates} "
            + $"medoids={census.Summary.MedoidCount} screening={screening.RecordCount} "
            + $"buildSpace={artifacts.BuildSpaceCsvPath} report={artifacts.CensusReportPath}");
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 census output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
