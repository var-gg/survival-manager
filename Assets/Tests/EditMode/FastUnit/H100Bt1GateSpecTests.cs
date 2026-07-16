using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class H100Bt1GateSpecTests
{
    private static readonly string Bt1SpecPath = Path.Combine(
        "Assets", "_Game", "Scripts", "Runtime", "HeadlessMetrics", "h100-gates-bt1-v1.json");

    private static readonly string Rc1SpecPath = Path.Combine(
        "Assets", "_Game", "Scripts", "Runtime", "HeadlessMetrics", "h100-gates-v1.json");

    [Test]
    public void CheckedInSpec_LoadsAllTenHardGates_WithExactThresholdsAndDependencies()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);

        Assert.That(spec.SchemaVersion, Is.EqualTo("h100-bt1-gate-spec-v1"));
        Assert.That(spec.SpecVersion, Is.EqualTo("h100-gates-bt1-v1"));
        Assert.That(spec.LegacySpecVersion, Is.EqualTo("h100-gates-v1"));
        Assert.That(spec.Gates.Select(gate => gate.Id), Is.EqualTo(Enumerable.Range(1, 10).Select(index => $"BT{index}")));
        Assert.That(spec.Gates, Has.All.Matches<H100Bt1GateSpec.GateDefinition>(gate => gate.Role == GateRole.Hard));
        Assert.That(spec.Gates.Where(gate => gate.Id is "BT2" or "BT3"),
            Has.All.Matches<H100Bt1GateSpec.GateDefinition>(gate => gate.EvaluableNow));
        Assert.That(
            spec.Gates.Where(gate => gate.Id is not ("BT2" or "BT3")),
            Has.All.Matches<H100Bt1GateSpec.GateDefinition>(gate => !gate.EvaluableNow));

        var expectedDependencies = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["BT1"] = new[] { "E07" },
            ["BT2"] = new[] { "E01" },
            ["BT3"] = new[] { "E02" },
            ["BT4"] = new[] { "E02", "E03", "E07" },
            ["BT5"] = new[] { "E04", "E07" },
            ["BT6"] = new[] { "E03", "E05" },
            ["BT7"] = new[] { "E03", "E05" },
            ["BT8"] = new[] { "E01", "E06" },
            ["BT9"] = new[] { "E08", "E09" },
            ["BT10"] = new[] { "E04", "E05", "E07" },
        };
        foreach (var gate in spec.Gates)
        {
            Assert.That(gate.DependsOnEnvelope, Is.EqualTo(expectedDependencies[gate.Id]), gate.Id);
            Assert.That(ThresholdSignatures(gate), Is.EqualTo(ExpectedThresholds[gate.Id]), gate.Id);
        }
    }

    [Test]
    public void CheckedInSpec_MapsEveryLegacyGateExactlyOnce()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);

        Assert.That(spec.LegacyGateMigrations, Has.Count.EqualTo(10));
        Assert.That(
            spec.LegacyGateMigrations.Select(migration => migration.LegacyGateId),
            Is.EquivalentTo(H100Bt1GateSpec.KnownLegacyGateIds));
        Assert.That(
            spec.LegacyGateMigrations.Single(migration => migration.LegacyGateId == "integrity_reproducibility").Role,
            Is.EqualTo(GateRole.Hard));
        Assert.That(
            spec.LegacyGateMigrations.Where(migration => migration.LegacyGateId != "integrity_reproducibility"),
            Has.All.Matches<H100Bt1GateSpec.LegacyGateMigration>(migration => migration.Role == GateRole.Diagnostic));
    }

    [Test]
    public void HardGate_MissingMetricFailsClosed()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);
        spec.Gates.Single(gate => gate.Id == "BT1").EvaluableNow = true;

        var report = H100Bt1GateEvaluator.Generate(spec);
        var result = report.Gates.Single(gate => gate.GateId == "BT1");

        Assert.That(result.Status, Is.EqualTo("fail"));
        Assert.That(result.Pass, Is.False);
        Assert.That(result.Thresholds, Has.All.Matches<H100Bt1GateReport.ThresholdResult>(
            threshold => threshold.Status == "missing" && !threshold.Observed && threshold.Pass == false));
        Assert.That(report.OverallStatus, Is.EqualTo("fail"));
        Assert.That(report.OverallPass, Is.False);
    }

    [Test]
    public void Bt2_ZeroAuditMetricsAreEvaluatedAndPass()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);
        var observations = ExpectedThresholds["BT2"]
            .Select(signature => signature.Split('|')[0])
            .Select(metricId => new H100GateEvaluator.ExternalObservation(metricId, 0d, 4, "fact ledger witness"))
            .ToArray();

        var report = H100Bt1GateEvaluator.Generate(spec, observations);
        var bt2 = report.Gates.Single(gate => gate.GateId == "BT2");

        Assert.That(bt2.Status, Is.EqualTo("pass"));
        Assert.That(bt2.Pass, Is.True);
        Assert.That(bt2.Thresholds, Has.All.Matches<H100Bt1GateReport.ThresholdResult>(
            threshold => threshold.Observed && threshold.ObservedValue == 0d && threshold.Pass == true));
    }

    [Test]
    public void DiagnosticMissing_IsExplicitAndDoesNotBlockOverall()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);
        foreach (var gate in spec.Gates)
        {
            gate.Role = GateRole.Diagnostic;
            gate.EvaluableNow = true;
        }

        var report = H100Bt1GateEvaluator.Generate(spec);

        Assert.That(report.Gates, Has.All.Matches<H100Bt1GateReport.GateResult>(
            gate => gate.Status == "missing" && gate.Pass == null));
        Assert.That(report.Gates.SelectMany(gate => gate.Thresholds),
            Has.All.Matches<H100Bt1GateReport.ThresholdResult>(threshold => threshold.Status == "missing"));
        Assert.That(report.OverallStatus, Is.EqualTo("pass"));
        Assert.That(report.OverallPass, Is.True);
    }

    [Test]
    public void NotYetEvaluable_StrictModeFailsWithoutLosingPendingStatus()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);

        var report = H100Bt1GateEvaluator.Generate(spec, strictMode: true);

        Assert.That(report.Gates.Where(gate => gate.GateId is "BT2" or "BT3"),
            Has.All.Matches<H100Bt1GateReport.GateResult>(gate => gate.Status == "fail"));
        Assert.That(
            report.Gates.Where(gate => gate.GateId is not ("BT2" or "BT3")),
            Has.All.Matches<H100Bt1GateReport.GateResult>(
                gate => gate.Status == "not_yet_evaluable" && gate.Pass == false));
        Assert.That(report.OverallStatus, Is.EqualTo("fail"));
        Assert.That(report.OverallPass, Is.False);
    }

    [Test]
    public void LegacyDiagnosticReport_PreservesObservedValueAndMissingStatus()
    {
        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);
        var legacyReport = new GateReport
        {
            SpecVersion = spec.LegacySpecVersion,
            Gates = new[]
            {
                new GateReport.GateResult(
                    "campaign_completion",
                    "캠페인 완주성",
                    false,
                    new[]
                    {
                        new GateReport.ThresholdResult(
                            "competent_completion_rate", "range_inclusive", null, 0.60, 0.80,
                            "ratio", true, 0.50, 8, false, "paired campaigns", "legacy band"),
                        new GateReport.ThresholdResult(
                            "ensemble_solvability_rate", "gte", 0.90, null, null,
                            "ratio", false, null, 0, false, "metric unavailable", "legacy reachability"),
                    }),
            },
        };

        var report = H100Bt1GateEvaluator.Generate(spec, legacyReport: legacyReport);
        var legacy = report.LegacyGates.Single(gate => gate.LegacyGateId == "campaign_completion");

        Assert.That(legacy.Role, Is.EqualTo("diagnostic"));
        Assert.That(legacy.Status, Is.EqualTo("missing"));
        Assert.That(legacy.Pass, Is.Null);
        Assert.That(legacy.Thresholds.Single(threshold => threshold.MetricId == "competent_completion_rate").ObservedValue,
            Is.EqualTo(0.50));
        Assert.That(legacy.Thresholds.Single(threshold => threshold.MetricId == "ensemble_solvability_rate").Status,
            Is.EqualTo("missing"));
    }

    [Test]
    public void Bt1ReportWriter_IsByteDeterministicAndUsesSeparatePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "sm-h100-bt1-gate-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);
            var report = H100Bt1GateEvaluator.Generate(spec);

            var firstPath = H100Bt1GateReportWriter.Write(root, report);
            var first = File.ReadAllBytes(firstPath);
            var secondPath = H100Bt1GateReportWriter.Write(root, report);

            Assert.That(Path.GetFileName(firstPath), Is.EqualTo("h100-bt1-gate-report.json"));
            Assert.That(File.ReadAllBytes(secondPath), Is.EqualTo(first));
            Assert.That(File.Exists(Path.Combine(root, "gate-report.json")), Is.False);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Test]
    public void Rc1GateSpec_RemainsByteFrozen()
    {
        using var sha256 = SHA256.Create();
        var hash = BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(Rc1SpecPath)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        Assert.That(hash, Is.EqualTo("5a43868586b395d38963b2382f012367491caa50b2518a9e6afa6ee9fb54e1bf"));
    }

    private static string[] ThresholdSignatures(H100Bt1GateSpec.GateDefinition gate)
        => gate.Thresholds.Select(ThresholdSignature).ToArray();

    private static string ThresholdSignature(H100Bt1GateSpec.ThresholdDefinition threshold)
    {
        static string Number(double? value)
            => value?.ToString("R", CultureInfo.InvariantCulture) ?? "-";

        return string.Join("|", threshold.MetricId, threshold.Operator, Number(threshold.Value),
            Number(threshold.MinValue), Number(threshold.MaxValue));
    }

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedThresholds =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["BT1"] = new[]
            {
                "independent_process_replay_count|gte|3|-|-",
                "state_event_result_hash_match_rate|eq|1|-|-",
                "sealed_llm_decision_trace_replay_match_rate|eq|1|-|-",
            },
            ["BT2"] = new[]
            {
                "post_decision_information_reference_count|eq|0|-|-",
                "non_ui_semantic_internal_field_reference_count|eq|0|-|-",
                "oracle_or_truth_leak_count|eq|0|-|-",
                "unsupported_certain_claim_count|eq|0|-|-",
            },
            ["BT3"] = new[]
            {
                "actionable_offer_missing_semantics|eq|0|-|-",
                "undefined_visible_token|eq|0|-|-",
                "hidden_prerequisite|eq|0|-|-",
                "description_behavior_mismatch_count|eq|0|-|-",
            },
            ["BT4"] = new[]
            {
                "cold_start_run_count|eq|6|-|-",
                "valid_concept_by_progress_0_30_count|gte|5|-|-",
                "grammar_family_precision_min|gte|0.85|-|-",
                "grammar_family_recall_min|gte|0.7|-|-",
            },
            ["BT5"] = new[]
            {
                "cold_start_run_count|eq|6|-|-",
                "spontaneous_intent_run_count|gte|5|-|-",
                "scarce_resource_commit_run_count|gte|4|-|-",
            },
            ["BT6"] = new[]
            {
                "core_track_realizability_rate|gte|0.75|-|-",
                "core_track_realizability_lcb95|gte|0.65|-|-",
                "aspirational_track_realizability_rate|gte|0.35|-|-",
                "aspirational_track_realizability_lcb95|gte|0.25|-|-",
                "core_first_progress_agency_window_p90|lte|3|-|-",
                "aspirational_first_progress_agency_window_p90|lte|5|-|-",
                "starvation_run_rate|lte|0.1|-|-",
                "silent_dead_end_count|eq|0|-|-",
            },
            ["BT7"] = new[]
            {
                "owner_anchor_realization_rate_min|gte|0.7|-|-",
                "owner_anchor_realization_lcb95_min|gte|0.55|-|-",
                "owner_anchor_realized_before_final_20_percent_rate_min|gte|0.7|-|-",
                "owner_anchor_post_realization_battle_opportunity_min|gte|2|-|-",
                "owner_anchor_payoff_witness_count_min|gte|1|-|-",
                "owner_anchor_failure_count|eq|0|-|-",
                "derived_medoid_pass_rate|gte|0.75|-|-",
            },
            ["BT8"] = new[]
            {
                "concept_adaptive_completion_rate|gte|0.5|-|-",
                "concept_adaptive_completion_lcb95|gte|0.4|-|-",
                "no_cheat_macro_family_completion_witness_min|gte|1|-|-",
                "oracle_0_8_blocker_chosen_win_rate|gte|0.7|-|-",
                "oracle_0_8_blocker_selection_regret|lte|0.25|-|-",
                "hard_wall_count|eq|0|-|-",
            },
            ["BT9"] = new[]
            {
                "confirmed_trap_count|eq|0|-|-",
                "unresolved_mechanical_defect_count|eq|0|-|-",
                "bug_grade_dominant_count|eq|0|-|-",
            },
            ["BT10"] = new[]
            {
                "cold_start_run_count|eq|6|-|-",
                "desire_formed_run_count|gte|5|-|-",
                "evidence_grounded_commit_run_count|gte|4|-|-",
                "payoff_or_legible_near_miss_run_count|gte|4|-|-",
                "next_concept_named_run_count|gte|4|-|-",
                "complaint_repeated_twice_count|eq|0|-|-",
                "evaluation_sentence_telemetry_link_rate|eq|1|-|-",
                "owner_approval|eq|1|-|-",
            },
        };
}
