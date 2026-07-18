using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>
/// SEALED trace와 재도출된 REPLAYED trace를 exact order/exact byte 기준으로 검증한다.
/// 누락·중복·순서 변경·미소비 entry·payload hash 위조·chain 단절은 모두 fail closed한다.
/// </summary>
public static class SealedDecisionTraceReplayVerifier
{
    public static SealedDecisionTraceReplayResult Verify(
        SealedDecisionTraceV1? sealedTrace,
        SealedDecisionTraceV1? replayedTrace)
    {
        if (sealedTrace == null)
        {
            return FailureWithoutComparison(
                0,
                replayedTrace?.Entries?.Count ?? 0,
                SealedDecisionTraceReplayDivergenceReason.SealedTraceMissing,
                "sealed trace is missing");
        }

        if (replayedTrace == null)
        {
            return FailureWithoutComparison(
                sealedTrace.Entries?.Count ?? 0,
                0,
                SealedDecisionTraceReplayDivergenceReason.ReplayedTraceMissing,
                "replayed trace is missing");
        }

        var sealedCount = sealedTrace.Entries?.Count ?? 0;
        var replayedCount = replayedTrace.Entries?.Count ?? 0;
        var comparedCount = Math.Max(sealedCount, replayedCount);

        var headerIssue = FindHeaderIssue(sealedTrace.Header, replayedTrace.Header);
        if (headerIssue != null)
        {
            return BuildResult(false, 0, sealedCount, replayedCount, comparedCount, headerIssue);
        }

        var shapeIssue = FindShapeIssue(sealedTrace.Entries, replayedTrace.Entries);
        var evaluation = EvaluateEntries(sealedTrace, replayedTrace);
        var firstIssue = Earlier(shapeIssue, evaluation.FirstIssue);

        if (firstIssue == null)
        {
            var sealedManifest = SealedDecisionTraceHash.ComputeManifest(sealedTrace);
            var replayedManifest = SealedDecisionTraceHash.ComputeManifest(replayedTrace);
            if (!Same(sealedManifest, replayedManifest))
            {
                firstIssue = new Divergence(
                    0,
                    2,
                    SealedDecisionTraceReplayDivergenceReason.TraceManifestHashMismatch,
                    "whole-trace manifest hashes differ");
            }
        }

        var passed = firstIssue == null && evaluation.MatchedCount == comparedCount;
        return BuildResult(
            passed,
            evaluation.MatchedCount,
            sealedCount,
            replayedCount,
            comparedCount,
            firstIssue);
    }

    private static SealedDecisionTraceReplayResult FailureWithoutComparison(
        int sealedCount,
        int replayedCount,
        SealedDecisionTraceReplayDivergenceReason reason,
        string detail)
    {
        var compared = Math.Max(sealedCount, replayedCount);
        return new SealedDecisionTraceReplayResult(
            false,
            0d,
            sealedCount,
            replayedCount,
            compared,
            0,
            compared,
            -1,
            reason,
            detail);
    }

    private static SealedDecisionTraceReplayResult BuildResult(
        bool passed,
        int matchedCount,
        int sealedCount,
        int replayedCount,
        int comparedCount,
        Divergence? issue)
    {
        var rate = comparedCount == 0
            ? passed ? 1d : 0d
            : (double)matchedCount / comparedCount;
        return new SealedDecisionTraceReplayResult(
            passed,
            rate,
            sealedCount,
            replayedCount,
            comparedCount,
            matchedCount,
            comparedCount - matchedCount,
            issue?.Index ?? -1,
            issue?.Reason ?? SealedDecisionTraceReplayDivergenceReason.None,
            issue?.Detail ?? string.Empty);
    }

