using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.HeadlessMetrics;

namespace SM.Tests.EditMode;

/// <summary>BT1 aggregate가 persisted trace bytes와 verifier 결과만으로 fail-closed 집계되는지 검증한다.</summary>
[Category("FastUnit")]
public sealed class Bt1ReplayAggregateFastTests
{
    private static readonly string Bt1SpecPath = Path.Combine(
        "Assets", "_Game", "Scripts", "Runtime", "HeadlessMetrics", "h100-gates-bt1-v1.json");

    [Test]
    public void ThreeDistinctByteIdenticalReplays_SupplyPassingBt1Observations()
    {
        var sealedTrace = Trace();
        var canonicalBytes = CanonicalBytes(sealedTrace);
        var replays = new[]
        {
            Input(Trace(), canonicalBytes, 101, "tr-TR"),
            Input(Trace(), canonicalBytes, 202, "de-DE"),
            Input(Trace(), canonicalBytes, 303, "ja-JP"),
        };

        var aggregate = Bt1ReplayAggregate.Aggregate(sealedTrace, canonicalBytes, replays);
        var observations = aggregate.ToBt1Observations();

        Assert.That(aggregate.IndependentProcessReplayCount, Is.EqualTo(3));
        Assert.That(aggregate.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(1d));
        Assert.That(aggregate.StateEventResultHashMatchRate, Is.EqualTo(1d));
        Assert.That(aggregate.DistinctAppliedCultureCount, Is.EqualTo(3));
        Assert.That(aggregate.AllByteIdentical, Is.True);
        Assert.That(observations.Select(value => value.MetricId), Is.EqualTo(new[]
        {
            "independent_process_replay_count",
            "state_event_result_hash_match_rate",
            "sealed_llm_decision_trace_replay_match_rate",
        }));
        Assert.That(observations.Select(value => value.Value), Is.EqualTo(new[] { 3d, 1d, 1d }));
        Assert.That(observations.Select(value => value.SampleCount), Is.EqualTo(new[] { 3, 6, 6 }));

        var spec = H100Bt1GateSpec.LoadFromFile(Bt1SpecPath);
        var bt1 = H100Bt1GateEvaluator.Generate(spec, observations)
            .Gates.Single(gate => gate.GateId == "BT1");
        Assert.That(bt1.EvaluableNow, Is.False);
        Assert.That(bt1.Status, Is.EqualTo("pass"));
        Assert.That(bt1.Pass, Is.True);
    }

    [Test]
    public void MutatedReplay_CollapsesStrictRateAndLowersStateEventRate()
    {
        var sealedTrace = Trace();
        var canonicalBytes = CanonicalBytes(sealedTrace);
        var mutatedEntries = sealedTrace.Entries.ToArray();
        mutatedEntries[^1] = mutatedEntries[^1] with { PostStateHash = "mutated-post-state" };
        var mutatedTrace = sealedTrace with { Entries = mutatedEntries };
        var replays = new[]
        {
            Input(Trace(), canonicalBytes, 101, "tr-TR"),
            Input(Trace(), canonicalBytes, 202, "de-DE"),
            Input(mutatedTrace, CanonicalBytes(mutatedTrace), 303, "ja-JP"),
        };

        var aggregate = Bt1ReplayAggregate.Aggregate(sealedTrace, canonicalBytes, replays);

        Assert.That(aggregate.AllByteIdentical, Is.False);
        Assert.That(aggregate.SealedLlmDecisionTraceReplayMatchRate, Is.Zero);
        Assert.That(aggregate.IndependentProcessReplayCount, Is.EqualTo(2));
        Assert.That(aggregate.StateEventResultHashMatchRate, Is.EqualTo(5d / 6d).Within(1e-12));
        Assert.That(aggregate.PerReplay[^1].StateEventResultMatchedEntryCount, Is.EqualTo(1));
    }

    [Test]
    public void DuplicateProcessIds_CountAsOneIndependentReplay()
    {
        var sealedTrace = Trace();
        var canonicalBytes = CanonicalBytes(sealedTrace);
        var replays = new[]
        {
            Input(Trace(), canonicalBytes, 101, "tr-TR"),
            Input(Trace(), canonicalBytes, 101, "de-DE"),
        };

        var aggregate = Bt1ReplayAggregate.Aggregate(sealedTrace, canonicalBytes, replays);

        Assert.That(aggregate.IndependentProcessReplayCount, Is.EqualTo(1));
        Assert.That(aggregate.SealedLlmDecisionTraceReplayMatchRate, Is.EqualTo(1d));
        Assert.That(aggregate.StateEventResultHashMatchRate, Is.EqualTo(1d));
    }

    [Test]
    public void EmptyReplayList_ReturnsFailingZeroMetricsWithoutThrowing()
    {
        var sealedTrace = Trace();

        var aggregate = Bt1ReplayAggregate.Aggregate(
            sealedTrace,
            CanonicalBytes(sealedTrace),
            Array.Empty<Bt1ReplayInput>());

        Assert.That(aggregate.IndependentProcessReplayCount, Is.Zero);
        Assert.That(aggregate.SealedLlmDecisionTraceReplayMatchRate, Is.Zero);
        Assert.That(aggregate.StateEventResultHashMatchRate, Is.Zero);
        Assert.That(aggregate.DistinctAppliedCultureCount, Is.Zero);
        Assert.That(aggregate.AllByteIdentical, Is.False);
        Assert.That(aggregate.PerReplay, Is.Empty);
        Assert.That(aggregate.ToBt1Observations().Select(value => value.SampleCount),
            Is.EqualTo(new[] { 0, 0, 0 }));
    }

    private static Bt1ReplayInput Input(
        SealedDecisionTraceV1 trace,
        byte[] canonicalBytes,
        int processId,
        string culture)
        => new(
            trace,
            canonicalBytes,
            new Bt1ReplayEnvFingerprint(
                culture,
                "UTC",
                "persisted-working-directory",
                processId,
                "persisted-machine",
                "untrusted-self-reported-sealed-manifest",
                "untrusted-self-reported-rebuilt-manifest"));

    private static SealedDecisionTraceV1 Trace()
    {
        var header = new SealedDecisionTraceHeader(
            SealedDecisionTraceV1.SchemaVersion,
            "run-bt1",
            "scenario-bt1",
            1701,
            "build-manifest",
            "content-manifest",
            "visible-surface",
            "prompt-schema",
            "scorer-config",
            SealedDecisionTraceCaptureSource.LiveColdStartLlm);
        var first = Entry(0, SealedDecisionTraceHash.ComputeHeaderHash(header));
        var second = Entry(1, SealedDecisionTraceHash.ComputeEntryHash(first));
        return new SealedDecisionTraceV1(header, new[] { first, second });
    }

    private static SealedDecisionEntry Entry(int index, string previousEntryHash)
    {
        var observation = Encoding.UTF8.GetBytes($"observation-{index}");
        var request = Encoding.UTF8.GetBytes($"request-{index}");
        var response = Encoding.UTF8.GetBytes($"response-{index}");
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

    private static byte[] CanonicalBytes(SealedDecisionTraceV1 trace)
        => new UTF8Encoding(false).GetBytes(HeadlessMetricJson.Serialize(trace) + "\n");
}
