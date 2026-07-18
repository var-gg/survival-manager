using System;
using System.Collections.Generic;
using SM.HeadlessMetrics;

namespace SM.SealedLlmBridge;

/// <summary>
/// Owns the single in-flight offer/response exchange and seals it into the exact
/// <see cref="SealedDecisionTraceReplayVerifier"/> hash-chain convention.
/// Index zero uses the header hash; every later history/previous hash is the canonical hash of the prior entry.
/// </summary>
public sealed class SealedDecisionTraceBuilder
{
    /// <summary>No game action was applied after an invalid selected_action.</summary>
    public const string TerminalAppliedActionHashSentinel = "sealed-terminal-failure:no-applied-action";

    /// <summary>No game result event exists after an invalid selected_action.</summary>
    public const string TerminalResultEventHashSentinel = "sealed-terminal-failure:no-result-event";

    public const string RunReportLegalActionSetHashSentinel = "sealed-run-report:no-legal-action-set";
    public const string RunReportSelectedActionSentinel = "sealed-run-report:no-selected-action";
    public const string RunReportAppliedActionHashSentinel = "sealed-run-report:no-applied-action";
    public const string RunReportResultEventHashSentinel = "sealed-run-report:report-recorded";

    private readonly SealedDecisionTraceHeader _header;
    private readonly string _headerHash;
    private readonly List<SealedDecisionEntry> _entries = new();
    private PendingDecision _pending;
    private bool _terminalFailureSealed;
    private bool _runReportSealed;

    public SealedDecisionTraceBuilder(
        string runId,
        string scenarioId,
        int campaignSeed,
        string buildManifestHash,
        string contentManifestHash,
        string visibleSurfaceHash,
        string promptSchemaHash,
        string scorerConfigHash,
        SealedDecisionTraceCaptureSource captureSource)
    {
        if (captureSource is not SealedDecisionTraceCaptureSource.SyntheticStandIn
            and not SealedDecisionTraceCaptureSource.LiveColdStartLlm)
        {
            throw new ArgumentOutOfRangeException(nameof(captureSource), captureSource, "Unknown capture source.");
        }

        _header = new SealedDecisionTraceHeader(
            SealedDecisionTraceV1.SchemaVersion,
            RequireValue(runId, nameof(runId)),
            RequireValue(scenarioId, nameof(scenarioId)),
            campaignSeed,
            RequireValue(buildManifestHash, nameof(buildManifestHash)),
            RequireValue(contentManifestHash, nameof(contentManifestHash)),
            RequireValue(visibleSurfaceHash, nameof(visibleSurfaceHash)),
            RequireValue(promptSchemaHash, nameof(promptSchemaHash)),
            RequireValue(scorerConfigHash, nameof(scorerConfigHash)),
            captureSource);
        _headerHash = SealedDecisionTraceHash.ComputeHeaderHash(_header);
    }

    /// <summary>
    /// Hash visible to the next request. Before the first entry this is the header hash; afterwards it is the
    /// immediately preceding entry hash, which is also that next entry's PreviousEntryHash.
    /// </summary>
    public string CurrentHistoryPrefixHash
        => _entries.Count == 0
            ? _headerHash
            : SealedDecisionTraceHash.ComputeEntryHash(_entries[_entries.Count - 1]);

    public string PromptSchemaHash => _header.PromptSchemaHash;
    public SealedDecisionSeamKey PendingSeamKey => _pending?.SeamKey;
    public bool PendingTerminalFailure => _pending?.TerminalFailure == true;

