using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>H100-BT1 hard/diagnostic/not-yet-evaluable 상태를 손실 없이 보존하는 보고서.</summary>
public sealed record H100Bt1GateReport
{
    public const string CurrentSchemaVersion = "h100-bt1-gate-report-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string SpecVersion { get; init; } = string.Empty;
    public string LegacySpecVersion { get; init; } = string.Empty;
    public bool StrictMode { get; init; }
    public string OverallStatus { get; init; } = GateEvaluationStatusWire.NotYetEvaluable;
    public bool? OverallPass { get; init; }
    public IReadOnlyList<GateResult> Gates { get; init; } = Array.Empty<GateResult>();
    public IReadOnlyList<LegacyGateResult> LegacyGates { get; init; } = Array.Empty<LegacyGateResult>();

    public sealed record GateResult(
        string GateId,
        string NameKo,
        string Role,
        bool EvaluableNow,
        IReadOnlyList<string> DependsOnEnvelope,
        IReadOnlyList<string> LegacyGateIds,
        string Status,
        bool? Pass,
        IReadOnlyList<ThresholdResult> Thresholds);

    public sealed record LegacyGateResult(
        string LegacyGateId,
        string Role,
        IReadOnlyList<string> BtGateIds,
        string Status,
        bool? Pass,
        IReadOnlyList<ThresholdResult> Thresholds);

    public sealed record ThresholdResult(
        string MetricId,
        string Operator,
        double? ExpectedValue,
        double? MinValue,
        double? MaxValue,
        string Unit,
        string Status,
        bool Observed,
        double? ObservedValue,
        int SampleCount,
        bool? Pass,
        string Evidence,
        string Note);
}

internal static class GateEvaluationStatusWire
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string Missing = "missing";
    public const string NotYetEvaluable = "not_yet_evaluable";
}
