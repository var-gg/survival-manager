using System;
using System.Collections.Generic;
using System.Linq;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.SealedLlmBridge;

namespace SM.Tests.EditMode;

public sealed record Bt5Bt10SyntheticRunOptions
{
    public SealedDecisionTraceCaptureSource CaptureSource { get; init; }
        = SealedDecisionTraceCaptureSource.LiveColdStartLlm;

    public string TrackTokenOverride { get; init; }
    public string TelemetryEventIdOverride { get; init; }
    public bool IncludeEvaluationSentence { get; init; } = true;
    public IReadOnlyList<string> Complaints { get; init; } = Array.Empty<string>();
    public PlayerVisibleTimelinePoint FactObservedAt { get; init; } = new(0, 0, 0);
    public PlayerVisibleTimelinePoint DecisionAt { get; init; } = new(0, 0, 0);
    public string BuildManifestHash { get; init; } = "build-manifest";
    public string PromptSchemaHash { get; init; } = "prompt-schema";
}

public sealed record Bt5Bt10SyntheticRunFixture(
    SealedDecisionTraceV1 Trace,
    IReadOnlyList<PlayerVisibleFactRecord> Facts,
    IReadOnlyList<PlayerVisibleDecisionRecord> Decisions,
    SealedScoredRunInput Input);

/// <summary>실 LLM 실행 없이 scorer/runner를 닫는 6-run synthetic persisted-byte fixture.</summary>
public static class Bt5Bt10SyntheticFixture
{
    public static IReadOnlyList<Bt5Bt10SyntheticRunFixture> CreateCohort(
        Func<int, Bt5Bt10SyntheticRunOptions> configure = null)
        => Enumerable.Range(0, 6)
            .Select(index => CreateRun(index, configure?.Invoke(index)))
            .ToArray();

    public static Bt5Bt10SyntheticRunFixture CreateRun(
        int index,
        Bt5Bt10SyntheticRunOptions options = null)
    {
        options ??= new Bt5Bt10SyntheticRunOptions();
        var runId = $"bt5-bt10-run-{index}";
        var campaignId = $"campaign-{index}";
        var archetypeId = $"recruit-{index}";
        var nextSkillId = $"next-skill-{index}";
        var fact = PlayerVisibleFactRecord.Create(
            runId,
            campaignId,
            options.FactObservedAt,
            PlayerVisibleUiSource.TownRoster,
            archetypeId,
            "is offered",
            nextSkillId,
            "visible recruit option",
            "one offer",
            "spend gold",
            $"{archetypeId} offers {nextSkillId}");
        var ledgerDecision = PlayerVisibleDecisionRecord.Create(
            runId,
            campaignId,
            options.DecisionAt,
            "cold-start-llm",
            "recruit",
            "offer:0",
            $"commit to {archetypeId}",
            1d,
            new[] { fact.FactId });

        var observation = new HeadlessRosterPolicyObservation(
            1701 + index,
            "chapter-synthetic",
            "site-synthetic",
            4,
            Array.Empty<HeadlessHeroObservation>(),
            new HeadlessWalletObservation(100, 100),
            new[]
            {
                new HeadlessRecruitOfferObservation(
                    0,
                    archetypeId,
                    "human",
                    "mystic",
                    "support",
                    nextSkillId,
                    $"passive-{index}",
                    5,
                    "Common",
                    "OnPlan",
                    false),
            },
            new[]
            {
                new HeadlessPassiveHeroObservation(
                    $"hero-{index}",
                    2,
                    $"board-{index}",
                    Array.Empty<string>(),
                    3,
                    1,
                    new[]
                    {
                        new HeadlessPassiveBoardObservation(
                            $"board-{index}",
                            new[]
                            {
                                new HeadlessPassiveNodeObservation(
                                    $"node-{index}",
                                    1,
                                    "Keystone",
                                    Array.Empty<string>(),
                                    Array.Empty<string>(),
                                    $"node-skill-{index}",
                                    new[] { "support" },
                                    Array.Empty<HeadlessStatModifierObservation>(),
                                    Array.Empty<HeadlessRuleModifierObservation>()),
                            }),
                    }),
            },
            new[]
            {
                new HeadlessRefitItemObservation(
                    $"item-{index}",
                    $"item-instance-{index}",
                    $"hero-{index}",
                    new[] { "weapon" },
                    "weapon-sword",
                    5,
                    new[] { new HeadlessRefitSlotObservation(0, null, true) }),
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["recruit-offer"] = fact.FactId,
            });
        var observationBytes = SealedLlmObservationCodec.CanonicalBytes(observation);
        var intent = new LlmDeclaredIntentV1(
            $"intent-{index}",
            new[] { options.TrackTokenOverride ?? $"archetype:{archetypeId}" },
            $"make {archetypeId} strengthen the team",
            new[] { fact.FactId },
            $"find {nextSkillId} after this commit",
            new[] { "another visible support recruit" },
            new[] { "pivot if the next offer lacks support" },
            0.8d);
        var decisionResponse = new LlmDecisionResponseV1(
            "0",
            intent,
            intent.IntentId,
            Array.Empty<LlmBuildHypothesisV1>());
        var telemetryId = options.TelemetryEventIdOverride ?? fact.FactId;
        var sentences = options.IncludeEvaluationSentence
            ? new[]
            {
                new LlmEvaluationSentenceV1(
                    $"{archetypeId} was a useful commitment",
                    new[] { telemetryId }),
            }
            : Array.Empty<LlmEvaluationSentenceV1>();
        var runReport = new LlmRunReportResponseV1(
            $"I wanted {archetypeId} before choosing it",
            $"{archetypeId} delivered the expected support payoff",
            $"Next I will try {nextSkillId}",
            options.Complaints ?? Array.Empty<string>(),
            sentences,
            $"retry for {nextSkillId}");

        var header = new SealedDecisionTraceHeader(
            SealedDecisionTraceV1.SchemaVersion,
            runId,
            "synthetic-bt5-bt10",
            7000 + index,
            options.BuildManifestHash,
            "content-manifest",
            "visible-surface",
            options.PromptSchemaHash,
            "scorer-config",
            options.CaptureSource);
        var first = Entry(
            0,
            "recruit",
            observationBytes,
            LlmWireCanonicalSerializer.CanonicalBytes(decisionResponse),
            "0",
            SealedDecisionTraceHash.ComputeHeaderHash(header));
        var reportEntry = Entry(
            1,
            "run_report",
            Array.Empty<byte>(),
            LlmWireCanonicalSerializer.CanonicalBytes(runReport),
            string.Empty,
            SealedDecisionTraceHash.ComputeEntryHash(first));
        var trace = new SealedDecisionTraceV1(header, new[] { first, reportEntry });
        var facts = new[] { fact };
        var decisions = new[] { ledgerDecision };
        return FromTrace(trace, facts, decisions);
    }