    internal SealedDecisionSeamKey Begin(
        string seamType,
        byte[] observationCanonicalBytes,
        string observationHash,
        string legalActionSetHash,
        string historyPrefixHash,
        byte[] requestCanonicalBytes,
        string requestHash)
    {
        EnsureNormalDecisionMayBegin();
        if (string.IsNullOrWhiteSpace(seamType)
            || string.Equals(seamType, SealedLlmSeamTypes.RunReport, StringComparison.Ordinal))
        {
            throw new ArgumentException("A normal decision requires a non-run_report seam type.", nameof(seamType));
        }

        var expectedHistory = CurrentHistoryPrefixHash;
        if (!string.Equals(historyPrefixHash, expectedHistory, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Decision history prefix drifted before Begin.");
        }

        var observationBytes = Clone(
            observationCanonicalBytes ?? throw new ArgumentNullException(nameof(observationCanonicalBytes)));
        var requestBytes = Clone(
            requestCanonicalBytes ?? throw new ArgumentNullException(nameof(requestCanonicalBytes)));
        RequirePayloadHash(observationBytes, observationHash, nameof(observationHash));
        RequirePayloadHash(requestBytes, requestHash, nameof(requestHash));

        var seamKey = new SealedDecisionSeamKey(_entries.Count, seamType, 0);
        _pending = new PendingDecision(
            seamKey,
            observationBytes,
            observationHash,
            RequireValue(legalActionSetHash, nameof(legalActionSetHash)),
            historyPrefixHash,
            requestBytes,
            requestHash);
        return seamKey;
    }

    internal void RecordResponse(
        SealedDecisionSeamKey seamKey,
        byte[] responseCanonicalBytes,
        string responseHash,
        string selectedAction)
    {
        var pending = RequirePending(seamKey);
        if (pending.ResponseCanonicalBytes != null)
        {
            throw new InvalidOperationException("The in-flight decision already has a response.");
        }

        var responseBytes = Clone(
            responseCanonicalBytes ?? throw new ArgumentNullException(nameof(responseCanonicalBytes)));
        RequirePayloadHash(responseBytes, responseHash, nameof(responseHash));
        pending.ResponseCanonicalBytes = responseBytes;
        pending.ResponseHash = responseHash;
        pending.SelectedAction = selectedAction
                                 ?? throw new ArgumentNullException(nameof(selectedAction));
    }

    internal void MarkTerminalFailure(SealedDecisionSeamKey seamKey)
    {
        var pending = RequirePending(seamKey);
        RequireRecordedResponse(pending);
        pending.TerminalFailure = true;
    }

    public SealedDecisionEntry Complete(
        SealedDecisionSeamKey seamKey,
        string preStateHash,
        string appliedActionHash,
        string resultEventHash,
        string postStateHash)
    {
        var pending = RequirePending(seamKey);
        RequireRecordedResponse(pending);
        if (pending.TerminalFailure)
        {
            throw new InvalidOperationException(
                "A terminal-failure pending exchange must be sealed with CompleteTerminalFailure.");
        }

        var entry = CreatePendingEntry(
            pending,
            RequireValue(preStateHash, nameof(preStateHash)),
            RequireValue(appliedActionHash, nameof(appliedActionHash)),
            RequireValue(resultEventHash, nameof(resultEventHash)),
            RequireValue(postStateHash, nameof(postStateHash)),
            terminalFailure: false);
        Append(entry);
        _pending = null;
        return CloneEntry(entry);
    }

    public SealedDecisionEntry CompleteTerminalFailure(
        SealedDecisionSeamKey seamKey,
        string preStateHash)
    {
        var pending = RequirePending(seamKey);
        RequireRecordedResponse(pending);
        if (!pending.TerminalFailure)
        {
            throw new InvalidOperationException(
                "CompleteTerminalFailure requires a pending exchange marked by decode or guard failure.");
        }

        var stateHash = RequireValue(preStateHash, nameof(preStateHash));
        var entry = CreatePendingEntry(
            pending,
            stateHash,
            TerminalAppliedActionHashSentinel,
            TerminalResultEventHashSentinel,
            stateHash,
            terminalFailure: true);
        Append(entry);
        _pending = null;
        _terminalFailureSealed = true;
        return CloneEntry(entry);
    }

    /// <summary>
    /// Seals the one allowed final entry after normal completion or terminal failure. Run reports have no game
    /// observation, legal menu, selected action, or applied action, so their canonical entry uses the declared
    /// run-report sentinels and preserves the supplied final state on both sides of the seam.
    /// </summary>
    public SealedDecisionEntry SealRunReport(
        SealedLlmRunReportRequest request,
        byte[] responseCanonicalBytes,
        string stateHash)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (_pending != null)
        {
            throw new InvalidOperationException("Cannot seal a run report while a decision exchange is pending.");
        }

        if (_runReportSealed)
        {
            throw new InvalidOperationException("The terminal run report has already been sealed.");
        }

        var expectedKey = NextRunReportSeamKey();
        if (!Equals(request.SeamKey, expectedKey))
        {
            throw new InvalidOperationException(
                $"Run-report seam drift: expected {Format(expectedKey)}, got {Format(request.SeamKey)}.");
        }

        var requestBytes = Clone(request.RequestCanonicalBytes);
        var responseBytes = Clone(
            responseCanonicalBytes ?? throw new ArgumentNullException(nameof(responseCanonicalBytes)));
        var finalStateHash = RequireValue(stateHash, nameof(stateHash));
        var historyPrefixHash = CurrentHistoryPrefixHash;
        var emptyObservation = Array.Empty<byte>();
        var entry = new SealedDecisionEntry(
            expectedKey,
            finalStateHash,
            emptyObservation,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(emptyObservation),
            RunReportLegalActionSetHashSentinel,
            historyPrefixHash,
            requestBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(requestBytes),
            responseBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(responseBytes),
            RunReportSelectedActionSentinel,
            RunReportAppliedActionHashSentinel,
            RunReportResultEventHashSentinel,
            finalStateHash,
            historyPrefixHash,
            TerminalFailure: false);
        Append(entry);
        _runReportSealed = true;
        return CloneEntry(entry);
    }

