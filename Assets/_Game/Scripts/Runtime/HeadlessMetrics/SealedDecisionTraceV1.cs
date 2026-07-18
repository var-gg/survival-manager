using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>cold-start LLM decision capture와 deterministic replay를 연결하는 ordered sealed trace.</summary>
public sealed record SealedDecisionTraceV1(
    SealedDecisionTraceHeader Header,
    IReadOnlyList<SealedDecisionEntry> Entries)
{
    public const string SchemaVersion = "SealedDecisionTraceV1";

    /// <summary>header의 hard capture-source lock을 trace 소비자에게 직접 노출한다.</summary>
    public bool CertificationEligible => Header?.CertificationEligible == true;
}
