using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>H100 spec 순서를 보존하는 AND-gate 평가 결과.</summary>
public sealed record GateReport
{
    public const string CurrentSchemaVersion = "h100-gate-report-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string SpecVersion { get; init; } = string.Empty;
    public bool OverallPass { get; init; }
    public int BattleRecordCount { get; init; }
    public int CampaignRecordCount { get; init; }
    public IReadOnlyList<GateResult> Gates { get; init; } = Array.Empty<GateResult>();

    public sealed record GateResult(
        string GateId,
        string NameKo,
        bool Pass,
        IReadOnlyList<ThresholdResult> Thresholds);

    public sealed record ThresholdResult(
        string MetricId,
        string Operator,
        double? ExpectedValue,
        double? MinValue,
        double? MaxValue,
        string Unit,
        bool Observed,
        double? ObservedValue,
        int SampleCount,
        bool Pass,
        string Evidence,
        string Note);
}
