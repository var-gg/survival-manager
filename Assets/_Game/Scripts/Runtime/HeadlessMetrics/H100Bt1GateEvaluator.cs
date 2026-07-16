using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>H100-BT1 역할별 missing semantics와 strict RC 판정을 적용한다.</summary>
public static class H100Bt1GateEvaluator
{
    public static H100Bt1GateReport Generate(
        H100Bt1GateSpec spec,
        IReadOnlyList<H100GateEvaluator.ExternalObservation>? observations = null,
        bool strictMode = false,
        GateReport? legacyReport = null)
    {
        spec.Validate();
        if (legacyReport != null
            && !string.Equals(legacyReport.SpecVersion, spec.LegacySpecVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"legacy report spec version mismatch: {legacyReport.SpecVersion}/{spec.LegacySpecVersion}",
                nameof(legacyReport));
        }

        var metrics = BuildMetricIndex(observations);
        var gateResults = spec.Gates.Select(gate => EvaluateGate(gate, metrics, strictMode)).ToArray();
        var legacyResults = spec.LegacyGateMigrations
            .Select(migration => EvaluateLegacyGate(migration, legacyReport))
            .ToArray();
        var overall = ResolveOverall(gateResults, strictMode);
        return new H100Bt1GateReport
        {
            SpecVersion = spec.SpecVersion,
            LegacySpecVersion = spec.LegacySpecVersion,
            StrictMode = strictMode,
            OverallStatus = overall.Status,
            OverallPass = overall.Pass,
            Gates = gateResults,
            LegacyGates = legacyResults,
        };
    }

    private static Dictionary<string, H100GateEvaluator.ExternalObservation> BuildMetricIndex(
        IReadOnlyList<H100GateEvaluator.ExternalObservation>? observations)
    {
        var metrics = new Dictionary<string, H100GateEvaluator.ExternalObservation>(StringComparer.Ordinal);
        foreach (var observation in observations ?? Array.Empty<H100GateEvaluator.ExternalObservation>())
        {
            if (string.IsNullOrWhiteSpace(observation.MetricId)
                || observation.SampleCount < 0
                || double.IsNaN(observation.Value)
                || double.IsInfinity(observation.Value))
            {
                throw new ArgumentException("H100-BT1 observation이 유효하지 않다.", nameof(observations));
            }

            if (!metrics.TryAdd(observation.MetricId, observation))
            {
                throw new InvalidOperationException($"중복 H100-BT1 observation: {observation.MetricId}");
            }
        }

        return metrics;
    }

    private static H100Bt1GateReport.GateResult EvaluateGate(
        H100Bt1GateSpec.GateDefinition gate,
        IReadOnlyDictionary<string, H100GateEvaluator.ExternalObservation> metrics,
        bool strictMode)
    {
        if (!gate.EvaluableNow)
        {
            var pendingThresholds = gate.Thresholds.Select(threshold => new H100Bt1GateReport.ThresholdResult(
                threshold.MetricId,
                threshold.Operator,
                threshold.Value,
                threshold.MinValue,
                threshold.MaxValue,
                threshold.Unit,
                GateEvaluationStatusWire.NotYetEvaluable,
                false,
                null,
                0,
                strictMode ? false : null,
                $"metric supplier pending: {string.Join(",", gate.DependsOnEnvelope)}",
                threshold.Note)).ToArray();
            return BuildGateResult(
                gate,
                GateEvaluationStatusWire.NotYetEvaluable,
                strictMode ? false : null,
                pendingThresholds);
        }

        var thresholds = gate.Thresholds.Select(threshold => EvaluateThreshold(threshold, gate.Role, metrics)).ToArray();
        var anyMissing = thresholds.Any(result => result.Status == GateEvaluationStatusWire.Missing);
        if (anyMissing)
        {
            return gate.Role == GateRole.Hard
                ? BuildGateResult(gate, GateEvaluationStatusWire.Fail, false, thresholds)
                : BuildGateResult(gate, GateEvaluationStatusWire.Missing, null, thresholds);
        }

        var pass = thresholds.All(result => result.Pass == true);
        return BuildGateResult(
            gate,
            pass ? GateEvaluationStatusWire.Pass : GateEvaluationStatusWire.Fail,
            pass,
            thresholds);
    }

    private static H100Bt1GateReport.GateResult BuildGateResult(
        H100Bt1GateSpec.GateDefinition gate,
        string status,
        bool? pass,
        IReadOnlyList<H100Bt1GateReport.ThresholdResult> thresholds)
    {
        return new H100Bt1GateReport.GateResult(
            gate.Id,
            gate.NameKo,
            gate.Role.ToWireValue(),
            gate.EvaluableNow,
            gate.DependsOnEnvelope.ToArray(),
            gate.LegacyGateIds.ToArray(),
            status,
            pass,
            thresholds);
    }

