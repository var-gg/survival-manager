using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using SM.HeadlessMetrics;
using UnityEngine;

namespace SM.Editor.Validation;

/// <summary>persisted trace+ledger만 읽어 BT5/BT10 witness와 gate report를 생성한다.</summary>
public static class H100Bt5Bt10GateRunner
{
    private const string Bt1GateSpecRelativePath =
        "Assets/_Game/Scripts/Runtime/HeadlessMetrics/h100-gates-bt1-v1.json";
    private const string Bt5WitnessFileName = "bt5-witness.json";
    private const string Bt10WitnessFileName = "bt10-witness.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static void RunBt5GateFromCli()
    {
        var inputs = ReadInputs();
        var outputDirectory = ResolveOutputDirectory();
        var aggregate = Bt5DesireCommitScorer.Score(inputs, ExpectedPromptSchemaHash());
        WriteWitness(outputDirectory, Bt5WitnessFileName, aggregate);
        var reportPath = WriteReport(outputDirectory, aggregate.ToBt5Observations());
        RequirePassingGate(reportPath, "BT5");
        Debug.Log(
            $"[H100Bt5Bt10] BT5_GATE pass valid={aggregate.ValidRunCount} "
            + $"spontaneous={aggregate.SpontaneousIntentRunCount} "
            + $"commit={aggregate.ScarceResourceCommitRunCount} report={reportPath}");
    }

    public static void RunBt10GateFromCli()
    {
        var inputs = ReadInputs();
        var outputDirectory = ResolveOutputDirectory();
        var ownerApproval = ReadOwnerApproval();
        var aggregate = Bt10FunRetryScorer.Score(
            inputs,
            ownerApproval,
            ExpectedPromptSchemaHash());
        WriteWitness(outputDirectory, Bt10WitnessFileName, aggregate);
        var reportPath = WriteReport(outputDirectory, aggregate.ToBt10Observations());
        RequirePassingGate(reportPath, "BT10");
        Debug.Log(
            $"[H100Bt5Bt10] BT10_GATE pass valid={aggregate.ValidRunCount} "
            + $"desire={aggregate.DesireFormedRunCount} "
            + $"commit={aggregate.EvidenceGroundedCommitRunCount} "
            + $"owner={aggregate.OwnerApproval} report={reportPath}");
    }

    private static IReadOnlyList<SealedScoredRunInput> ReadInputs()
    {
        var tracePaths = ReadPathList("SM_H100_TRACE_PATHS");
        var ledgerPaths = ReadPathList("SM_H100_LEDGER_PATHS");
        if (tracePaths.Length != ledgerPaths.Length)
        {
            throw new InvalidOperationException(
                "SM_H100_TRACE_PATHS and SM_H100_LEDGER_PATHS must contain the same number of paths.");
        }

        var inputs = new SealedScoredRunInput[tracePaths.Length];
        for (var index = 0; index < inputs.Length; index++)
        {
            var tracePath = ResolveExistingFile(tracePaths[index], "sealed trace");
            var ledgerPath = ResolveExistingFile(ledgerPaths[index], "player-visible ledger");
            var trace = HeadlessMetricJson.Deserialize<SealedDecisionTraceV1>(
                Utf8WithoutBom.GetString(File.ReadAllBytes(tracePath)));
            var ledger = ReadLedger(ledgerPath);
            inputs[index] = new SealedScoredRunInput(
                SealedRunDecoder.Decode(trace),
                ledger.Facts,
                ledger.Decisions);
        }

        return inputs;
    }