    private static Divergence? FindHeaderIssue(
        SealedDecisionTraceHeader? sealedHeader,
        SealedDecisionTraceHeader? replayedHeader)
    {
        if (sealedHeader == null || replayedHeader == null)
        {
            return HeaderIssue(
                SealedDecisionTraceReplayDivergenceReason.HeaderMissing,
                sealedHeader == null ? "sealed header is missing" : "replayed header is missing");
        }

        var sealedMissing = FirstMissingHeaderField(sealedHeader);
        var replayedMissing = FirstMissingHeaderField(replayedHeader);
        if (sealedMissing != null || replayedMissing != null)
        {
            return HeaderIssue(
                SealedDecisionTraceReplayDivergenceReason.MissingHeaderField,
                sealedMissing != null
                    ? $"sealed header field {sealedMissing} is null"
                    : $"replayed header field {replayedMissing} is null");
        }

        if (!Same(sealedHeader.TraceSchemaVersion, SealedDecisionTraceV1.SchemaVersion)
            || !Same(replayedHeader.TraceSchemaVersion, SealedDecisionTraceV1.SchemaVersion))
        {
            return HeaderIssue(
                SealedDecisionTraceReplayDivergenceReason.InvalidTraceSchemaVersion,
                $"expected {SealedDecisionTraceV1.SchemaVersion}");
        }

        if (!IsKnownCaptureSource(sealedHeader.CaptureSource)
            || !IsKnownCaptureSource(replayedHeader.CaptureSource))
        {
            return HeaderIssue(
                SealedDecisionTraceReplayDivergenceReason.InvalidCaptureSource,
                "capture_source is outside the locked enum");
        }

        if (!Same(sealedHeader.RunId, replayedHeader.RunId)) return HeaderMismatch("run_id");
        if (!Same(sealedHeader.ScenarioId, replayedHeader.ScenarioId)) return HeaderMismatch("scenario_id");
        if (sealedHeader.CampaignSeed != replayedHeader.CampaignSeed) return HeaderMismatch("campaign_seed");
        if (!Same(sealedHeader.BuildManifestHash, replayedHeader.BuildManifestHash)) return HeaderMismatch("build_manifest_hash");
        if (!Same(sealedHeader.ContentManifestHash, replayedHeader.ContentManifestHash)) return HeaderMismatch("content_manifest_hash");
        if (!Same(sealedHeader.VisibleSurfaceHash, replayedHeader.VisibleSurfaceHash)) return HeaderMismatch("visible_surface_hash");
        if (!Same(sealedHeader.PromptSchemaHash, replayedHeader.PromptSchemaHash)) return HeaderMismatch("prompt_schema_hash");
        if (!Same(sealedHeader.ScorerConfigHash, replayedHeader.ScorerConfigHash)) return HeaderMismatch("scorer_config_hash");
        if (sealedHeader.CaptureSource != replayedHeader.CaptureSource) return HeaderMismatch("capture_source");
        return null;
    }

    private static Divergence? FindShapeIssue(
        IReadOnlyList<SealedDecisionEntry>? sealedEntries,
        IReadOnlyList<SealedDecisionEntry>? replayedEntries)
    {
        if (sealedEntries == null || replayedEntries == null)
        {
            return new Divergence(
                0,
                0,
                SealedDecisionTraceReplayDivergenceReason.MissingEntryData,
                sealedEntries == null ? "sealed entries collection is null" : "replayed entries collection is null");
        }

        var sealedKeys = new HashSet<SealedDecisionSeamKey>();
        for (var index = 0; index < sealedEntries.Count; index++)
        {
            var entry = sealedEntries[index];
            if (entry?.SeamKey == null || entry.SeamKey.SeamType == null)
            {
                return EntryDataMissing(index, "sealed entry or seam_key is null");
            }

            if (!sealedKeys.Add(entry.SeamKey))
            {
                return new Divergence(
                    index,
                    0,
                    SealedDecisionTraceReplayDivergenceReason.DuplicateSeamKey,
                    $"sealed trace duplicates {Format(entry.SeamKey)}");
            }
        }

        var replayedKeys = new HashSet<SealedDecisionSeamKey>();
        for (var index = 0; index < replayedEntries.Count; index++)
        {
            var entry = replayedEntries[index];
            if (entry?.SeamKey == null || entry.SeamKey.SeamType == null)
            {
                return EntryDataMissing(index, "replayed entry or seam_key is null");
            }

            if (!replayedKeys.Add(entry.SeamKey))
            {
                return new Divergence(
                    index,
                    0,
                    SealedDecisionTraceReplayDivergenceReason.DuplicateSeamKey,
                    $"replayed trace duplicates {Format(entry.SeamKey)}");
            }
        }

        for (var index = 0; index < sealedEntries.Count; index++)
        {
            var key = sealedEntries[index].SeamKey;
            if (!replayedKeys.Contains(key))
            {
                return new Divergence(
                    index,
                    0,
                    SealedDecisionTraceReplayDivergenceReason.MissingEntry,
                    $"sealed entry {Format(key)} was not consumed by replay");
            }
        }

        for (var index = 0; index < replayedEntries.Count; index++)
        {
            var key = replayedEntries[index].SeamKey;
            if (!sealedKeys.Contains(key))
            {
                return new Divergence(
                    index,
                    0,
                    SealedDecisionTraceReplayDivergenceReason.ExtraEntry,
                    $"replay produced unexpected entry {Format(key)}");
            }
        }

        for (var index = 0; index < sealedEntries.Count; index++)
        {
            if (!Equals(sealedEntries[index].SeamKey, replayedEntries[index].SeamKey))
            {
                return new Divergence(
                    index,
                    0,
                    SealedDecisionTraceReplayDivergenceReason.OutOfOrderEntry,
                    $"expected {Format(sealedEntries[index].SeamKey)} but replay consumed {Format(replayedEntries[index].SeamKey)}");
            }
        }

        return null;
    }