    public SealedDecisionTraceV1 Build()
    {
        if (_pending != null)
        {
            throw new InvalidOperationException(
                $"Cannot build a trace while {Format(_pending.SeamKey)} is pending.");
        }

        var entries = new SealedDecisionEntry[_entries.Count];
        for (var index = 0; index < _entries.Count; index++)
        {
            entries[index] = CloneEntry(_entries[index]);
        }

        return new SealedDecisionTraceV1(_header, entries);
    }

    internal SealedDecisionSeamKey NextRunReportSeamKey()
    {
        if (_pending != null)
        {
            throw new InvalidOperationException("A decision exchange is still pending.");
        }

        if (_runReportSealed)
        {
            throw new InvalidOperationException("The terminal run report has already been sealed.");
        }

        return new SealedDecisionSeamKey(_entries.Count, SealedLlmSeamTypes.RunReport, 0);
    }

    private void EnsureNormalDecisionMayBegin()
    {
        if (_pending != null)
        {
            throw new InvalidOperationException(
                $"Decision {Format(_pending.SeamKey)} must be completed before another Begin.");
        }

        if (_terminalFailureSealed)
        {
            throw new InvalidOperationException(
                "The trace is terminal after failure; only the final run_report may be sealed.");
        }

        if (_runReportSealed)
        {
            throw new InvalidOperationException("The trace is terminal after its run_report.");
        }
    }

    private PendingDecision RequirePending(SealedDecisionSeamKey seamKey)
    {
        if (_pending == null)
        {
            throw new InvalidOperationException("No decision exchange is pending.");
        }

        if (!Equals(_pending.SeamKey, seamKey))
        {
            throw new InvalidOperationException(
                $"Pending seam drift: expected {Format(_pending.SeamKey)}, got {Format(seamKey)}.");
        }

        return _pending;
    }

