using System;
using System.Globalization;
using System.IO;

namespace SM.HeadlessMetrics;

/// <summary>
/// SealedDecisionTraceV1의 canonical bytes와 FNV-1a hash를 소유한다.
/// 모든 part는 ReplayHash와 동일한 UTF-8 byte-length prefix framing을 사용한다.
/// </summary>
public static class SealedDecisionTraceHash
{
    public const string HeaderCanonicalSchema = "SealedDecisionTraceHeaderV1";
    public const string EntryCanonicalSchema = "SealedDecisionEntryV1";
    public const string PayloadHashSchema = "SealedDecisionCanonicalPayloadV1";
    public const string ManifestSchema = "SealedDecisionTraceManifestV1";

    public static byte[] GetHeaderCanonicalBytes(SealedDecisionTraceHeader header)
    {
        if (header == null) throw new ArgumentNullException(nameof(header));

        using var payload = new MemoryStream();
        Append(payload, HeaderCanonicalSchema);
        Append(payload, header.TraceSchemaVersion);
        Append(payload, header.RunId);
        Append(payload, header.ScenarioId);
        Append(payload, Invariant(header.CampaignSeed));
        Append(payload, header.BuildManifestHash);
        Append(payload, header.ContentManifestHash);
        Append(payload, header.VisibleSurfaceHash);
        Append(payload, header.PromptSchemaHash);
        Append(payload, header.ScorerConfigHash);
        Append(payload, CaptureSourceToken(header.CaptureSource));
        return payload.ToArray();
    }

    public static byte[] GetEntryCanonicalBytes(SealedDecisionEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (entry.SeamKey == null) throw new ArgumentException("SeamKey is required.", nameof(entry));

        using var payload = new MemoryStream();
        Append(payload, EntryCanonicalSchema);
        Append(payload, Invariant(entry.SeamKey.DecisionIndex));
        Append(payload, entry.SeamKey.SeamType);
        Append(payload, Invariant(entry.SeamKey.Ordinal));
        Append(payload, entry.PreStateHash);
        Append(payload, entry.ObservationCanonicalBytes);
        Append(payload, entry.ObservationHash);
        Append(payload, entry.LegalActionSetHash);
        Append(payload, entry.HistoryPrefixHash);
        Append(payload, entry.RequestCanonicalBytes);
        Append(payload, entry.RequestHash);
        Append(payload, entry.ResponseCanonicalBytes);
        Append(payload, entry.ResponseHash);
        Append(payload, entry.SelectedAction);
        Append(payload, entry.AppliedActionHash);
        Append(payload, entry.ResultEventHash);
        Append(payload, entry.PostStateHash);
        Append(payload, entry.PreviousEntryHash);
        Append(payload, entry.TerminalFailure ? "1" : "0");
        return payload.ToArray();
    }

    public static string ComputeCanonicalPayloadHash(byte[] canonicalBytes)
    {
        if (canonicalBytes == null) throw new ArgumentNullException(nameof(canonicalBytes));

        using var payload = new MemoryStream();
        Append(payload, PayloadHashSchema);
        Append(payload, canonicalBytes);
        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }

    public static string ComputeHeaderHash(SealedDecisionTraceHeader header)
        => LengthPrefixedStableHash.Compute(GetHeaderCanonicalBytes(header));

    public static string ComputeEntryHash(SealedDecisionEntry entry)
        => LengthPrefixedStableHash.Compute(GetEntryCanonicalBytes(entry));

    /// <summary>
    /// Header hash와 entry hash를 trace의 의미 있는 순서 그대로 결합한다.
    /// 첫 entry의 PreviousEntryHash sentinel은 이 manifest에도 쓰이는 header hash다.
    /// </summary>
    public static string ComputeManifest(SealedDecisionTraceV1 trace)
    {
        if (trace == null) throw new ArgumentNullException(nameof(trace));
        if (trace.Header == null) throw new ArgumentException("Header is required.", nameof(trace));
        if (trace.Entries == null) throw new ArgumentException("Entries are required.", nameof(trace));

        using var payload = new MemoryStream();
        Append(payload, ManifestSchema);
        Append(payload, ComputeHeaderHash(trace.Header));
        Append(payload, Invariant(trace.Entries.Count));
        foreach (var entry in trace.Entries)
        {
            if (entry == null) throw new ArgumentException("Trace entries cannot contain null.", nameof(trace));
            Append(payload, ComputeEntryHash(entry));
        }

        return LengthPrefixedStableHash.Compute(payload.ToArray());
    }

    private static void Append(Stream payload, string value)
        => LengthPrefixedStableHash.AppendPart(payload, value);

    private static void Append(Stream payload, byte[] value)
        => LengthPrefixedStableHash.AppendPart(payload, value);

    private static string Invariant(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string CaptureSourceToken(SealedDecisionTraceCaptureSource captureSource)
        => captureSource switch
        {
            SealedDecisionTraceCaptureSource.SyntheticStandIn => "SyntheticStandIn",
            SealedDecisionTraceCaptureSource.LiveColdStartLlm => "LiveColdStartLlm",
            _ => throw new ArgumentOutOfRangeException(nameof(captureSource), captureSource, "Unknown capture source."),
        };
}
