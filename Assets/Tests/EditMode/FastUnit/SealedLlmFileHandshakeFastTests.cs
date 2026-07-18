using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.SealedLlmBridge;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SealedLlmFileHandshakeFastTests
{
    private string _root;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "sm-h100-file-handshake-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void NormalRoundTrip_ReturnsStrictParsedResponseAndWritesPromptRequest()
    {
        var request = DecisionRequest();
        var action = LegalRewardAction(request);
        WriteDecisionResponse(1, request, Decision(action));

        var actual = Source().RequestDecision(request);

        Assert.That(actual.SelectedAction, Is.EqualTo(action));
        Assert.That(File.Exists(Path.Combine(_root, "d000.prompt.md")), Is.True);
        Assert.That(File.Exists(Path.Combine(_root, "d000.request.json")), Is.True);
        Assert.That(Directory.EnumerateFiles(_root, "*.tmp").Any(), Is.False);
    }

    [Test]
    public void StrictRejectThenCorrectedAccept_WritesRejectAndReturnsSecondAnswer()
    {
        var request = DecisionRequest();
        var action = LegalRewardAction(request);
        WriteRawDecisionResponse(1, request, "not-json");
        WriteDecisionResponse(2, request, Decision(action));

        var actual = Source().RequestDecision(request);

        Assert.That(actual.SelectedAction, Is.EqualTo(action));
        var reject = SealedLlmExchangeEnvelope.ReadReject(Path.Combine(_root, "d000.a1.reject.json"));
        Assert.That(reject.ReasonKind, Is.EqualTo(SealedLlmExchangeEnvelope.StrictParseReason));
        Assert.That(reject.NextAttempt, Is.EqualTo(2));
    }

    [Test]
    public void ThreeIllegalAnswers_SealTheRealFinalAnswerAsTerminalFailure()
    {
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var harness = Harness(observation);
        var requestBytes = SealedLlmRequestCodec.RequestCanonicalBytes(
            harness.Manifest,
            SealedLlmObservationCodec.CanonicalBytes(observation),
            harness.Builder.CurrentHistoryPrefixHash);
        var expectedRequest = SealedLlmDecisionRequest.ForPolicy(
            new SealedDecisionSeamKey(0, SealedLlmSeamTypes.Reward, 0),
            requestBytes,
            observation);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            WriteDecisionResponse(attempt, expectedRequest, Decision("999999"));
        }

        var failure = Assert.Throws<SealedLlmTerminalFailureException>(
            () => harness.Bridge.DecideReward(observation));
        Assert.That(failure.InnerException, Is.TypeOf<SealedLlmActionDecodeException>());
        Assert.That(harness.Builder.PendingTerminalFailure, Is.True);
        var pending = harness.Builder.PendingSeamKey;
        var terminal = harness.Builder.CompleteTerminalFailure(pending, "terminal-state");
        Assert.That(terminal.SelectedAction, Is.EqualTo("999999"));
        Assert.That(terminal.TerminalFailure, Is.True);

        var reportBytes = Encoding.UTF8.GetBytes("terminal-report-request");
        WriteRunReportResponse(
            new SealedDecisionSeamKey(1, SealedLlmSeamTypes.RunReport, 0),
            reportBytes,
            RunReport());
        harness.Bridge.SealRunReport(reportBytes, "terminal-state", "terminal_failure");
        var trace = harness.Builder.Build();
        Assert.That(trace.Entries[0].TerminalFailure, Is.True);
        Assert.That(trace.Entries[0].SelectedAction, Is.EqualTo("999999"));
        Assert.That(SealedDecisionTraceReplayVerifier.Verify(trace, trace).VerificationPassed, Is.True);
    }

    [Test]
    public void ThreeUnparseableAnswers_ThrowWithoutSealingOrFabricating()
    {
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var harness = Harness(observation);
        var requestBytes = SealedLlmRequestCodec.RequestCanonicalBytes(
            harness.Manifest,
            SealedLlmObservationCodec.CanonicalBytes(observation),
            harness.Builder.CurrentHistoryPrefixHash);
        var request = SealedLlmDecisionRequest.ForPolicy(
            new SealedDecisionSeamKey(0, SealedLlmSeamTypes.Reward, 0),
            requestBytes,
            observation);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            WriteRawDecisionResponse(attempt, request, "not-json");
        }

        Assert.Throws<InvalidOperationException>(() => harness.Bridge.DecideReward(observation));
        Assert.That(harness.Builder.PendingSeamKey, Is.Not.Null);
        Assert.That(harness.Builder.PendingTerminalFailure, Is.False);
        Assert.Throws<InvalidOperationException>(() => harness.Builder.Build());
    }

    [Test]
    public void StaleRequestHash_AbortsImmediatelyWithoutReject()
    {
        var request = DecisionRequest();
        var action = LegalRewardAction(request);
        var response = new SealedLlmExchangeResponseV1(
            SealedLlmExchangeResponseV1.CurrentSchemaVersion,
            request.SeamKey,
            "stale-hash",
            "scripted-stub",
            LlmWireCanonicalSerializer.CanonicalJson(Decision(action)));
        SealedLlmExchangeEnvelope.WriteResponse(
            Path.Combine(_root, "d000.a1.response.json"),
            response);

        Assert.That(
            Assert.Throws<InvalidOperationException>(() => Source().RequestDecision(request))?.Message,
            Does.Contain("request_canonical_hash mismatch"));
        Assert.That(File.Exists(Path.Combine(_root, "d000.a1.reject.json")), Is.False);
    }

    [Test]
    public void AgentKindGuard_RejectsLiveKindInDryRunMode()
    {
        var request = DecisionRequest();
        var action = LegalRewardAction(request);
        WriteDecisionResponse(1, request, Decision(action), "codex-exec");

        Assert.That(
            Assert.Throws<InvalidOperationException>(() => Source().RequestDecision(request))?.Message,
            Does.Contain("not allowed"));
    }

    [Test]
    public void EnvelopeParser_RejectsUnknownFields()
    {
        var request = DecisionRequest();
        var valid = new SealedLlmExchangeResponseV1(
            SealedLlmExchangeResponseV1.CurrentSchemaVersion,
            request.SeamKey,
            RequestHash(request),
            "scripted-stub",
            "not-json");
        var json = SealedLlmExchangeEnvelope.CanonicalJson(valid);
        var withUnknown = json.Substring(0, json.Length - 1) + ",\"timestamp\":\"forbidden\"}";

        Assert.That(
            () => SealedLlmExchangeEnvelope.ParseResponse(withUnknown),
            Throws.InstanceOf<Exception>());
    }

    private LiveColdStartFileHandshakeSource Source()
        => new(
            _root,
            Manifest(),
            new[] { "scripted-stub" },
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromMilliseconds(2));

    private SealedLlmDecisionRequest DecisionRequest()
        => SealedLlmDecisionRequest.ForPolicy(
            new SealedDecisionSeamKey(0, SealedLlmSeamTypes.Reward, 0),
            Encoding.UTF8.GetBytes("fixed-request"),
            IntentPolicyObservationFixture.CreateRecruitBaseline());

    private static string LegalRewardAction(SealedLlmDecisionRequest request)
        => SealedLlmPromptRenderer.LegalActionKeys(request.SeamKey, request.PolicyObservation).First();

    private void WriteDecisionResponse(
        int attempt,
        SealedLlmDecisionRequest request,
        LlmDecisionResponseV1 response,
        string agentKind = "scripted-stub")
        => WriteRawDecisionResponse(
            attempt,
            request,
            LlmWireCanonicalSerializer.CanonicalJson(response),
            agentKind);

    private void WriteRawDecisionResponse(
        int attempt,
        SealedLlmDecisionRequest request,
        string rawJson,
        string agentKind = "scripted-stub")
    {
        SealedLlmExchangeEnvelope.WriteResponse(
            Path.Combine(_root, $"d000.a{attempt}.response.json"),
            new SealedLlmExchangeResponseV1(
                SealedLlmExchangeResponseV1.CurrentSchemaVersion,
                request.SeamKey,
                RequestHash(request),
                agentKind,
                rawJson));
    }

    private void WriteRunReportResponse(
        SealedDecisionSeamKey seamKey,
        byte[] requestBytes,
        LlmRunReportResponseV1 response)
    {
        SealedLlmExchangeEnvelope.WriteResponse(
            Path.Combine(_root, $"r{seamKey.DecisionIndex:D3}.a1.response.json"),
            new SealedLlmExchangeResponseV1(
                SealedLlmExchangeResponseV1.CurrentSchemaVersion,
                seamKey,
                SealedDecisionTraceHash.ComputeCanonicalPayloadHash(requestBytes),
                "scripted-stub",
                LlmWireCanonicalSerializer.CanonicalJson(response)));
    }

    private static string RequestHash(SealedLlmDecisionRequest request)
        => SealedDecisionTraceHash.ComputeCanonicalPayloadHash(request.RequestCanonicalBytes);

    private CaptureHarness Harness(HeadlessPolicyObservation observation)
    {
        var manifest = Manifest();
        var builder = new SealedDecisionTraceBuilder(
            "file-handshake-test",
            "file-handshake-scenario",
            1701,
            "build-fixed",
            "content-fixed",
            "surface-fixed",
            manifest.PromptSchemaHash,
            "scorer-fixed",
            SealedDecisionTraceCaptureSource.SyntheticStandIn);
        return new CaptureHarness(
            manifest,
            builder,
            new SealedLlmBridgePolicy(Source(), builder, manifest));
    }

    private static LlmDecisionResponseV1 Decision(string selectedAction)
        => new(
            selectedAction,
            new LlmDeclaredIntentV1(
                "stub-intent",
                Array.Empty<string>(),
                "exercise the selected visible option",
                Array.Empty<string>(),
                "inspect the next visible offer",
                Array.Empty<string>(),
                new[] { "pivot if the visible payoff changes" },
                1d),
            "stub-intent-ref",
            Array.Empty<LlmBuildHypothesisV1>());

    private static LlmRunReportResponseV1 RunReport()
        => new(
            "stub desire retrospective",
            "stub payoff or near miss",
            "stub next concept",
            Array.Empty<string>(),
            Array.Empty<LlmEvaluationSentenceV1>(),
            "retry with a different visible concept");

    private static LlmPromptManifestV1 Manifest()
        => new(
            "file-handshake-test-v1",
            "TEST PLACEHOLDER: use the exact legal menu.",
            "TEST PLACEHOLDER: blind player with no tools or hidden state.",
            "scripted-stub/test",
            "mechanism=scripted-stub");

    private sealed record CaptureHarness(
        LlmPromptManifestV1 Manifest,
        SealedDecisionTraceBuilder Builder,
        SealedLlmBridgePolicy Bridge);
}