    private SealedDecisionEntry CreatePendingEntry(
        PendingDecision pending,
        string preStateHash,
        string appliedActionHash,
        string resultEventHash,
        string postStateHash,
        bool terminalFailure)
    {
        var previousEntryHash = CurrentHistoryPrefixHash;
        if (!string.Equals(pending.HistoryPrefixHash, previousEntryHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Pending history prefix drifted before Complete.");
        }

        return new SealedDecisionEntry(
            pending.SeamKey,
            preStateHash,
            Clone(pending.ObservationCanonicalBytes),
            pending.ObservationHash,
            pending.LegalActionSetHash,
            pending.HistoryPrefixHash,
            Clone(pending.RequestCanonicalBytes),
            pending.RequestHash,
            Clone(pending.ResponseCanonicalBytes),
            pending.ResponseHash,
            pending.SelectedAction,
            appliedActionHash,
            resultEventHash,
            postStateHash,
            previousEntryHash,
            terminalFailure);
    }

    private void Append(SealedDecisionEntry entry)
    {
        var expectedPrevious = CurrentHistoryPrefixHash;
        if (!string.Equals(entry.PreviousEntryHash, expectedPrevious, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Entry previous hash does not match the current chain head.");
        }

        _entries.Add(CloneEntry(entry));
    }

    private static void RequireRecordedResponse(PendingDecision pending)
    {
        if (pending.ResponseCanonicalBytes == null
            || pending.ResponseHash == null
            || pending.SelectedAction == null)
        {
            throw new InvalidOperationException("The pending decision has no canonical response to seal.");
        }
    }

    private static void RequirePayloadHash(byte[] bytes, string hash, string argumentName)
    {
        var expected = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(bytes);
        if (!string.Equals(hash, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException("Canonical payload hash does not match its bytes.", argumentName);
        }
    }

    private static string RequireValue(string value, string argumentName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A stable non-empty value is required.", argumentName);
        }

        return value;
    }

    private static byte[] Clone(byte[] value)
        => (byte[])value.Clone();

    private static SealedDecisionEntry CloneEntry(SealedDecisionEntry entry)
        => new(
            entry.SeamKey,
            entry.PreStateHash,
            Clone(entry.ObservationCanonicalBytes),
            entry.ObservationHash,
            entry.LegalActionSetHash,
            entry.HistoryPrefixHash,
            Clone(entry.RequestCanonicalBytes),
            entry.RequestHash,
            Clone(entry.ResponseCanonicalBytes),
            entry.ResponseHash,
            entry.SelectedAction,
            entry.AppliedActionHash,
            entry.ResultEventHash,
            entry.PostStateHash,
            entry.PreviousEntryHash,
            entry.TerminalFailure);

    private static string Format(SealedDecisionSeamKey seamKey)
        => seamKey == null
            ? "<null>"
            : $"{seamKey.DecisionIndex}:{seamKey.SeamType}:{seamKey.Ordinal}";

    private sealed class PendingDecision
    {
        public PendingDecision(
            SealedDecisionSeamKey seamKey,
            byte[] observationCanonicalBytes,
            string observationHash,
            string legalActionSetHash,
            string historyPrefixHash,
            byte[] requestCanonicalBytes,
            string requestHash)
        {
            SeamKey = seamKey;
            ObservationCanonicalBytes = observationCanonicalBytes;
            ObservationHash = observationHash;
            LegalActionSetHash = legalActionSetHash;
            HistoryPrefixHash = historyPrefixHash;
            RequestCanonicalBytes = requestCanonicalBytes;
            RequestHash = requestHash;
        }

        public SealedDecisionSeamKey SeamKey { get; }
        public byte[] ObservationCanonicalBytes { get; }
        public string ObservationHash { get; }
        public string LegalActionSetHash { get; }
        public string HistoryPrefixHash { get; }
        public byte[] RequestCanonicalBytes { get; }
        public string RequestHash { get; }
        public byte[] ResponseCanonicalBytes { get; set; }
        public string ResponseHash { get; set; }
        public string SelectedAction { get; set; }
        public bool TerminalFailure { get; set; }
    }
}
