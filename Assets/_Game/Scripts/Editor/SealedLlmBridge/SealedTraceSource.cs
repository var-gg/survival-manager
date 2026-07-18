using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>
/// Strict replay source that consumes one sealed response at a time while the caller rebuilds observations and
/// state hashes from a fresh campaign. The source never fabricates, skips, or searches ahead for an entry.
/// </summary>
public sealed class SealedTraceSource : ISealedDecisionSource
{
    private const string DecisionCanonicalSchema = "LlmDecisionResponseV1";
    private const string DeclaredIntentCanonicalSchema = "LlmDeclaredIntentV1";
    private const string BuildHypothesisCanonicalSchema = "LlmBuildHypothesisV1";
    private const string RunReportCanonicalSchema = "LlmRunReportResponseV1";
    private const string EvaluationSentenceCanonicalSchema = "LlmEvaluationSentenceV1";

    private readonly IReadOnlyList<SealedDecisionEntry> _entries;
    private int _nextEntryIndex;

    public SealedTraceSource(SealedDecisionTraceV1 sealedTrace)
    {
        if (sealedTrace == null) throw new ArgumentNullException(nameof(sealedTrace));
        if (sealedTrace.Entries == null)
        {
            throw new DivergenceException(
                DivergenceReason.SealedEntriesMissing,
                -1,
                null,
                null,
                "sealed entries collection is null");
        }

        var entries = new SealedDecisionEntry[sealedTrace.Entries.Count];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = sealedTrace.Entries[index];
            if (entry?.SeamKey == null)
            {
                throw new DivergenceException(
                    DivergenceReason.SealedEntryMissing,
                    index,
                    null,
                    null,
                    "sealed entry or seam key is null");
            }

            entries[index] = CloneEntry(entry);
        }

