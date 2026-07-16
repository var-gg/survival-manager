using System;
using System.IO;
using System.Linq;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>H100 Stage 4 formation coverage/causal/placement/healer 단일 CLI 진입점.</summary>
public static class H100FormationRunner
{
    private const float TargetBattleSeconds = 35f;

    public static void RunFromCli()
    {
        var settings = H100FormationRunSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100FormationRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var roster = H100BuildSpaceContentAdapter.BuildCanonicalRoster(lookup);
        var census = BuildSpaceEnumerator.Generate(roster);
        CanonicalBuildSpaceContract.RequireExpected(census);
        var cases = H100FormationCaseFactory.Build(lookup, census, settings);
        var battles = H100FormationBattleRunner.Run(
            lookup,
            settings.RunId,
            cases,
            settings.MaxBattleSteps,
            TargetBattleSeconds);
        var failures = battles.Where(record => !string.IsNullOrWhiteSpace(record.FailureCode)).ToArray();
        if (failures.Length > 0)
        {
            var details = string.Join(
                ", ",
                failures.Take(8).Select(record => $"{record.BattleId}={record.FailureCode}"));
            throw new InvalidOperationException(
                $"Formation runner emitted failed battles: {failures.Length}/{battles.Count}; {details}");
        }

        var causal = FormationCausalEvaluator.Evaluate(battles);
        var placement = PlacementLeverageEvaluator.Evaluate(battles);
        var healer = HealerMarginalValueEvaluator.Evaluate(battles);
        var report = FormationQ5Evaluator.Evaluate(
            settings.RunId,
            HeadlessPolicyFactory.CoverageId,
            settings.CompetentPolicyId,
            causal,
            placement,
            healer);
        var artifacts = FormationEvaluationArtifactWriter.Write(
            outputDirectory,
            causal.EventLogs,
            placement.Records,
            healer.Records,
            report);

        Debug.Log(
            $"[H100Formation] battles={battles.Count} coverage={report.CoveragePass} "
            + $"prevalence={report.CompetentPrevalencePass} impact={report.CompetentImpactPass} "
            + $"placement={report.PlacementLeveragePass} healer={report.HealerSelectionPass} "
            + $"q5={report.CompetentQ5Pass} stage5Balance={report.NeedsStageFiveBalance} "
            + $"report={artifacts.FormationReportPath}");
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 formation output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
