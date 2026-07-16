using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessCensus;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>coverage 단일 계약 주입과 catalog-hidden discovery를 같은 real campaign 경로에서 smoke한다.</summary>
public static class H100IntentTraceRunner
{
    private const string GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static void RunFromCli()
    {
        var settings = H100IntentTraceRunSettings.FromEnvironment();
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100IntentTraceRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var outputRoot = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var spec = H100GateSpec.LoadFromFile(Path.Combine(projectRoot, GateSpecRelativePath));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out var snapshot, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        if (settings.IncludesCoverage)
        {
            var catalog = H100ConceptCatalogRunner.DeriveForSnapshot(snapshot);
            var coverageIntent = H100ConceptIntentProjector.ProjectSingle(catalog, settings.CoverageAnchorId);
            RunLane(
                "coverage",
                outputRoot,
                settings,
                lookup,
                spec.TargetBattleSeconds,
                _ => new ConceptCommitPolicy(coverageIntent),
                coverageIntent.IntentId);
        }

        if (settings.IncludesDiscovery)
        {
            RunLane(
                "discovery",
                outputRoot,
                settings,
                lookup,
                spec.TargetBattleSeconds,
                _ => new ConceptCommitPolicy(),
                string.Empty);
        }
    }

    private static void RunLane(
        string lane,
        string outputRoot,
        H100IntentTraceRunSettings settings,
        RuntimeCombatContentLookup lookup,
        float targetBattleSeconds,
        Func<int, IHeadlessPolicy> policyFactory,
        string projectedIntentId)
    {
        var outputDirectory = Path.Combine(outputRoot, lane);
        var runSettings = new H100MetricsRunSettings(
            BattleCount: 1,
            CampaignCount: settings.SeedCount,
            ReplayCopies: 2,
            SeedBase: settings.SeedBase,
            CampaignSiteSafety: settings.CampaignSiteSafety,
            MaxBattleSteps: settings.MaxBattleSteps,
            WriteCsv: false,
            OutputDirectory: outputDirectory,
            PolicyId: ConceptCommitPolicy.PolicyId);
        var corpus = H100CampaignCorpusRunner.Run(
            lookup,
            runSettings,
            targetBattleSeconds,
            message => Debug.Log(message),
            observationHooks: null,
            policyFactory);

        var tracePath = IntentTraceArtifactWriter.Write(outputDirectory, corpus.IntentTraces);
        var missingTraceCount = corpus.Decisions.Count - corpus.IntentTraces.Count;
        var committedCampaigns = corpus.IntentTraces
            .Where(value => value.IsCommit)
            .Select(value => value.CampaignId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var hiddenFactUseCount = corpus.FactAudit.PostDecisionInformationReferenceCount
                                 + corpus.FactAudit.NonUiSemanticInternalFieldReferenceCount
                                 + corpus.FactAudit.OracleOrTruthLeakCount
                                 + corpus.FactAudit.UnsupportedCertainClaimCount;
        var commitIndexes = corpus.IntentTraces.Where(value => value.IsCommit)
            .Select(value => value.DecidedAt.DecisionIndex)
            .OrderBy(value => value)
            .ToArray();
        var reasons = corpus.IntentTraces
            .GroupBy(value => value.Reason, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new IntentReasonCount { Reason = group.Key, Count = group.Count() })
            .ToArray();
        var intentIds = corpus.IntentTraces.Select(value => value.IntentState.IntentId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var summary = new IntentTraceRunSummary
        {
            SchemaVersion = "intent-trace-summary-bt1-v1",
            Lane = lane,
            ProjectedIntentId = projectedIntentId,
            IntentIds = intentIds,
            CampaignCount = corpus.Campaigns.Count,
            DecisionCount = corpus.Decisions.Count,
            TraceLineCount = corpus.IntentTraces.Count,
            MissingTraceCount = missingTraceCount,
            CampaignsWithCommit = committedCampaigns,
            CommitDecisionIndexes = commitIndexes,
            ReasonDistribution = reasons,
            HiddenFactUseCount = hiddenFactUseCount,
            FactCount = corpus.Facts.Count,
            TraceFile = Path.GetFileName(tracePath),
        };
        var summaryPath = Path.Combine(outputDirectory, "intent_trace_summary.json");
        File.WriteAllText(summaryPath, HeadlessMetricJson.Serialize(summary) + "\n", Utf8WithoutBom);

        if (missingTraceCount != 0)
        {
            throw new InvalidOperationException($"{lane} intent trace missed {missingTraceCount} policy decisions.");
        }

        if (hiddenFactUseCount != 0)
        {
            throw new InvalidOperationException($"{lane} intent policy used {hiddenFactUseCount} hidden or unsupported facts.");
        }

        if (corpus.IntentTraces.Count == 0 || committedCampaigns != corpus.Campaigns.Count)
        {
            throw new InvalidOperationException(
                $"{lane} commit_t coverage incomplete (traces={corpus.IntentTraces.Count}, committed={committedCampaigns}, campaigns={corpus.Campaigns.Count}).");
        }

        Debug.Log(
            $"[H100IntentTrace] lane={lane} campaigns={corpus.Campaigns.Count} traces={corpus.IntentTraces.Count} "
            + $"commits={committedCampaigns} commitIndexes=[{string.Join(",", commitIndexes)}] "
            + $"reasons=[{string.Join(",", reasons.Select(value => $"{value.Reason}:{value.Count}"))}] output={outputDirectory}");
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 intent trace output must stay inside project root: {candidate}");
        }

        return candidate;
    }

    private sealed class IntentTraceRunSummary
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string Lane { get; set; } = string.Empty;
        public string ProjectedIntentId { get; set; } = string.Empty;
        public IReadOnlyList<string> IntentIds { get; set; } = Array.Empty<string>();
        public int CampaignCount { get; set; }
        public int DecisionCount { get; set; }
        public int TraceLineCount { get; set; }
        public int MissingTraceCount { get; set; }
        public int CampaignsWithCommit { get; set; }
        public IReadOnlyList<int> CommitDecisionIndexes { get; set; } = Array.Empty<int>();
        public IReadOnlyList<IntentReasonCount> ReasonDistribution { get; set; } = Array.Empty<IntentReasonCount>();
        public int HiddenFactUseCount { get; set; }
        public int FactCount { get; set; }
        public string TraceFile { get; set; } = string.Empty;
    }

    private sealed class IntentReasonCount
    {
        public string Reason { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
