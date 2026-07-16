using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>H100-BT1 게이트와 H100-RC1 migration metadata를 보존하는 순수 스펙.</summary>
public sealed class H100Bt1GateSpec
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.Ordinal)
    {
        "eq", "gte", "lte", "lt", "range_inclusive",
    };

    private static readonly HashSet<string> SupportedEnvelopeIds = Enumerable.Range(1, 9)
        .Select(index => $"E{index:D2}")
        .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlyList<string> KnownLegacyGateIds { get; } = new[]
    {
        "integrity_reproducibility",
        "campaign_completion",
        "build_ecology",
        "effective_build_diversity",
        "decision_depth",
        "formation_significance",
        "spectator_arc",
        "depth_causality",
        "blind_fun_approval",
        "final_reproduction",
    };

    public string SchemaVersion { get; set; } = string.Empty;
    public string SpecVersion { get; set; } = string.Empty;
    public string LegacySpecVersion { get; set; } = string.Empty;
    public string ThresholdPolicyNote { get; set; } = string.Empty;
    public List<GateDefinition> Gates { get; set; } = new();
    public List<LegacyGateMigration> LegacyGateMigrations { get; set; } = new();

    public static H100Bt1GateSpec LoadFromFile(string path)
    {
        var spec = HeadlessMetricJson.Deserialize<H100Bt1GateSpec>(File.ReadAllText(path));
        spec.Validate();
        return spec;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemaVersion)
            || string.IsNullOrWhiteSpace(SpecVersion)
            || string.IsNullOrWhiteSpace(LegacySpecVersion)
            || string.IsNullOrWhiteSpace(ThresholdPolicyNote))
        {
            throw new InvalidDataException(
                "H100-BT1 gate spec의 schema_version/spec_version/legacy_spec_version/threshold_policy_note가 비어 있다.");
        }

        if (Gates.Count == 0)
        {
            throw new InvalidDataException("H100-BT1 gate spec에 gate가 없다.");
        }

        var duplicateGate = Gates.GroupBy(gate => gate.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateGate != null)
        {
            throw new InvalidDataException($"중복 BT gate id: {duplicateGate.Key}");
        }

        var gatesById = Gates.ToDictionary(gate => gate.Id, StringComparer.Ordinal);
        foreach (var gate in Gates)
        {
            ValidateGate(gate);
        }

        var duplicateMigration = LegacyGateMigrations
            .GroupBy(migration => migration.LegacyGateId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMigration != null)
        {
            throw new InvalidDataException($"중복 legacy gate migration: {duplicateMigration.Key}");
        }

        var knownLegacyIds = KnownLegacyGateIds.ToHashSet(StringComparer.Ordinal);
        var mappedLegacyIds = LegacyGateMigrations.Select(migration => migration.LegacyGateId)
            .ToHashSet(StringComparer.Ordinal);
        if (!knownLegacyIds.SetEquals(mappedLegacyIds))
        {
            throw new InvalidDataException("H100-RC1 legacy gate 10종이 migration map에 정확히 한 번씩 있어야 한다.");
        }

        foreach (var migration in LegacyGateMigrations)
        {
            if (!Enum.IsDefined(typeof(GateRole), migration.Role)
                || migration.BtGateIds.Count == 0
                || string.IsNullOrWhiteSpace(migration.Rationale))
            {
                throw new InvalidDataException($"legacy gate migration이 유효하지 않다: {migration.LegacyGateId}");
            }

            if (migration.BtGateIds.Count != migration.BtGateIds.Distinct(StringComparer.Ordinal).Count())
            {
                throw new InvalidDataException($"중복 BT gate mapping: {migration.LegacyGateId}");
            }

            foreach (var btGateId in migration.BtGateIds)
            {
                if (!gatesById.TryGetValue(btGateId, out var btGate)
                    || !btGate.LegacyGateIds.Contains(migration.LegacyGateId, StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        $"legacy↔BT migration 역참조가 일치하지 않는다: {migration.LegacyGateId}/{btGateId}");
                }
            }
        }

        var migrationsByLegacyId = LegacyGateMigrations.ToDictionary(
            migration => migration.LegacyGateId,
            StringComparer.Ordinal);
        foreach (var gate in Gates)
        {
            foreach (var legacyGateId in gate.LegacyGateIds)
            {
                if (!migrationsByLegacyId.TryGetValue(legacyGateId, out var migration)
                    || !migration.BtGateIds.Contains(gate.Id, StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        $"BT↔legacy migration 역참조가 일치하지 않는다: {gate.Id}/{legacyGateId}");
                }
            }
        }
    }

    private static void ValidateGate(GateDefinition gate)
    {
        if (string.IsNullOrWhiteSpace(gate.Id)
            || string.IsNullOrWhiteSpace(gate.NameKo)
            || string.IsNullOrWhiteSpace(gate.Measurement)
            || !Enum.IsDefined(typeof(GateRole), gate.Role)
            || gate.DependsOnEnvelope.Count == 0
            || gate.Thresholds.Count == 0)
        {
            throw new InvalidDataException("BT gate id/name/measurement/role/dependency 또는 thresholds가 비어 있다.");
        }

        if (gate.DependsOnEnvelope.Count != gate.DependsOnEnvelope.Distinct(StringComparer.Ordinal).Count()
            || gate.DependsOnEnvelope.Any(envelopeId => !SupportedEnvelopeIds.Contains(envelopeId)))
        {
            throw new InvalidDataException($"{gate.Id}의 depends_on_envelope가 E01~E09 범위를 벗어났다.");
        }

        if (gate.LegacyGateIds.Count != gate.LegacyGateIds.Distinct(StringComparer.Ordinal).Count()
            || gate.LegacyGateIds.Any(legacyId => !KnownLegacyGateIds.Contains(legacyId, StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"{gate.Id}의 legacy_gate_ids가 유효하지 않다.");
        }

        var duplicateMetric = gate.Thresholds.GroupBy(threshold => threshold.MetricId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateMetric != null)
        {
            throw new InvalidDataException($"{gate.Id}의 중복 metric id: {duplicateMetric.Key}");
        }

        foreach (var threshold in gate.Thresholds)
        {
            if (string.IsNullOrWhiteSpace(threshold.MetricId)
                || !SupportedOperators.Contains(threshold.Operator))
            {
                throw new InvalidDataException(
                    $"{gate.Id}의 threshold가 유효하지 않다: {threshold.MetricId}/{threshold.Operator}");
            }

            if (threshold.Operator == "range_inclusive"
                && (!threshold.MinValue.HasValue
                    || !threshold.MaxValue.HasValue
                    || threshold.MinValue > threshold.MaxValue))
            {
                throw new InvalidDataException($"{gate.Id}/{threshold.MetricId} range가 유효하지 않다.");
            }

            if (threshold.Operator != "range_inclusive" && !threshold.Value.HasValue)
            {
                throw new InvalidDataException($"{gate.Id}/{threshold.MetricId} value가 없다.");
            }

            if (IsNonFinite(threshold.Value)
                || IsNonFinite(threshold.MinValue)
                || IsNonFinite(threshold.MaxValue))
            {
                throw new InvalidDataException($"{gate.Id}/{threshold.MetricId} threshold가 유한하지 않다.");
            }
        }
    }

    private static bool IsNonFinite(double? value)
        => value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value));

    public sealed class GateDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string NameKo { get; set; } = string.Empty;
        public string Measurement { get; set; } = string.Empty;
        public GateRole Role { get; set; }
        public bool EvaluableNow { get; set; }
        public List<string> DependsOnEnvelope { get; set; } = new();
        public List<string> LegacyGateIds { get; set; } = new();
        public List<ThresholdDefinition> Thresholds { get; set; } = new();
    }

    public sealed class ThresholdDefinition
    {
        public string MetricId { get; set; } = string.Empty;
        public string Operator { get; set; } = string.Empty;
        public double? Value { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public sealed class LegacyGateMigration
    {
        public string LegacyGateId { get; set; } = string.Empty;
        public GateRole Role { get; set; }
        public List<string> BtGateIds { get; set; } = new();
        public string Rationale { get; set; } = string.Empty;
    }
}