    private static EntryEvaluation EvaluateEntries(
        SealedDecisionTraceV1 sealedTrace,
        SealedDecisionTraceV1 replayedTrace)
    {
        var sealedEntries = sealedTrace.Entries;
        var replayedEntries = replayedTrace.Entries;
        if (sealedEntries == null || replayedEntries == null)
        {
            return new EntryEvaluation(0, null);
        }

        var matched = 0;
        Divergence? firstIssue = null;
        var commonCount = Math.Min(sealedEntries.Count, replayedEntries.Count);
        for (var index = 0; index < commonCount; index++)
        {
            var sealedIntegrity = ValidateEntryIntegrity(sealedTrace, index, "sealed");
            var replayedIntegrity = ValidateEntryIntegrity(replayedTrace, index, "replayed");
            var comparison = CompareEntries(sealedEntries[index], replayedEntries[index], index);
            var entryIssue = Earlier(Earlier(sealedIntegrity, replayedIntegrity), comparison);
            if (entryIssue == null)
            {
                matched++;
            }
            else
            {
                firstIssue = Earlier(firstIssue, entryIssue);
            }
        }

        return new EntryEvaluation(matched, firstIssue);
    }

    private static Divergence? ValidateEntryIntegrity(
        SealedDecisionTraceV1 trace,
        int index,
        string lane)
    {
        var entry = trace.Entries[index];
        var missingField = FirstMissingEntryField(entry);
        if (missingField != null)
        {
            return EntryDataMissing(index, $"{lane} entry field {missingField} is null", 1);
        }

        if (!Same(entry.ObservationHash, SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.ObservationCanonicalBytes)))
        {
            return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ObservationHashMismatch,
                $"{lane} observation_hash does not hash observation_canonical_bytes", 1);
        }

        if (!Same(entry.RequestHash, SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.RequestCanonicalBytes)))
        {
            return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.RequestHashMismatch,
                $"{lane} request_hash does not hash request_canonical_bytes", 1);
        }

        if (!Same(entry.ResponseHash, SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.ResponseCanonicalBytes)))
        {
            return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ResponseHashMismatch,
                $"{lane} response_hash does not hash response_canonical_bytes", 1);
        }

        string expectedPreviousHash;
        try
        {
            expectedPreviousHash = index == 0
                ? SealedDecisionTraceHash.ComputeHeaderHash(trace.Header)
                : SealedDecisionTraceHash.ComputeEntryHash(trace.Entries[index - 1]);
        }
        catch (ArgumentException exception)
        {
            return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.BrokenPreviousEntryHashChain,
                $"{lane} previous entry cannot be hashed: {exception.Message}", 1);
        }

        if (!Same(entry.PreviousEntryHash, expectedPreviousHash))
        {
            return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.BrokenPreviousEntryHashChain,
                $"{lane} previous_entry_hash is not the expected {(index == 0 ? "header" : "entry")} hash", 1);
        }

        return null;
    }

    private static Divergence? CompareEntries(
        SealedDecisionEntry? sealedEntry,
        SealedDecisionEntry? replayedEntry,
        int index)
    {
        if (sealedEntry == null || replayedEntry == null)
        {
            return EntryDataMissing(index, sealedEntry == null ? "sealed entry is null" : "replayed entry is null", 2);
        }

        if (!Equals(sealedEntry.SeamKey, replayedEntry.SeamKey)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.SeamKeyMismatch, "seam_key differs");
        if (!Same(sealedEntry.PreStateHash, replayedEntry.PreStateHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.PreStateHashMismatch, "pre_state_hash differs");
        if (!SameBytes(sealedEntry.ObservationCanonicalBytes, replayedEntry.ObservationCanonicalBytes)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ObservationCanonicalBytesMismatch, "observation_canonical_bytes differ");
        if (!Same(sealedEntry.ObservationHash, replayedEntry.ObservationHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ObservationHashMismatch, "observation_hash differs");
        if (!Same(sealedEntry.LegalActionSetHash, replayedEntry.LegalActionSetHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.LegalActionSetHashMismatch, "legal_action_set_hash differs");
        if (!Same(sealedEntry.HistoryPrefixHash, replayedEntry.HistoryPrefixHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.HistoryPrefixHashMismatch, "history_prefix_hash differs");
        if (!SameBytes(sealedEntry.RequestCanonicalBytes, replayedEntry.RequestCanonicalBytes)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.RequestCanonicalBytesMismatch, "request_canonical_bytes differ");
        if (!Same(sealedEntry.RequestHash, replayedEntry.RequestHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.RequestHashMismatch, "request_hash differs");
        if (!SameBytes(sealedEntry.ResponseCanonicalBytes, replayedEntry.ResponseCanonicalBytes)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ResponseCanonicalBytesMismatch, "response_canonical_bytes differ");
        if (!Same(sealedEntry.ResponseHash, replayedEntry.ResponseHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ResponseHashMismatch, "response_hash differs");
        if (!Same(sealedEntry.SelectedAction, replayedEntry.SelectedAction)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.SelectedActionMismatch, "selected_action differs");
        if (!Same(sealedEntry.AppliedActionHash, replayedEntry.AppliedActionHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.AppliedActionHashMismatch, "applied_action_hash differs");
        if (!Same(sealedEntry.ResultEventHash, replayedEntry.ResultEventHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.ResultEventHashMismatch, "result_event_hash differs");
        if (!Same(sealedEntry.PostStateHash, replayedEntry.PostStateHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.PostStateHashMismatch, "post_state_hash differs");
        if (!Same(sealedEntry.PreviousEntryHash, replayedEntry.PreviousEntryHash)) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.PreviousEntryHashMismatch, "previous_entry_hash differs");
        if (sealedEntry.TerminalFailure != replayedEntry.TerminalFailure) return FieldIssue(index, SealedDecisionTraceReplayDivergenceReason.TerminalFailureMismatch, "terminal_failure differs");
        return null;
    }

    private static string? FirstMissingHeaderField(SealedDecisionTraceHeader header)
    {
        if (header.TraceSchemaVersion == null) return "trace_schema_version";
        if (header.RunId == null) return "run_id";
        if (header.ScenarioId == null) return "scenario_id";
        if (header.BuildManifestHash == null) return "build_manifest_hash";
        if (header.ContentManifestHash == null) return "content_manifest_hash";
        if (header.VisibleSurfaceHash == null) return "visible_surface_hash";
        if (header.PromptSchemaHash == null) return "prompt_schema_hash";
        if (header.ScorerConfigHash == null) return "scorer_config_hash";
        return null;
    }

    private static string? FirstMissingEntryField(SealedDecisionEntry? entry)
    {
        if (entry == null) return "entry";
        if (entry.SeamKey == null) return "seam_key";
        if (entry.SeamKey.SeamType == null) return "seam_key.seam_type";
        if (entry.PreStateHash == null) return "pre_state_hash";
        if (entry.ObservationCanonicalBytes == null) return "observation_canonical_bytes";
        if (entry.ObservationHash == null) return "observation_hash";
        if (entry.LegalActionSetHash == null) return "legal_action_set_hash";
        if (entry.HistoryPrefixHash == null) return "history_prefix_hash";
        if (entry.RequestCanonicalBytes == null) return "request_canonical_bytes";
        if (entry.RequestHash == null) return "request_hash";
        if (entry.ResponseCanonicalBytes == null) return "response_canonical_bytes";
        if (entry.ResponseHash == null) return "response_hash";
        if (entry.SelectedAction == null) return "selected_action";
        if (entry.AppliedActionHash == null) return "applied_action_hash";
        if (entry.ResultEventHash == null) return "result_event_hash";
        if (entry.PostStateHash == null) return "post_state_hash";
        if (entry.PreviousEntryHash == null) return "previous_entry_hash";
        return null;
    }

    private static bool IsKnownCaptureSource(SealedDecisionTraceCaptureSource source)
        => source is SealedDecisionTraceCaptureSource.SyntheticStandIn
            or SealedDecisionTraceCaptureSource.LiveColdStartLlm;

    private static bool Same(string? left, string? right)
        => string.Equals(left, right, StringComparison.Ordinal);

    private static bool SameBytes(byte[]? left, byte[]? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index]) return false;
        }

        return true;
    }

    private static Divergence HeaderMismatch(string field)
        => HeaderIssue(SealedDecisionTraceReplayDivergenceReason.HeaderMismatch, $"header field {field} differs");

    private static Divergence HeaderIssue(SealedDecisionTraceReplayDivergenceReason reason, string detail)
        => new(-1, 0, reason, detail);

    private static Divergence EntryDataMissing(int index, string detail, int priority = 0)
        => new(index, priority, SealedDecisionTraceReplayDivergenceReason.MissingEntryData, detail);

    private static Divergence FieldIssue(
        int index,
        SealedDecisionTraceReplayDivergenceReason reason,
        string detail,
        int priority = 2)
        => new(index, priority, reason, detail);

    private static Divergence? Earlier(Divergence? left, Divergence? right)
    {
        if (left == null) return right;
        if (right == null) return left;
        if (left.Index != right.Index) return left.Index < right.Index ? left : right;
        return left.Priority <= right.Priority ? left : right;
    }

    private static string Format(SealedDecisionSeamKey key)
        => $"{key.DecisionIndex}:{key.SeamType}:{key.Ordinal}";

    private sealed record Divergence(
        int Index,
        int Priority,
        SealedDecisionTraceReplayDivergenceReason Reason,
        string Detail);

    private sealed record EntryEvaluation(int MatchedCount, Divergence? FirstIssue);
}
