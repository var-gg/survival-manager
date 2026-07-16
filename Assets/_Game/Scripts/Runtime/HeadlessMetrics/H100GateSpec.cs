using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>H100 AND-gate JSON schema와 loader. threshold 이동 없이 spec 파일을 평가 입력으로 고정한다.</summary>
public sealed class H100GateSpec
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.Ordinal)
    {
        "eq", "gte", "lte", "lt", "range_inclusive",
    };

    public string SchemaVersion { get; set; } = string.Empty;
    public string SpecVersion { get; set; } = string.Empty;
    public string ThresholdPolicyNote { get; set; } = string.Empty;
    public float TargetBattleSeconds { get; set; } = 35f;
    public List<GateDefinition> Gates { get; set; } = new();

    public static H100GateSpec LoadFromFile(string path)
    {
        var spec = HeadlessMetricJson.Deserialize<H100GateSpec>(File.ReadAllText(path));
        spec.Validate();
        return spec;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemaVersion)
            || string.IsNullOrWhiteSpace(SpecVersion)
            || string.IsNullOrWhiteSpace(ThresholdPolicyNote))
        {
            throw new InvalidDataException("H100 gate spec의 schema_version/spec_version/threshold_policy_note가 비어 있다.");
        }

        if (TargetBattleSeconds <= 0f || float.IsNaN(TargetBattleSeconds) || float.IsInfinity(TargetBattleSeconds))
        {
            throw new InvalidDataException("target_battle_seconds는 유한한 양수여야 한다.");
        }

        if (Gates.Count == 0)
        {
            throw new InvalidDataException("H100 gate spec에 gate가 없다.");
        }

        var duplicateGate = Gates.GroupBy(gate => gate.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateGate != null)
        {
            throw new InvalidDataException($"중복 gate id: {duplicateGate.Key}");
        }

        foreach (var gate in Gates)
        {
            if (string.IsNullOrWhiteSpace(gate.Id)
                || string.IsNullOrWhiteSpace(gate.NameKo)
                || string.IsNullOrWhiteSpace(gate.Measurement)
                || gate.Thresholds.Count == 0)
            {
                throw new InvalidDataException("gate id/name/measurement 또는 thresholds가 비어 있다.");
            }

            var duplicateMetric = gate.Thresholds
                .GroupBy(threshold => threshold.MetricId, StringComparer.Ordinal)
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
                    throw new InvalidDataException($"{gate.Id}의 threshold가 유효하지 않다: {threshold.MetricId}/{threshold.Operator}");
                }

                if (threshold.Operator == "range_inclusive"
                    && (!threshold.MinValue.HasValue || !threshold.MaxValue.HasValue || threshold.MinValue > threshold.MaxValue))
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
    }

    private static bool IsNonFinite(double? value)
        => value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value));

    public sealed class GateDefinition
    {
        public string Id { get; set; } = string.Empty;
        public string NameKo { get; set; } = string.Empty;
        public string Measurement { get; set; } = string.Empty;
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
}
