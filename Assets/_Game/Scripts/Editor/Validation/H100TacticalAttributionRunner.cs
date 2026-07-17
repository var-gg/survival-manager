using System;
using System.IO;
using System.Linq;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>BT1-E09 TacticalAttribution의 단일 CLI entrypoint.</summary>
public static class H100TacticalAttributionRunner
{
    public static void RunFromCli()
    {
        var settings = H100TacticalAttributionRunSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100TacticalAttributionRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var snapshotError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {snapshotError}");
        }

        var roster = H100BuildSpaceContentAdapter.BuildCanonicalRosterFromSnapshot(snapshot);
        var census = BuildSpaceEnumerator.Generate(roster);
        CanonicalBuildSpaceContract.RequireExpected(census);
        var catalog = H100ConceptCatalogRunner.DeriveForSnapshot(snapshot);
        var cases = H100TacticalAttributionCaseFactory.Build(snapshot, census, catalog, settings);
        var battles = H100TacticalAttributionBattleRunner.Run(
            lookup,
            settings.RunId,
            cases,
            settings.MaxBattleSteps);
        var evidence = H100TacticalAttributionEvidenceJoin.Build(projectRoot, catalog, settings);
        var report = PlacementAttributionEvaluator.Evaluate(
            settings.RunId,
            battles,
            evidence,
            "8 concept-medoid compositions x 3 authored site families x paired seeds; 8-anchor sweep per stratum");
        var reportPath = PlacementAttributionArtifactWriter.Write(outputDirectory, report);
        var failures = battles.Where(value => !string.IsNullOrWhiteSpace(value.FailureCode)).ToArray();
        if (failures.Length > 0)
        {
            var details = string.Join(", ", failures.Take(8).Select(value => $"{value.BattleId}={value.FailureCode}"));
            throw new InvalidOperationException(
                $"Tactical attribution emitted failed battles: {failures.Length}/{battles.Count}; {details}; report={reportPath}");
        }

        Debug.Log(
            $"[H100TacticalAttribution] battles={report.Sample.BattleCount} pairs={report.Sample.PairCount} "
            + $"compositions={report.Sample.CompositionCount} families={report.Sample.EncounterFamilyCount} "
            + $"verdict={report.Verdict} triggered={report.ProConditions.Count(value => value.Triggered)} "
            + $"trapCandidates={report.FormationOptions.Count(value => value.TrapCandidate)} report={reportPath}");
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"BT1-E09 output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
