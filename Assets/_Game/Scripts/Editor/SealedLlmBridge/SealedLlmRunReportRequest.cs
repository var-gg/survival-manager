using System;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>Canonical terminal run-report request shared by synthetic, replay, and future live sources.</summary>
public sealed record SealedLlmRunReportRequest
{
    public SealedLlmRunReportRequest(
        SealedDecisionSeamKey seamKey,
        byte[] requestCanonicalBytes,
        string statusToken = "completed")
    {
        SeamKey = seamKey ?? throw new ArgumentNullException(nameof(seamKey));
        if (!string.Equals(seamKey.SeamType, SealedLlmSeamTypes.RunReport, StringComparison.Ordinal)
            || seamKey.DecisionIndex < 0
            || seamKey.Ordinal != 0)
        {
            throw new ArgumentException(
                "Run-report seam key must use seamType run_report, a non-negative decision index, and ordinal 0.",
                nameof(seamKey));
        }

        RequestCanonicalBytes = (byte[])(requestCanonicalBytes
            ?? throw new ArgumentNullException(nameof(requestCanonicalBytes))).Clone();
        if (string.IsNullOrWhiteSpace(statusToken))
        {
            throw new ArgumentException("Run-report status token is required.", nameof(statusToken));
        }

        StatusToken = statusToken;
    }

    public SealedDecisionSeamKey SeamKey { get; }
    public byte[] RequestCanonicalBytes { get; }
    public string StatusToken { get; }
}