        _entries = entries;
    }

    public int ConsumedEntryCount => _nextEntryIndex;

    public int TotalEntryCount => _entries.Count;

    public DivergenceException LastDivergence { get; private set; }

    public LlmDecisionResponseV1 RequestDecision(SealedLlmDecisionRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        try
        {
            var entry = RequireExpectedEntry(request.SeamKey, request.RequestCanonicalBytes, expectRunReport: false);
            var response = ParseDecision(entry);
            _nextEntryIndex++;
            return response;
        }
        catch (DivergenceException exception)
        {
            LastDivergence = exception;
            throw;
        }
    }

    public LlmRunReportResponseV1 RequestRunReport(SealedLlmRunReportRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        try
        {
            var entry = RequireExpectedEntry(request.SeamKey, request.RequestCanonicalBytes, expectRunReport: true);
            var response = ParseRunReport(entry);
            _nextEntryIndex++;
            return response;
        }
        catch (DivergenceException exception)
        {
            LastDivergence = exception;
            throw;
        }
    }

    /// <summary>Fails closed when replay did not request every sealed entry exactly once.</summary>
    public void EnsureFullyConsumed()
    {
        if (_nextEntryIndex == _entries.Count)
        {
            return;
        }

        var expected = _nextEntryIndex < _entries.Count
            ? _entries[_nextEntryIndex].SeamKey
            : null;
        LastDivergence = new DivergenceException(
            DivergenceReason.UnconsumedEntries,
            _nextEntryIndex,
            expected,
            null,
            $"replay consumed {_nextEntryIndex} of {_entries.Count} sealed entries");
        throw LastDivergence;
    }

    private SealedDecisionEntry RequireExpectedEntry(
        SealedDecisionSeamKey actualKey,
        byte[] actualRequestBytes,
        bool expectRunReport)
    {
        if (_nextEntryIndex >= _entries.Count)
        {
            throw new DivergenceException(
                DivergenceReason.UnexpectedExtraRequest,
                _nextEntryIndex,
                null,
                actualKey,
                "replay requested a decision after the sealed trace was exhausted");
        }

        var expected = _entries[_nextEntryIndex];
        if (!Equals(expected.SeamKey, actualKey))
        {
            throw new DivergenceException(
                DivergenceReason.SeamKeyMismatch,
                _nextEntryIndex,
                expected.SeamKey,
                actualKey,
                "requested seam is not the expected next sealed entry");
        }

        var isRunReport = string.Equals(
            expected.SeamKey.SeamType,
            SealedLlmSeamTypes.RunReport,
            StringComparison.Ordinal);
        if (isRunReport != expectRunReport)
        {
            throw new DivergenceException(
                DivergenceReason.EntryKindMismatch,
                _nextEntryIndex,
                expected.SeamKey,
                actualKey,
                expectRunReport
                    ? "replay requested a run report but the next sealed entry is a normal decision"
                    : "replay requested a normal decision but the next sealed entry is the run report");
        }

        if (!SameBytes(expected.RequestCanonicalBytes, actualRequestBytes))
        {
            throw new DivergenceException(
                DivergenceReason.RequestCanonicalBytesMismatch,
                _nextEntryIndex,
                expected.SeamKey,
                actualKey,
                "fresh replay request bytes differ from the sealed request bytes");
        }

        return expected;
    }

    private LlmDecisionResponseV1 ParseDecision(SealedDecisionEntry entry)
    {
        RequireResponseIntegrity(entry);
        try
        {
            var decoded = DecodeDecision(entry.ResponseCanonicalBytes);
            var parsed = LlmWireCanonicalSerializer.ParseDecisionResponse(
                LlmWireCanonicalSerializer.CanonicalJson(decoded));
            RequireCanonicalRoundTrip(entry, LlmWireCanonicalSerializer.CanonicalBytes(parsed));
            return parsed;
        }
        catch (DivergenceException)
        {
            throw;
        }
        catch (Exception exception) when (IsCanonicalParseFailure(exception))
        {
            throw InvalidResponse(entry, exception);
        }
    }

    private LlmRunReportResponseV1 ParseRunReport(SealedDecisionEntry entry)
    {
        RequireResponseIntegrity(entry);
        try
        {
            var decoded = DecodeRunReport(entry.ResponseCanonicalBytes);
            var parsed = LlmWireCanonicalSerializer.ParseRunReportResponse(
                LlmWireCanonicalSerializer.CanonicalJson(decoded));
            RequireCanonicalRoundTrip(entry, LlmWireCanonicalSerializer.CanonicalBytes(parsed));
            return parsed;
        }
        catch (DivergenceException)
        {
            throw;
        }
        catch (Exception exception) when (IsCanonicalParseFailure(exception))
        {
            throw InvalidResponse(entry, exception);
        }
    }

    private void RequireResponseIntegrity(SealedDecisionEntry entry)
    {
        if (entry.ResponseCanonicalBytes == null || entry.ResponseHash == null)
        {
            throw new DivergenceException(
                DivergenceReason.ResponsePayloadMissing,
                _nextEntryIndex,
                entry.SeamKey,
                entry.SeamKey,
                "sealed response bytes or response hash is null");
        }

        var actualHash = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.ResponseCanonicalBytes);
        if (!string.Equals(entry.ResponseHash, actualHash, StringComparison.Ordinal))
        {
            throw new DivergenceException(
                DivergenceReason.ResponseHashMismatch,
                _nextEntryIndex,
                entry.SeamKey,
                entry.SeamKey,
                "sealed response_hash does not hash response_canonical_bytes");
        }
    }

    private void RequireCanonicalRoundTrip(SealedDecisionEntry entry, byte[] rebuiltBytes)
    {
        if (!SameBytes(entry.ResponseCanonicalBytes, rebuiltBytes))
        {
            throw new DivergenceException(
                DivergenceReason.ResponseCanonicalBytesInvalid,
                _nextEntryIndex,
                entry.SeamKey,
                entry.SeamKey,
                "sealed response bytes are not the canonical encoding of the parsed response");
        }
    }

    private DivergenceException InvalidResponse(SealedDecisionEntry entry, Exception innerException)
        => new(
            DivergenceReason.ResponseCanonicalBytesInvalid,
            _nextEntryIndex,
            entry.SeamKey,
            entry.SeamKey,
            $"sealed response canonical frame is invalid: {innerException.Message}",
            innerException);

    private static LlmDecisionResponseV1 DecodeDecision(byte[] bytes)
    {
        var reader = new CanonicalFrameReader(bytes, "decision");
        reader.RequireSchema(DecisionCanonicalSchema);
        var selectedAction = reader.ReadString("selected_action");
        var declaredIntent = DecodeDeclaredIntent(reader.ReadBytes("declared_intent"));
        var intentRef = reader.ReadString("intent_ref");
        var hypothesisCount = reader.ReadCount("build_hypothesis_count");
        var hypotheses = new LlmBuildHypothesisV1[hypothesisCount];
        for (var index = 0; index < hypotheses.Length; index++)
        {
            hypotheses[index] = DecodeBuildHypothesis(reader.ReadBytes($"build_hypotheses[{index}]"));
        }

        reader.RequireEnd();
        return new LlmDecisionResponseV1(selectedAction, declaredIntent, intentRef, hypotheses);
    }

    private static LlmDeclaredIntentV1 DecodeDeclaredIntent(byte[] bytes)
    {
        var reader = new CanonicalFrameReader(bytes, "declared_intent");
        reader.RequireSchema(DeclaredIntentCanonicalSchema);
        var result = new LlmDeclaredIntentV1(
            reader.ReadString("intent_id"),
            reader.ReadStrings("track_token_ids"),
            reader.ReadString("expected_payoff"),
            reader.ReadStrings("evidence_fact_ids"),
            reader.ReadString("next_acquisition_plan"),
            reader.ReadStrings("allowed_substitutions"),
            reader.ReadStrings("pivot_conditions"),
            reader.ReadDouble("confidence"));
        reader.RequireEnd();
        return result;
    }

    private static LlmBuildHypothesisV1 DecodeBuildHypothesis(byte[] bytes)
    {
        var reader = new CanonicalFrameReader(bytes, "build_hypothesis");
        reader.RequireSchema(BuildHypothesisCanonicalSchema);
        var result = new LlmBuildHypothesisV1(
            reader.ReadString("subject_kind"),
            reader.ReadString("subject_id"),
            reader.ReadString("relation"),
            reader.ReadString("target_kind"),
            reader.ReadString("target_id"),
            reader.ReadStrings("evidence_refs"),
            reader.ReadDouble("confidence"));
        reader.RequireEnd();
        return result;
    }

    private static LlmRunReportResponseV1 DecodeRunReport(byte[] bytes)
    {
        var reader = new CanonicalFrameReader(bytes, "run_report");
        reader.RequireSchema(RunReportCanonicalSchema);
        var desireRetrospective = reader.ReadString("desire_retrospective");
        var payoffOrNearMiss = reader.ReadString("payoff_or_near_miss");
        var nextConcept = reader.ReadString("next_concept");
        var complaints = reader.ReadStrings("complaints");
        var sentenceCount = reader.ReadCount("evaluation_sentence_count");
        var sentences = new LlmEvaluationSentenceV1[sentenceCount];
        for (var index = 0; index < sentences.Length; index++)
        {
            sentences[index] = DecodeEvaluationSentence(reader.ReadBytes($"evaluation_sentences[{index}]"));
        }

        var retryIntent = reader.ReadString("retry_intent");
        reader.RequireEnd();
        return new LlmRunReportResponseV1(
            desireRetrospective,
            payoffOrNearMiss,
            nextConcept,
            complaints,
            sentences,
            retryIntent);
    }

    private static LlmEvaluationSentenceV1 DecodeEvaluationSentence(byte[] bytes)
    {
        var reader = new CanonicalFrameReader(bytes, "evaluation_sentence");
        reader.RequireSchema(EvaluationSentenceCanonicalSchema);
        var result = new LlmEvaluationSentenceV1(
            reader.ReadString("sentence"),
            reader.ReadStrings("telemetry_event_ids"));
        reader.RequireEnd();
        return result;
    }

    private static bool IsCanonicalParseFailure(Exception exception)
        => exception is ArgumentException
            or FormatException
            or InvalidOperationException
            or ArithmeticException;

    private static bool SameBytes(byte[] left, byte[] right)
        => left != null
           && right != null
           && left.SequenceEqual(right);

    private static SealedDecisionEntry CloneEntry(SealedDecisionEntry entry)
        => new(
            new SealedDecisionSeamKey(
                entry.SeamKey.DecisionIndex,
                entry.SeamKey.SeamType,
                entry.SeamKey.Ordinal),
            entry.PreStateHash,
            CloneBytes(entry.ObservationCanonicalBytes),
            entry.ObservationHash,
            entry.LegalActionSetHash,
            entry.HistoryPrefixHash,
            CloneBytes(entry.RequestCanonicalBytes),
            entry.RequestHash,
            CloneBytes(entry.ResponseCanonicalBytes),
            entry.ResponseHash,
            entry.SelectedAction,
            entry.AppliedActionHash,
            entry.ResultEventHash,
            entry.PostStateHash,
            entry.PreviousEntryHash,
            entry.TerminalFailure);

    private static byte[] CloneBytes(byte[] value)
        => value == null ? null : (byte[])value.Clone();

    private static string Format(SealedDecisionSeamKey key)
        => key == null
            ? "<none>"
            : $"{key.DecisionIndex}:{key.SeamType}:{key.Ordinal}";

    public enum DivergenceReason
    {
        SealedEntriesMissing = 0,
        SealedEntryMissing,
        SeamKeyMismatch,
        EntryKindMismatch,
        RequestCanonicalBytesMismatch,
        ResponsePayloadMissing,
        ResponseHashMismatch,
        ResponseCanonicalBytesInvalid,
        UnexpectedExtraRequest,
        UnconsumedEntries,
    }

    /// <summary>Typed fail-closed signal for sealed-source order, consumption, request, or response divergence.</summary>
    public sealed class DivergenceException : InvalidOperationException
    {
        public DivergenceException(
            DivergenceReason reason,
            int entryIndex,
            SealedDecisionSeamKey expectedSeamKey,
            SealedDecisionSeamKey actualSeamKey,
            string detail,
            Exception innerException = null)
            : base(
                $"Sealed trace divergence [{reason}] at entry {entryIndex}: {detail}; "
                + $"expected={Format(expectedSeamKey)} actual={Format(actualSeamKey)}",
                innerException)
        {
            Reason = reason;
            EntryIndex = entryIndex;
            ExpectedSeamKey = expectedSeamKey;
            ActualSeamKey = actualSeamKey;
            Detail = detail ?? string.Empty;
        }

        public DivergenceReason Reason { get; }
        public int EntryIndex { get; }
        public SealedDecisionSeamKey ExpectedSeamKey { get; }
        public SealedDecisionSeamKey ActualSeamKey { get; }
        public string Detail { get; }
    }

    private sealed class CanonicalFrameReader
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        private readonly byte[] _bytes;
        private readonly string _scope;
        private int _offset;

        public CanonicalFrameReader(byte[] bytes, string scope)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        public void RequireSchema(string expected)
        {
            var actual = ReadString("schema");
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new FormatException($"{_scope}.schema expected '{expected}' but found '{actual}'.");
            }
        }

        public byte[] ReadBytes(string field)
        {
            if (_offset >= _bytes.Length)
            {
                throw new FormatException($"{_scope}.{field} frame is missing.");
            }

            var lengthStart = _offset;
            long length = 0;
            var digitCount = 0;
            while (_offset < _bytes.Length && _bytes[_offset] != (byte)':')
            {
                var value = _bytes[_offset];
                if (value < (byte)'0' || value > (byte)'9')
                {
                    throw new FormatException($"{_scope}.{field} frame length is not ASCII decimal.");
                }

                if (digitCount == 0 && value == (byte)'0'
                    && _offset + 1 < _bytes.Length
                    && _bytes[_offset + 1] != (byte)':')
                {
                    throw new FormatException($"{_scope}.{field} frame length has a leading zero.");
                }

                checked
                {
                    length = (length * 10) + (value - (byte)'0');
                }

                digitCount++;
                _offset++;
            }

            if (digitCount == 0 || _offset >= _bytes.Length || _bytes[_offset] != (byte)':')
            {
                throw new FormatException($"{_scope}.{field} frame length delimiter is missing at {lengthStart}.");
            }

            _offset++;
            if (length > int.MaxValue || length > _bytes.Length - _offset - 1L)
            {
                throw new FormatException($"{_scope}.{field} frame payload length exceeds remaining bytes.");
            }

            var result = new byte[(int)length];
            Buffer.BlockCopy(_bytes, _offset, result, 0, result.Length);
            _offset += result.Length;
            if (_offset >= _bytes.Length || _bytes[_offset] != (byte)'|')
            {
                throw new FormatException($"{_scope}.{field} frame terminator is missing.");
            }

            _offset++;
            return result;
        }

        public string ReadString(string field)
        {
            try
            {
                return StrictUtf8.GetString(ReadBytes(field));
            }
            catch (DecoderFallbackException exception)
            {
                throw new FormatException($"{_scope}.{field} is not strict UTF-8.", exception);
            }
        }

        public int ReadCount(string field)
        {
            var value = ReadString(field);
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
                || result < 0
                || !string.Equals(
                    value,
                    result.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                throw new FormatException($"{_scope}.{field} is not a canonical non-negative integer.");
            }

            return result;
        }

        public double ReadDouble(string field)
        {
            var value = ReadString(field);
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                || double.IsNaN(result)
                || double.IsInfinity(result)
                || !string.Equals(value, result.ToString("R", CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                throw new FormatException($"{_scope}.{field} is not a canonical finite round-trip number.");
            }

            return result;
        }

        public IReadOnlyList<string> ReadStrings(string field)
        {
            var count = ReadCount($"{field}.count");
            var result = new string[count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = ReadString($"{field}[{index}]");
            }

            return result;
        }

        public void RequireEnd()
        {
            if (_offset != _bytes.Length)
            {
                throw new FormatException(
                    $"{_scope} canonical frame has {_bytes.Length - _offset} trailing bytes.");
            }
        }
    }
}