    public static Bt5Bt10SyntheticRunFixture FromTrace(
        SealedDecisionTraceV1 trace,
        IReadOnlyList<PlayerVisibleFactRecord> facts,
        IReadOnlyList<PlayerVisibleDecisionRecord> decisions)
    {
        var input = new SealedScoredRunInput(SealedRunDecoder.Decode(trace), facts, decisions);
        return new Bt5Bt10SyntheticRunFixture(trace, facts, decisions, input);
    }

    public static IReadOnlyList<SealedScoredRunInput> Inputs(
        IEnumerable<Bt5Bt10SyntheticRunFixture> fixtures)
        => fixtures.Select(fixture => fixture.Input).ToArray();

    public static OwnerApprovalArtifact OwnerApproval(
        IEnumerable<Bt5Bt10SyntheticRunFixture> fixtures,
        IReadOnlyList<string> overrideHashes = null)
    {
        var hashes = overrideHashes ?? fixtures
            .Select(fixture => SealedDecisionTraceHash.ComputeManifest(fixture.Trace))
            .ToArray();
        return new OwnerApprovalArtifact(
            true,
            "synthetic owner acceptance for scorer fixture",
            "2026-07-19",
            hashes,
            "synthetic-owner-approval.json");
    }

    private static SealedDecisionEntry Entry(
        int decisionIndex,
        string seamType,
        byte[] observationBytes,
        byte[] responseBytes,
        string selectedAction,
        string previousEntryHash)
    {
        var requestBytes = Array.Empty<byte>();
        return new SealedDecisionEntry(
            new SealedDecisionSeamKey(decisionIndex, seamType, 0),
            $"pre-{decisionIndex}",
            observationBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(observationBytes),
            $"legal-{decisionIndex}",
            $"history-{decisionIndex}",
            requestBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(requestBytes),
            responseBytes,
            SealedDecisionTraceHash.ComputeCanonicalPayloadHash(responseBytes),
            selectedAction,
            $"applied-{decisionIndex}",
            $"result-{decisionIndex}",
            $"post-{decisionIndex}",
            previousEntryHash,
            false);
    }
}
