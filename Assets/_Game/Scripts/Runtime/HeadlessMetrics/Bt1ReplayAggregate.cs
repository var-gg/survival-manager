using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>독립 replay process가 persisted witness로 남긴 환경 fingerprint.</summary>
public sealed record Bt1ReplayEnvFingerprint(
    string AppliedCulture,
    string TimeZoneId,
    string CurrentDirectory,
    int ProcessId,
    string MachineName,
    string SealedManifestHash,
    string RebuiltManifestHash);

/// <summary>disk에서 다시 읽은 rebuilt trace와 exact persisted bytes.</summary>
public sealed record Bt1ReplayInput(
    SealedDecisionTraceV1 RebuiltTrace,
    byte[] RebuiltTraceCanonicalBytes,
    Bt1ReplayEnvFingerprint Fingerprint);

/// <summary>BT1 replay metric을 persisted trace bytes에서 독립 재도출한 결과.</summary>
public sealed record Bt1ReplayAggregate(
    int IndependentProcessReplayCount,
    double SealedLlmDecisionTraceReplayMatchRate,
    double StateEventResultHashMatchRate,
    int DistinctAppliedCultureCount,
    bool AllByteIdentical,
    IReadOnlyList<Bt1ReplayEvidence> PerReplay)
{
    public static Bt1ReplayAggregate Aggregate(
        SealedDecisionTraceV1 sealedTrace,
        byte[] sealedTraceCanonicalBytes,
        IReadOnlyList<Bt1ReplayInput> replays)
    {
        if (replays == null || replays.Count == 0)
        {
            return new Bt1ReplayAggregate(
                0,
                0d,
                0d,
                0,
                false,
                Array.Empty<Bt1ReplayEvidence>());
        }

        var perReplay = new Bt1ReplayEvidence[replays.Count];
        var totalComparedEntries = 0;
        var totalStateEventResultMatchedEntries = 0;
        for (var index = 0; index < replays.Count; index++)
        {
            var replay = replays[index];
            var verification = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replay.RebuiltTrace);
            var byteIdentical = sealedTraceCanonicalBytes.AsSpan()
                .SequenceEqual(replay.RebuiltTraceCanonicalBytes.AsSpan());
            var stateEventResultMatchedEntryCount = CountStateEventResultMatches(
                sealedTrace,
                replay.RebuiltTrace);

            totalComparedEntries += verification.ComparedEntryCount;
            totalStateEventResultMatchedEntries += stateEventResultMatchedEntryCount;
            perReplay[index] = new Bt1ReplayEvidence(
                replay.Fingerprint,
                verification.VerificationPassed,
                byteIdentical,
                verification.SealedLlmDecisionTraceReplayMatchRate,
                verification.ComparedEntryCount,
                verification.MatchedEntryCount,
                stateEventResultMatchedEntryCount,
                verification.FirstDivergenceReason.ToString(),
                verification.FirstDivergenceDetail);
        }

        var passingReplays = perReplay
            .Where(replay => replay.VerificationPassed && replay.ByteIdentical)
            .ToArray();
        var sealedReplayMatchRate = passingReplays.Length == perReplay.Length
            ? perReplay.Min(replay => replay.ComparedEntryCount == 0
                ? 0d
                : (double)replay.MatchedEntryCount / replay.ComparedEntryCount)
            : 0d;
        var stateEventResultHashMatchRate = totalComparedEntries == 0
            ? 0d
            : (double)totalStateEventResultMatchedEntries / totalComparedEntries;

        return new Bt1ReplayAggregate(
            passingReplays.Select(replay => replay.Fingerprint.ProcessId).Distinct().Count(),
            sealedReplayMatchRate,
            stateEventResultHashMatchRate,
            passingReplays.Select(replay => replay.Fingerprint.AppliedCulture)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            perReplay.Length > 0 && perReplay.All(replay => replay.ByteIdentical),
            perReplay);
    }

    public IReadOnlyList<H100GateEvaluator.ExternalObservation> ToBt1Observations()
    {
        var replays = PerReplay ?? Array.Empty<Bt1ReplayEvidence>();
        var totalComparedEntries = replays.Sum(replay => replay.ComparedEntryCount);
        var totalMatchedEntries = replays.Sum(replay => replay.MatchedEntryCount);
        var totalStateEventResultMatchedEntries = replays.Sum(
            replay => replay.StateEventResultMatchedEntryCount);
        var verifiedByteIdenticalReplayCount = replays.Count(
            replay => replay.VerificationPassed && replay.ByteIdentical);

        return new[]
        {
            new H100GateEvaluator.ExternalObservation(
                "independent_process_replay_count",
                IndependentProcessReplayCount,
                IndependentProcessReplayCount,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt1-replay-witness.json distinct_process_ids={0} verified_byte_identical_replays={1}/{2}",
                    IndependentProcessReplayCount,
                    verifiedByteIdenticalReplayCount,
                    replays.Count)),
            new H100GateEvaluator.ExternalObservation(
                "state_event_result_hash_match_rate",
                StateEventResultHashMatchRate,
                totalComparedEntries,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt1-replay-witness.json state_event_result_entries={0}/{1}",
                    totalStateEventResultMatchedEntries,
                    totalComparedEntries)),
            new H100GateEvaluator.ExternalObservation(
                "sealed_llm_decision_trace_replay_match_rate",
                SealedLlmDecisionTraceReplayMatchRate,
                totalComparedEntries,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "bt1-replay-witness.json strict_matched_entries={0}/{1} all_byte_identical={2}",
                    totalMatchedEntries,
                    totalComparedEntries,
                    AllByteIdentical)),
        };
    }

    private static int CountStateEventResultMatches(
        SealedDecisionTraceV1 sealedTrace,
        SealedDecisionTraceV1 replayedTrace)
    {
        if (sealedTrace?.Entries == null || replayedTrace?.Entries == null)
        {
            return 0;
        }

        var matched = 0;
        var commonCount = Math.Min(sealedTrace.Entries.Count, replayedTrace.Entries.Count);
        for (var index = 0; index < commonCount; index++)
        {
            var sealedEntry = sealedTrace.Entries[index];
            var replayedEntry = replayedTrace.Entries[index];
            if (sealedEntry == null || replayedEntry == null)
            {
                continue;
            }

            if (string.Equals(sealedEntry.PreStateHash, replayedEntry.PreStateHash, StringComparison.Ordinal)
                && string.Equals(sealedEntry.PostStateHash, replayedEntry.PostStateHash, StringComparison.Ordinal)
                && string.Equals(sealedEntry.ResultEventHash, replayedEntry.ResultEventHash, StringComparison.Ordinal))
            {
                matched++;
            }
        }

        return matched;
    }
}

/// <summary>각 replay의 verifier 재평가와 exact byte/state-event witness.</summary>
public sealed record Bt1ReplayEvidence(
    Bt1ReplayEnvFingerprint Fingerprint,
    bool VerificationPassed,
    bool ByteIdentical,
    double MatchRate,
    int ComparedEntryCount,
    int MatchedEntryCount,
    int StateEventResultMatchedEntryCount,
    string FirstDivergenceReason,
    string FirstDivergenceDetail);
