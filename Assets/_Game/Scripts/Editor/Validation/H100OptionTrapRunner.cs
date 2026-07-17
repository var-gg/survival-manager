using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>BT1-E08 golden-neutral option trap/dominance oracle의 CLI entry point.</summary>
public static class H100OptionTrapRunner
{
    private const string Bt1GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-bt1-v1.json";

    public static void RunFromCli()
    {
        var settings = H100OptionTrapRunSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100OptionTrapRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var sources = H100BuildGrammarTruthProjector.ProjectSources(snapshot);
        var graph = BuildGrammarTruthGraphBuilder.Build(sources);
        var contracts = OptionWitnessContractDeriver.Derive(graph, sources);
        var mechanical = H100OptionMechanicalProbe.Sweep(contracts, sources);
        var stageAFlagged = OptionTrapOracleEvaluator.ScreenStageA(contracts, mechanical);
        var healthy = SelectHealthySample(contracts, stageAFlagged, settings.SeedBase, settings.HealthySampleCount);
        var selected = stageAFlagged.Concat(healthy).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var census = BuildSpaceEnumerator.Generate(
            H100BuildSpaceContentAdapter.BuildCanonicalRosterFromSnapshot(snapshot),
            settings.MedoidCount);
        var screeningPairs = H100OptionTrapBattleRunner.Run(
            lookup,
            snapshot,
            census,
            contracts,
            sources,
            selected,
            settings,
            fullCensus: false);
        var plan = new OptionTrapSamplingPlan(
            settings.SeedBase,
            settings.SeedCount,
            census.Medoids.Count,
            healthy.Length,
            census.Formations.Count,
            "sha256(seed_base|option_id), lowest 12 outside Stage A flags",
            "source requires predicates are added to both sides; same build, placement, enemy and seed",
            "only preliminary trap/dominant threshold candidates receive all 360 placements");
        var preliminary = OptionTrapOracleEvaluator.Evaluate(new OptionTrapOracleInput(
            contracts,
            mechanical,
            screeningPairs,
            Array.Empty<OptionContinuationComparison>(),
            plan));
        var fullCandidateIds = preliminary.Evidence
            .Where(value => value.CandidateStatus == "trap_candidate" || value.BugGradeDominant)
            .Select(value => value.OptionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var fullPairs = H100OptionTrapBattleRunner.Run(
            lookup,
            snapshot,
            census,
            contracts,
            sources,
            fullCandidateIds,
            settings,
            fullCensus: true);
        var continuationCandidateIds = preliminary.Evidence
            .Where(value => value.OwnerVerdictRequired || value.CandidateStatus == "trap_candidate")
            .Select(value => value.OptionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var continuations = H100OptionContinuationProbe.EvaluateCandidates(contracts, continuationCandidateIds);
        var report = OptionTrapOracleEvaluator.Evaluate(new OptionTrapOracleInput(
            contracts,
            mechanical,
            screeningPairs.Concat(fullPairs).ToArray(),
            continuations,
            plan));
        var reportPath = OptionTrapArtifactWriter.Write(outputDirectory, report);

        var bt1Spec = H100Bt1GateSpec.LoadFromFile(Path.Combine(projectRoot, Bt1GateSpecRelativePath));
        var observations = new[]
        {
            new H100GateEvaluator.ExternalObservation("confirmed_trap_count", report.ConfirmedTrapCount, contracts.Count, reportPath),
            new H100GateEvaluator.ExternalObservation("unresolved_mechanical_defect_count", report.MechanicalDefectCandidateCount, contracts.Count, reportPath),
            new H100GateEvaluator.ExternalObservation("bug_grade_dominant_count", report.BugGradeDominantCount, screeningPairs.Count, reportPath),
        };
        var bt1Report = H100Bt1GateEvaluator.Generate(bt1Spec, observations);
        H100Bt1GateReportWriter.Write(outputDirectory, bt1Report);
        var bt9 = bt1Report.Gates.Single(value => value.GateId == "BT9");
        Debug.Log(
            $"[H100OptionTrap] contracts={contracts.Count} stageA={stageAFlagged.Count} sampled={selected.Length} "
            + $"pairs={screeningPairs.Count} full={fullPairs.Count} confirmed={report.ConfirmedTrapCount} "
            + $"dominant={report.BugGradeDominantCount} rescued={report.RescuedEnablerCount} bt9={bt9.Status} output={reportPath}");
    }

    private static string[] SelectHealthySample(
        IReadOnlyList<OptionWitnessContract> contracts,
        IReadOnlyCollection<string> flagged,
        int seedBase,
        int count)
    {
        var blocked = flagged.ToHashSet(StringComparer.Ordinal);
        return contracts.Where(value => !blocked.Contains(value.OptionId))
            .Select(value => new { value.OptionId, Hash = StableHash($"{seedBase}|{value.OptionId}") })
            .OrderBy(value => value.Hash, StringComparer.Ordinal)
            .ThenBy(value => value.OptionId, StringComparer.Ordinal)
            .Take(count)
            .Select(value => value.OptionId)
            .ToArray();
    }

    private static string StableHash(string value)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 option-trap output must stay inside project root: {candidate}");
        }

        return candidate;
    }
}
