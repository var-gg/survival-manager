namespace SM.HeadlessMetrics;

/// <summary>채널 한 건의 eligible → fired → causal → legible 판정을 평탄화한 결정적 로그 행.</summary>
public sealed record FormationEventLogRecord
{
    public const string CurrentSchemaVersion = "formation-event-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public string RunId { get; init; } = string.Empty;
    public string BattleId { get; init; } = string.Empty;
    public string PairingId { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
    public string PlacementVariantId { get; init; } = string.Empty;
    public string CoverageProbeChannelId { get; init; } = string.Empty;
    public int Seed { get; init; }
    public string ChannelId { get; init; } = string.Empty;
    public bool Eligible { get; init; }
    public bool Fired { get; init; }
    public bool Causal { get; init; }
    public bool Legible { get; init; }
    public int EventCount { get; init; }
    public float OutcomeDelta { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public string CausalMethod { get; init; } = string.Empty;
}
