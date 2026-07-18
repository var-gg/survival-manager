using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.Editor.Validation;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[TestFixture]
[Category("BatchOnly")]
public sealed class H100Bt5Bt10GateRunnerSyntheticTests
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    [Test]
    public void SyntheticPersistedCohort_FlipsBt5AndBt10GatesToPass()
    {
        var root = CreateTemporaryRoot();
        var environment = CaptureEnvironment();
        try
        {
            var fixtures = Bt5Bt10SyntheticFixture.CreateCohort();
            var paths = WriteCohort(root, fixtures);
            var ownerPath = WriteOwnerApproval(root, fixtures.Select(fixture =>
                SealedDecisionTraceHash.ComputeManifest(fixture.Trace)).ToArray());
            SetInputEnvironment(paths, ownerPath);

            var bt5Output = Path.Combine(root, "bt5-output");
            Environment.SetEnvironmentVariable("SM_H100_SEALED_OUTPUT", bt5Output);
            H100Bt5Bt10GateRunner.RunBt5GateFromCli();
            AssertGatePass(bt5Output, "BT5");
            Assert.That(File.Exists(Path.Combine(bt5Output, "bt5-witness.json")), Is.True);

            var bt10Output = Path.Combine(root, "bt10-output");
            Environment.SetEnvironmentVariable("SM_H100_SEALED_OUTPUT", bt10Output);
            H100Bt5Bt10GateRunner.RunBt10GateFromCli();
            AssertGatePass(bt10Output, "BT10");
            Assert.That(File.Exists(Path.Combine(bt10Output, "bt10-witness.json")), Is.True);
        }
        finally
        {
            RestoreEnvironment(environment);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void SyntheticRedCorpus_WritesFailingComplaintTelemetryAndOwnerThresholds()
    {
        var root = CreateTemporaryRoot();
        var environment = CaptureEnvironment();
        try
        {
            var fixtures = Bt5Bt10SyntheticFixture.CreateCohort(index => index switch
            {
                0 => new Bt5Bt10SyntheticRunOptions
                {
                    TelemetryEventIdOverride = "fact-polluted",
                    Complaints = new[] { "opaque choices" },
                },
                1 => new Bt5Bt10SyntheticRunOptions
                {
                    IncludeEvaluationSentence = false,
                    Complaints = new[] { "  OPAQUE   CHOICES " },
                },
                _ => null,
            });
            var paths = WriteCohort(root, fixtures);
            var staleHashes = fixtures.Select(fixture =>
                SealedDecisionTraceHash.ComputeManifest(fixture.Trace)).ToArray();
            staleHashes[0] = "stale-owner-binding";
            var ownerPath = WriteOwnerApproval(root, staleHashes);
            SetInputEnvironment(paths, ownerPath);
            var output = Path.Combine(root, "red-output");
            Environment.SetEnvironmentVariable("SM_H100_SEALED_OUTPUT", output);

            Assert.Throws<InvalidOperationException>(() =>
                H100Bt5Bt10GateRunner.RunBt10GateFromCli());

            var report = ReadReport(output);
            var bt10 = report.Gates.Single(gate => gate.GateId == "BT10");
            Assert.That(bt10.Status, Is.EqualTo("fail"));
            AssertThresholdFail(bt10, "complaint_repeated_twice_count");
            AssertThresholdFail(bt10, "evaluation_sentence_telemetry_link_rate");
            AssertThresholdFail(bt10, "owner_approval");
        }
        finally
        {
            RestoreEnvironment(environment);
            Directory.Delete(root, recursive: true);
        }
    }

    private static CohortPaths WriteCohort(
        string root,
        IReadOnlyList<Bt5Bt10SyntheticRunFixture> fixtures)
    {
        var tracePaths = new List<string>();
        var ledgerPaths = new List<string>();
        for (var index = 0; index < fixtures.Count; index++)
        {
            var runDirectory = Path.Combine(root, $"run-{index}");
            Directory.CreateDirectory(runDirectory);
            var tracePath = Path.Combine(runDirectory, "sealed-decision-trace-v1.json");
            var ledgerPath = Path.Combine(runDirectory, "player_visible_fact_ledger.jsonl");
            File.WriteAllText(
                tracePath,
                HeadlessMetricJson.Serialize(fixtures[index].Trace) + "\n",
                Utf8WithoutBom);
            var ledgerRows = fixtures[index].Facts
                .Select(HeadlessMetricJson.Serialize)
                .Concat(fixtures[index].Decisions.Select(HeadlessMetricJson.Serialize));
            File.WriteAllText(
                ledgerPath,
                string.Join("\n", ledgerRows) + "\n",
                Utf8WithoutBom);
            tracePaths.Add(tracePath);
            ledgerPaths.Add(ledgerPath);
        }

        return new CohortPaths(tracePaths, ledgerPaths);
    }

    private static string WriteOwnerApproval(string root, IReadOnlyList<string> manifestHashes)
    {
        var path = Path.Combine(root, "owner-approval.json");
        File.WriteAllText(
            path,
            HeadlessMetricJson.Serialize(new OwnerApprovalFileFixture
            {
                Approved = true,
                Statement = "synthetic fixture approval",
                ApprovedOn = "2026-07-19",
                BoundTraceManifestHashes = manifestHashes,
            }) + "\n",
            Utf8WithoutBom);
        return path;
    }

    private static void SetInputEnvironment(CohortPaths paths, string ownerPath)
    {
        Environment.SetEnvironmentVariable(
            "SM_H100_TRACE_PATHS",
            string.Join(";", paths.TracePaths));
        Environment.SetEnvironmentVariable(
            "SM_H100_LEDGER_PATHS",
            string.Join(";", paths.LedgerPaths));
        Environment.SetEnvironmentVariable("SM_H100_OWNER_APPROVAL_PATH", ownerPath);
        Environment.SetEnvironmentVariable("SM_H100_EXPECTED_PROMPT_SCHEMA_HASH", "prompt-schema");
    }

    private static void AssertGatePass(string outputDirectory, string gateId)
    {
        var gate = ReadReport(outputDirectory).Gates.Single(candidate => candidate.GateId == gateId);
        Assert.That(gate.Status, Is.EqualTo("pass"));
        Assert.That(gate.Pass, Is.True);
        Assert.That(gate.Thresholds.All(threshold => threshold.Pass == true), Is.True);
    }

    private static void AssertThresholdFail(H100Bt1GateReport.GateResult gate, string metricId)
    {
        var threshold = gate.Thresholds.Single(value => value.MetricId == metricId);
        Assert.That(threshold.Observed, Is.True);
        Assert.That(threshold.Pass, Is.False);
    }

    private static H100Bt1GateReport ReadReport(string outputDirectory)
        => HeadlessMetricJson.Deserialize<H100Bt1GateReport>(File.ReadAllText(
            Path.Combine(outputDirectory, H100Bt1GateReportWriter.FileName)));

    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "survival-manager-bt5-bt10-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static IReadOnlyDictionary<string, string> CaptureEnvironment()
    {
        var names = new[]
        {
            "SM_H100_TRACE_PATHS",
            "SM_H100_LEDGER_PATHS",
            "SM_H100_OWNER_APPROVAL_PATH",
            "SM_H100_EXPECTED_PROMPT_SCHEMA_HASH",
            "SM_H100_SEALED_OUTPUT",
        };
        return names.ToDictionary(
            name => name,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);
    }

    private static void RestoreEnvironment(IReadOnlyDictionary<string, string> values)
    {
        foreach (var pair in values)
        {
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    private sealed record CohortPaths(
        IReadOnlyList<string> TracePaths,
        IReadOnlyList<string> LedgerPaths);

    private sealed class OwnerApprovalFileFixture
    {
        public bool Approved { get; set; }
        public string Statement { get; set; }
        public string ApprovedOn { get; set; }
        public IReadOnlyList<string> BoundTraceManifestHashes { get; set; }
    }
}
