using System;
using System.Collections.Generic;

namespace SM.SealedLlmBridge;

/// <summary>E07b-2b가 declared intent provenance로 교체하기 전 choice-only decode용 marker.</summary>
internal static class SealedLlmDecisionEnvelope
{
    public const string Rationale = "llm";

    public static IReadOnlyList<string> EvidenceFactIds { get; } =
        Array.AsReadOnly(new[] { "llm" });
}