    private static H100Bt1GateReport.ThresholdResult EvaluateThreshold(
        H100Bt1GateSpec.ThresholdDefinition threshold,
        GateRole role,
        IReadOnlyDictionary<string, H100GateEvaluator.ExternalObservation> metrics)
    {
        if (!metrics.TryGetValue(threshold.MetricId, out var observed))
        {
            return new H100Bt1GateReport.ThresholdResult(
                threshold.MetricId,
                threshold.Operator,
                threshold.Value,
                threshold.MinValue,
                threshold.MaxValue,
                threshold.Unit,
                GateEvaluationStatusWire.Missing,
                false,
                null,
                0,
                role == GateRole.Hard ? false : null,
                "metric unavailable",
                threshold.Note);
        }

        var pass = Compare(
            threshold.Operator,
            observed.Value,
            threshold.Value,
            threshold.MinValue,
            threshold.MaxValue);
        return new H100Bt1GateReport.ThresholdResult(
            threshold.MetricId,
            threshold.Operator,
            threshold.Value,
            threshold.MinValue,
            threshold.MaxValue,
            threshold.Unit,
            pass ? GateEvaluationStatusWire.Pass : GateEvaluationStatusWire.Fail,
            true,
            observed.Value,
            observed.SampleCount,
            pass,
            observed.Evidence ?? string.Empty,
            threshold.Note);
    }

    private static H100Bt1GateReport.LegacyGateResult EvaluateLegacyGate(
        H100Bt1GateSpec.LegacyGateMigration migration,
        GateReport? legacyReport)
    {
        var source = legacyReport?.Gates.SingleOrDefault(
            gate => string.Equals(gate.GateId, migration.LegacyGateId, StringComparison.Ordinal));
        if (source == null)
        {
            return new H100Bt1GateReport.LegacyGateResult(
                migration.LegacyGateId,
                migration.Role.ToWireValue(),
                migration.BtGateIds.ToArray(),
                migration.Role == GateRole.Hard ? GateEvaluationStatusWire.Fail : GateEvaluationStatusWire.Missing,
                migration.Role == GateRole.Hard ? false : null,
                Array.Empty<H100Bt1GateReport.ThresholdResult>());
        }

        var thresholds = source.Thresholds.Select(threshold => ConvertLegacyThreshold(threshold, migration.Role)).ToArray();
        var anyMissing = thresholds.Any(threshold => threshold.Status == GateEvaluationStatusWire.Missing);
        var pass = thresholds.All(threshold => threshold.Pass == true);
        var status = anyMissing
            ? migration.Role == GateRole.Hard
                ? GateEvaluationStatusWire.Fail
                : GateEvaluationStatusWire.Missing
            : pass
                ? GateEvaluationStatusWire.Pass
                : GateEvaluationStatusWire.Fail;
        bool? resultPass = anyMissing && migration.Role == GateRole.Diagnostic ? null : pass;
        return new H100Bt1GateReport.LegacyGateResult(
            migration.LegacyGateId,
            migration.Role.ToWireValue(),
            migration.BtGateIds.ToArray(),
            status,
            resultPass,
            thresholds);
    }

    private static H100Bt1GateReport.ThresholdResult ConvertLegacyThreshold(
        GateReport.ThresholdResult threshold,
        GateRole role)
    {
        var status = !threshold.Observed
            ? GateEvaluationStatusWire.Missing
            : threshold.Pass
                ? GateEvaluationStatusWire.Pass
                : GateEvaluationStatusWire.Fail;
        return new H100Bt1GateReport.ThresholdResult(
            threshold.MetricId,
            threshold.Operator,
            threshold.ExpectedValue,
            threshold.MinValue,
            threshold.MaxValue,
            threshold.Unit,
            status,
            threshold.Observed,
            threshold.ObservedValue,
            threshold.SampleCount,
            threshold.Observed ? threshold.Pass : role == GateRole.Hard ? false : null,
            threshold.Evidence,
            threshold.Note);
    }

    private static (string Status, bool? Pass) ResolveOverall(
        IReadOnlyList<H100Bt1GateReport.GateResult> gates,
        bool strictMode)
    {
        var hardGates = gates.Where(gate => gate.Role == GateRole.Hard.ToWireValue()).ToArray();
        if (hardGates.Any(gate => gate.Status == GateEvaluationStatusWire.Fail)
            || strictMode && hardGates.Any(gate => gate.Status == GateEvaluationStatusWire.NotYetEvaluable))
        {
            return (GateEvaluationStatusWire.Fail, false);
        }

        if (hardGates.Any(gate => gate.Status == GateEvaluationStatusWire.NotYetEvaluable))
        {
            return (GateEvaluationStatusWire.NotYetEvaluable, null);
        }

        return (GateEvaluationStatusWire.Pass, true);
    }

    private static bool Compare(
        string @operator,
        double observed,
        double? expected,
        double? min,
        double? max)
    {
        return @operator switch
        {
            "eq" => Math.Abs(observed - expected!.Value) <= 1e-9d,
            "gte" => observed >= expected!.Value,
            "lte" => observed <= expected!.Value,
            "lt" => observed < expected!.Value,
            "range_inclusive" => observed >= min!.Value && observed <= max!.Value,
            _ => false,
        };
    }
}
