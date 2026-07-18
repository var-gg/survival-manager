using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

/// <summary>SealedDecisionTraceV1 canonical hash와 strict replay-match fail-closed 계약.</summary>
[Category("FastUnit")]
public sealed class SealedDecisionTraceFastTests
{
    public enum EntryMutation
    {
        SeamKey,
        PreStateHash,
        ObservationCanonicalBytes,
        ObservationHash,
        LegalActionSetHash,
        HistoryPrefixHash,
        RequestCanonicalBytes,
        RequestHash,
        ResponseCanonicalBytes,
        ResponseHash,
        SelectedAction,
        AppliedActionHash,
        ResultEventHash,
        PostStateHash,
        PreviousEntryHash,
        TerminalFailure,
    }

    [Test]
    public void Verify_IdenticalSealedAndReplayedTrace_PassesAtOne()
    {
        var sealedTrace = Trace();
        var replayedTrace = Trace();

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.True);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(1d));
        Assert.That(result.SealedEntryCount, Is.EqualTo(2));
        Assert.That(result.ReplayedEntryCount, Is.EqualTo(2));
        Assert.That(result.ComparedEntryCount, Is.EqualTo(2));
        Assert.That(result.MatchedEntryCount, Is.EqualTo(2));
        Assert.That(result.UnmatchedEntryCount, Is.Zero);
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.None));
        Assert.That(result.FirstDivergenceIndex, Is.EqualTo(-1));
    }

    [TestCase(EntryMutation.SeamKey, SealedDecisionTraceReplayDivergenceReason.MissingEntry)]
    [TestCase(EntryMutation.PreStateHash, SealedDecisionTraceReplayDivergenceReason.PreStateHashMismatch)]
    [TestCase(EntryMutation.ObservationCanonicalBytes, SealedDecisionTraceReplayDivergenceReason.ObservationCanonicalBytesMismatch)]
    [TestCase(EntryMutation.ObservationHash, SealedDecisionTraceReplayDivergenceReason.ObservationHashMismatch)]
    [TestCase(EntryMutation.LegalActionSetHash, SealedDecisionTraceReplayDivergenceReason.LegalActionSetHashMismatch)]
    [TestCase(EntryMutation.HistoryPrefixHash, SealedDecisionTraceReplayDivergenceReason.HistoryPrefixHashMismatch)]
    [TestCase(EntryMutation.RequestCanonicalBytes, SealedDecisionTraceReplayDivergenceReason.RequestCanonicalBytesMismatch)]
    [TestCase(EntryMutation.RequestHash, SealedDecisionTraceReplayDivergenceReason.RequestHashMismatch)]
    [TestCase(EntryMutation.ResponseCanonicalBytes, SealedDecisionTraceReplayDivergenceReason.ResponseCanonicalBytesMismatch)]
    [TestCase(EntryMutation.ResponseHash, SealedDecisionTraceReplayDivergenceReason.ResponseHashMismatch)]
    [TestCase(EntryMutation.SelectedAction, SealedDecisionTraceReplayDivergenceReason.SelectedActionMismatch)]
    [TestCase(EntryMutation.AppliedActionHash, SealedDecisionTraceReplayDivergenceReason.AppliedActionHashMismatch)]
    [TestCase(EntryMutation.ResultEventHash, SealedDecisionTraceReplayDivergenceReason.ResultEventHashMismatch)]
    [TestCase(EntryMutation.PostStateHash, SealedDecisionTraceReplayDivergenceReason.PostStateHashMismatch)]
    [TestCase(EntryMutation.PreviousEntryHash, SealedDecisionTraceReplayDivergenceReason.BrokenPreviousEntryHashChain)]
    [TestCase(EntryMutation.TerminalFailure, SealedDecisionTraceReplayDivergenceReason.TerminalFailureMismatch)]
    public void Verify_AnySingleEntryFieldMismatch_FailsWithExactReason(
        EntryMutation mutation,
        SealedDecisionTraceReplayDivergenceReason expectedReason)
    {
        var sealedTrace = Trace();
        var replayedTrace = MutateLast(sealedTrace, mutation);

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(0.5d));
        Assert.That(result.MatchedEntryCount, Is.EqualTo(1));
        Assert.That(result.UnmatchedEntryCount, Is.EqualTo(1));
        Assert.That(result.FirstDivergenceIndex, Is.EqualTo(1));
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(expectedReason));
    }

    [Test]
    public void Verify_MissingEntry_FailsAsUnconsumedSealedEntry()
    {
        var sealedTrace = Trace();
        var replayedTrace = sealedTrace with { Entries = sealedTrace.Entries.Take(1).ToArray() };

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(0.5d));
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.MissingEntry));
        Assert.That(result.FirstDivergenceDetail, Does.Contain("not consumed"));
    }

    [Test]
    public void Verify_ExtraEntry_FailsAsUnconsumedReplayOutput()
    {
        var sealedTrace = Trace();
        var replayedEntries = sealedTrace.Entries.ToList();
        replayedEntries.Add(Entry(2, SealedDecisionTraceHash.ComputeEntryHash(replayedEntries[^1])));
        var replayedTrace = sealedTrace with { Entries = replayedEntries };

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(2d / 3d).Within(1e-12));
        Assert.That(result.ReplayedEntryCount, Is.EqualTo(3));
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.ExtraEntry));
    }

    [Test]
    public void Verify_DuplicateSeamKey_FailsEvenWhenPrefixMatches()
    {
        var sealedTrace = Trace();
        var duplicate = sealedTrace.Entries[^1] with
        {
            PreviousEntryHash = SealedDecisionTraceHash.ComputeEntryHash(sealedTrace.Entries[^1]),
        };
        var replayedTrace = sealedTrace with
        {
            Entries = sealedTrace.Entries.Concat(new[] { duplicate }).ToArray(),
        };

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.FirstDivergenceIndex, Is.EqualTo(2));
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.DuplicateSeamKey));
    }

    [Test]
    public void Verify_SameEntriesInDifferentOrder_FailsAsOutOfOrder()
    {
        var sealedTrace = Trace();
        var replayedTrace = sealedTrace with
        {
            Entries = new[] { sealedTrace.Entries[1], sealedTrace.Entries[0] },
        };

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.Zero);
        Assert.That(result.FirstDivergenceIndex, Is.Zero);
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.OutOfOrderEntry));
    }

    [Test]
    public void Verify_BrokenPreviousEntryHashChain_FailsClosed()
    {
        var sealedTrace = Trace();
        var broken = ReplaceLast(sealedTrace, entry => entry with { PreviousEntryHash = "not-the-previous-entry-hash" });

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, broken);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(0.5d));
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.BrokenPreviousEntryHashChain));
    }

    [Test]
    public void Verify_HeaderConfigurationMismatch_FailsBeforeEntryComparison()
    {
        var sealedTrace = Trace();
        var replayedTrace = sealedTrace with
        {
            Header = sealedTrace.Header with { ContentManifestHash = "different-content" },
        };

        var result = SealedDecisionTraceReplayVerifier.Verify(sealedTrace, replayedTrace);

        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.SealedLlmDecisionTraceReplayMatchRate, Is.Zero);
        Assert.That(result.MatchedEntryCount, Is.Zero);
        Assert.That(result.FirstDivergenceIndex, Is.EqualTo(-1));
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.HeaderMismatch));
        Assert.That(result.FirstDivergenceDetail, Does.Contain("content_manifest_hash"));
    }

    [Test]
    public void Hash_CanonicalBytesEntryAndManifest_AreByteDeterministic()
    {
        var first = Trace();
        var second = Trace();

        Assert.That(
            SealedDecisionTraceHash.GetHeaderCanonicalBytes(first.Header),
            Is.EqualTo(SealedDecisionTraceHash.GetHeaderCanonicalBytes(second.Header)));
        Assert.That(
            SealedDecisionTraceHash.GetEntryCanonicalBytes(first.Entries[0]),
            Is.EqualTo(SealedDecisionTraceHash.GetEntryCanonicalBytes(second.Entries[0])));
        Assert.That(
            SealedDecisionTraceHash.ComputeEntryHash(first.Entries[0]),
            Is.EqualTo(SealedDecisionTraceHash.ComputeEntryHash(second.Entries[0])));
        Assert.That(
            SealedDecisionTraceHash.ComputeManifest(first),
            Is.EqualTo(SealedDecisionTraceHash.ComputeManifest(second)));
    }

    [Test]
    public void Hash_SharedCodec_PreservesReplayHashKnownVector()
    {
        Assert.That(ReplayHash.Compute("state", "activity"), Is.EqualTo("e3babef904bc0308"));
    }

    [Test]
    public void Hash_ManifestPreservesSemanticEntryOrder()
    {
        var trace = Trace();
        var reordered = trace with { Entries = new[] { trace.Entries[1], trace.Entries[0] } };

        Assert.That(
            SealedDecisionTraceHash.ComputeManifest(reordered),
            Is.Not.EqualTo(SealedDecisionTraceHash.ComputeManifest(trace)));
    }

    [Test]
    public void Hash_FirstEntryPreviousHash_IsHeaderHashSentinel()
    {
        var trace = Trace();

        Assert.That(
            trace.Entries[0].PreviousEntryHash,
            Is.EqualTo(SealedDecisionTraceHash.ComputeHeaderHash(trace.Header)));

        var emptySentinel = trace with
        {
            Entries = new[]
            {
                trace.Entries[0] with { PreviousEntryHash = string.Empty },
                trace.Entries[1],
            },
        };
        var result = SealedDecisionTraceReplayVerifier.Verify(trace, emptySentinel);
        Assert.That(result.VerificationPassed, Is.False);
        Assert.That(result.FirstDivergenceReason, Is.EqualTo(SealedDecisionTraceReplayDivergenceReason.BrokenPreviousEntryHashChain));
    }

    [TestCase(SealedDecisionTraceCaptureSource.SyntheticStandIn, false)]
    [TestCase(SealedDecisionTraceCaptureSource.LiveColdStartLlm, true)]
    public void CaptureSource_HardLocksCertificationEligibility(
        SealedDecisionTraceCaptureSource captureSource,
        bool expectedEligible)
    {
        var trace = Trace(captureSource);

        Assert.That(trace.Header.CertificationEligible, Is.EqualTo(expectedEligible));
        Assert.That(trace.CertificationEligible, Is.EqualTo(expectedEligible));
        Assert.That(SealedDecisionTraceReplayVerifier.Verify(trace, Trace(captureSource)).VerificationPassed, Is.True);
    }

    [Test]
    public void TerminalFailure_IsHashedAndCanRepresentCompletedFailureRun()
    {
        var normal = Trace();
        var terminal = ReplaceLast(normal, entry => entry with { TerminalFailure = true });

        Assert.That(
            SealedDecisionTraceHash.ComputeEntryHash(terminal.Entries[^1]),
            Is.Not.EqualTo(SealedDecisionTraceHash.ComputeEntryHash(normal.Entries[^1])));
        Assert.That(SealedDecisionTraceReplayVerifier.Verify(terminal, terminal).VerificationPassed, Is.True);
    }

    private static SealedDecisionTraceV1 Trace(
        SealedDecisionTraceCaptureSource captureSource = SealedDecisionTraceCaptureSource.LiveColdStartLlm)
    {
        var header = new SealedDecisionTraceHeader(
            SealedDecisionTraceV1.SchemaVersion,
            "run-001",
            "scenario-alpha",
            1701,
            "build-manifest",
            "content-manifest",
            "visible-surface",
            "prompt-schema",
            "scorer-config",
            captureSource);
        var first = Entry(0, SealedDecisionTraceHash.ComputeHeaderHash(header));
        var second = Entry(1, SealedDecisionTraceHash.ComputeEntryHash(first));
        return new SealedDecisionTraceV1(header, new[] { first, second });
    }

    private static SealedDecisionEntry Entry(int index, string previousEntryHash)
    {
        var observation = Bytes($"{{\"decision\":{index},\"surface\":\"visible\"}}");
        var request = Bytes($"{{\"history\":{index},\"request\":\"choose\"}}");
        var response = Bytes($"{{\"selected_action\":\"action-{index}\"}}");
        return new SealedDecisionEntry(
            new SealedDecisionSeamKey(index, "campaign-choice", 0),
            $"pre-state-{index}",
            observation,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(observation),
            $"legal-actions-{index}",
            $"history-prefix-{index}",
            request,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(request),
            response,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(response),
            $"action-{index}",
            $"applied-action-{index}",
            $"result-event-{index}",
            $"post-state-{index}",
            previousEntryHash,
            TerminalFailure: false);
    }

    private static SealedDecisionTraceV1 MutateLast(SealedDecisionTraceV1 trace, EntryMutation mutation)
    {
        return ReplaceLast(trace, entry =>
        {
            switch (mutation)
            {
                case EntryMutation.SeamKey:
                    return entry with { SeamKey = entry.SeamKey with { DecisionIndex = 99 } };
                case EntryMutation.PreStateHash:
                    return entry with { PreStateHash = "different-pre-state" };
                case EntryMutation.ObservationCanonicalBytes:
                {
                    var bytes = Bytes("different-observation");
                    return entry with
                    {
                        ObservationCanonicalBytes = bytes,
                        ObservationHash = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(bytes),
                    };
                }
                case EntryMutation.ObservationHash:
                    return entry with { ObservationHash = "invalid-observation-hash" };
                case EntryMutation.LegalActionSetHash:
                    return entry with { LegalActionSetHash = "different-legal-actions" };
                case EntryMutation.HistoryPrefixHash:
                    return entry with { HistoryPrefixHash = "different-history-prefix" };
                case EntryMutation.RequestCanonicalBytes:
                {
                    var bytes = Bytes("different-request");
                    return entry with
                    {
                        RequestCanonicalBytes = bytes,
                        RequestHash = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(bytes),
                    };
                }
                case EntryMutation.RequestHash:
                    return entry with { RequestHash = "invalid-request-hash" };
                case EntryMutation.ResponseCanonicalBytes:
                {
                    var bytes = Bytes("different-response");
                    return entry with
                    {
                        ResponseCanonicalBytes = bytes,
                        ResponseHash = SealedDecisionTraceHash.ComputeCanonicalPayloadHash(bytes),
                    };
                }
                case EntryMutation.ResponseHash:
                    return entry with { ResponseHash = "invalid-response-hash" };
                case EntryMutation.SelectedAction:
                    return entry with { SelectedAction = "different-action" };
                case EntryMutation.AppliedActionHash:
                    return entry with { AppliedActionHash = "different-applied-action" };
                case EntryMutation.ResultEventHash:
                    return entry with { ResultEventHash = "different-result-event" };
                case EntryMutation.PostStateHash:
                    return entry with { PostStateHash = "different-post-state" };
                case EntryMutation.PreviousEntryHash:
                    return entry with { PreviousEntryHash = "broken-chain" };
                case EntryMutation.TerminalFailure:
                    return entry with { TerminalFailure = !entry.TerminalFailure };
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        });
    }

    private static SealedDecisionTraceV1 ReplaceLast(
        SealedDecisionTraceV1 trace,
        Func<SealedDecisionEntry, SealedDecisionEntry> replace)
    {
        var entries = trace.Entries.ToArray();
        entries[^1] = replace(entries[^1]);
        return trace with { Entries = entries };
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
