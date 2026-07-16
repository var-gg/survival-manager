using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SM.Editor.SeedData;
using SM.HeadlessMetrics;
using SM.Unity;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>H100 Stage 1 단일 진입점: 실 전투/캠페인 → JSONL/CSV → GateReport.</summary>
public static class H100MetricsRunner
{
    private const string GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-v1.json";
    private const string Bt1GateSpecRelativePath = "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-bt1-v1.json";
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    /// <summary>HUB 파이프라인 witness용 축소 실행(4 paired battles + 1 short campaign).</summary>
    public static void RunSmokeFromCli() => Run(H100MetricsRunSettings.Smoke);

    /// <summary>환경변수 기반 full 실행. 기본값은 H100 integrity floor(10k battle groups, 10k campaigns).</summary>
    public static void RunFromCli() => Run(H100MetricsRunSettings.FromEnvironment());

    private static void Run(H100MetricsRunSettings settings)
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(H100MetricsRunner));
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var gateSpecPath = Path.GetFullPath(Path.Combine(projectRoot, GateSpecRelativePath));
        var bt1GateSpecPath = Path.GetFullPath(Path.Combine(projectRoot, Bt1GateSpecRelativePath));
        var outputDirectory = ResolveOutputDirectory(projectRoot, settings.OutputDirectory);
        var spec = H100GateSpec.LoadFromFile(gateSpecPath);
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out _, out var contentError))
        {
            throw new InvalidOperationException($"combat snapshot unavailable: {contentError}");
        }

        var battleCorpus = H100BattleCorpusRunner.Run(lookup, settings, spec.TargetBattleSeconds, message => Debug.Log(message));
        var campaignCorpus = H100CampaignCorpusRunner.Run(lookup, settings, spec.TargetBattleSeconds, message => Debug.Log(message));
        var battles = battleCorpus.Concat(campaignCorpus.Battles).ToArray();
        var campaigns = campaignCorpus.Campaigns.ToArray();
        var gateReport = H100GateEvaluator.Generate(spec, battles, campaigns);
        var artifacts = HeadlessMetricArtifactWriter.Write(outputDirectory, battles, campaigns, gateReport, settings.WriteCsv);
        PlayerVisibleFactLedgerArtifactWriter.Write(
            outputDirectory,
            campaignCorpus.Facts,
            campaignCorpus.Decisions);
        var bt1Spec = H100Bt1GateSpec.LoadFromFile(bt1GateSpecPath);
        var bt1Report = H100Bt1GateEvaluator.Generate(
            bt1Spec,
            campaignCorpus.FactAudit.ToBt2Observations(),
            legacyReport: gateReport);
        H100Bt1GateReportWriter.Write(outputDirectory, bt1Report);
        WriteManifest(outputDirectory, settings, spec, artifacts, battles, campaigns, gateReport);

        Debug.Log(
            $"[H100Metrics] complete run={settings.RunId} battles={battles.Length} campaigns={campaigns.Length} "
            + $"overallPass={gateReport.OverallPass} bt2={bt1Report.Gates.Single(gate => gate.GateId == "BT2").Status} "
            + $"facts={campaignCorpus.Facts.Count} decisions={campaignCorpus.Decisions.Count} output={outputDirectory}");
    }

    private static string ResolveOutputDirectory(string projectRoot, string requested)
    {
        var candidate = Path.GetFullPath(Path.IsPathRooted(requested) ? requested : Path.Combine(projectRoot, requested));
        var rootWithSeparator = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"H100 output must stay inside project root: {candidate}");
        }

        return candidate;
    }

    private static void WriteManifest(
        string outputDirectory,
        H100MetricsRunSettings settings,
        H100GateSpec spec,
        HeadlessMetricArtifactWriter.ArtifactSet artifacts,
        IReadOnlyCollection<BattleMetricRecord> battles,
        IReadOnlyCollection<CampaignMetricRecord> campaigns,
        GateReport gateReport)
    {
        var manifest = new RunManifest
        {
            SchemaVersion = "h100-run-manifest-v1",
            RunId = settings.RunId,
            GateSpecVersion = spec.SpecVersion,
            BattleGroupCount = settings.BattleCount,
            EmittedBattleRecordCount = battles.Count,
            CampaignCount = campaigns.Count,
            ReplayCopies = settings.ReplayCopies,
            SeedBase = settings.SeedBase,
            MaxBattleSteps = settings.MaxBattleSteps,
            CampaignSiteSafety = settings.CampaignSiteSafety,
            TargetBattleSeconds = spec.TargetBattleSeconds,
            PolicyId = settings.PolicyId,
            ReplayManifestHash = ReplayHash.ComputeManifest(battles.Select(record => record.ReplayHash)),
            OverallPass = gateReport.OverallPass,
            BattleJsonl = Path.GetFileName(artifacts.BattleJsonlPath),
            CampaignJsonl = Path.GetFileName(artifacts.CampaignJsonlPath),
            GateReport = Path.GetFileName(artifacts.GateReportPath),
            BattleCsv = artifacts.BattleCsvPath == null ? string.Empty : Path.GetFileName(artifacts.BattleCsvPath),
            CampaignCsv = artifacts.CampaignCsvPath == null ? string.Empty : Path.GetFileName(artifacts.CampaignCsvPath),
        };
        File.WriteAllText(
            Path.Combine(outputDirectory, "run-manifest.json"),
            HeadlessMetricJson.Serialize(manifest) + "\n",
            Utf8WithoutBom);
    }

    private sealed class RunManifest
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string RunId { get; set; } = string.Empty;
        public string GateSpecVersion { get; set; } = string.Empty;
        public int BattleGroupCount { get; set; }
        public int EmittedBattleRecordCount { get; set; }
        public int CampaignCount { get; set; }
        public int ReplayCopies { get; set; }
        public int SeedBase { get; set; }
        public int MaxBattleSteps { get; set; }
        public int CampaignSiteSafety { get; set; }
        public float TargetBattleSeconds { get; set; }
        public string PolicyId { get; set; } = string.Empty;
        public string ReplayManifestHash { get; set; } = string.Empty;
        public bool OverallPass { get; set; }
        public string BattleJsonl { get; set; } = string.Empty;
        public string CampaignJsonl { get; set; } = string.Empty;
        public string GateReport { get; set; } = string.Empty;
        public string BattleCsv { get; set; } = string.Empty;
        public string CampaignCsv { get; set; } = string.Empty;
    }
}