    private static LedgerRows ReadLedger(string path)
    {
        var facts = new List<PlayerVisibleFactRecord>();
        var decisions = new List<PlayerVisibleDecisionRecord>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(path, Utf8WithoutBom))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var value = JObject.Parse(line);
            var entryKind = value.Value<string>("entry_kind");
            switch (entryKind)
            {
                case "fact":
                    facts.Add(HeadlessMetricJson.Deserialize<PlayerVisibleFactRecord>(line));
                    break;
                case "decision":
                    decisions.Add(HeadlessMetricJson.Deserialize<PlayerVisibleDecisionRecord>(line));
                    break;
                default:
                    throw new InvalidDataException(
                        $"Unknown ledger entry_kind at {path}:{lineNumber}: {entryKind}");
            }
        }

        return new LedgerRows(facts, decisions);
    }

    private static OwnerApprovalArtifact ReadOwnerApproval()
    {
        var requested = Environment.GetEnvironmentVariable("SM_H100_OWNER_APPROVAL_PATH");
        if (string.IsNullOrWhiteSpace(requested))
        {
            return null;
        }

        var path = ResolveExistingFile(requested, "owner approval");
        var file = HeadlessMetricJson.Deserialize<OwnerApprovalFile>(File.ReadAllText(path));
        return new OwnerApprovalArtifact(
            file.Approved,
            file.Statement ?? string.Empty,
            file.ApprovedOn ?? string.Empty,
            file.BoundTraceManifestHashes ?? Array.Empty<string>(),
            path);
    }

    private static string WriteReport(
        string outputDirectory,
        IReadOnlyList<H100GateEvaluator.ExternalObservation> observations)
    {
        var specPath = Path.GetFullPath(Path.Combine(ProjectRoot(), Bt1GateSpecRelativePath));
        var spec = H100Bt1GateSpec.LoadFromFile(specPath);
        var report = H100Bt1GateEvaluator.Generate(
            spec,
            observations,
            legacyReport: null);
        return H100Bt1GateReportWriter.Write(outputDirectory, report);
    }

    private static void RequirePassingGate(string reportPath, string gateId)
    {
        var report = HeadlessMetricJson.Deserialize<H100Bt1GateReport>(File.ReadAllText(reportPath));
        var gate = report.Gates.SingleOrDefault(candidate => string.Equals(
            candidate.GateId,
            gateId,
            StringComparison.Ordinal));
        if (gate == null)
        {
            throw new InvalidOperationException($"H100 gate report does not contain {gateId}.");
        }

        if (!string.Equals(gate.Status, "pass", StringComparison.Ordinal))
        {
            var failed = gate.Thresholds
                .Where(threshold => threshold.Pass != true)
                .Select(threshold =>
                    $"{threshold.MetricId}={threshold.ObservedValue?.ToString("R", CultureInfo.InvariantCulture) ?? "missing"}")
                .ToArray();
            throw new InvalidOperationException(
                $"H100 {gateId} gate failed: {string.Join(",", failed)} report={reportPath}");
        }
    }

    private static void WriteWitness<T>(string outputDirectory, string fileName, T witness)
    {
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(
            Path.Combine(outputDirectory, fileName),
            HeadlessMetricJson.Serialize(witness) + "\n",
            Utf8WithoutBom);
    }

    private static string[] ReadPathList(string environmentName)
    {
        var value = Environment.GetEnvironmentVariable(environmentName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{environmentName} must identify at least one file.");
        }

        var result = value.Split(';')
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToArray();
        if (result.Length == 0)
        {
            throw new InvalidOperationException($"{environmentName} must identify at least one file.");
        }

        return result;
    }

    private static string ResolveExistingFile(string requested, string label)
    {
        var path = Path.GetFullPath(
            Path.IsPathRooted(requested)
                ? requested
                : Path.Combine(ProjectRoot(), requested));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{label} does not exist.", path);
        }

        return path;
    }

    private static string ResolveOutputDirectory()
    {
        var requested = Environment.GetEnvironmentVariable("SM_H100_SEALED_OUTPUT");
        if (string.IsNullOrWhiteSpace(requested))
        {
            throw new InvalidOperationException("SM_H100_SEALED_OUTPUT must identify an output directory.");
        }

        return Path.GetFullPath(
            Path.IsPathRooted(requested)
                ? requested
                : Path.Combine(ProjectRoot(), requested));
    }

    private static string ExpectedPromptSchemaHash()
    {
        var value = Environment.GetEnvironmentVariable("SM_H100_EXPECTED_PROMPT_SCHEMA_HASH");
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string ProjectRoot()
        => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    private sealed record LedgerRows(
        IReadOnlyList<PlayerVisibleFactRecord> Facts,
        IReadOnlyList<PlayerVisibleDecisionRecord> Decisions);

    private sealed class OwnerApprovalFile
    {
        public bool Approved { get; set; }
        public string Statement { get; set; } = string.Empty;
        public string ApprovedOn { get; set; } = string.Empty;
        public IReadOnlyList<string> BoundTraceManifestHashes { get; set; } = Array.Empty<string>();
    }
}
