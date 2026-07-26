using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using SM.HeadlessMetrics;
using SM.HeadlessPolicies;
using SM.SealedLlmBridge;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SealedLlmCaptureCoreFastTests
{
    [Test]
    public void FiveSeams_DecideThenComplete_MirrorReferenceChoicesAndBuildWellFormedEntries()
    {
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var rosterObservation = CreateRosterObservation(observation.Roster);
        var referenceForSource = new ConceptCommitPolicy(RosterIntent());
        var referenceForExpected = new ConceptCommitPolicy(RosterIntent());
        var harness = CreateHarness(new SyntheticStandInSource(referenceForSource, referenceForSource));
        var driver = new InMemoryDecisionDriver(harness.Bridge, harness.Builder);
        var expectedActions = new List<string>();

        var expectedDeployment = referenceForExpected.DecideDeployment(observation);
        var actualDeployment = driver.DecideDeployment(observation);
        Assert.That(PlacementSignature(actualDeployment), Is.EqualTo(PlacementSignature(expectedDeployment)));
        expectedActions.Add(SealedLlmActionCodec.EncodeDeployment(expectedDeployment));

        var expectedReward = referenceForExpected.DecideReward(observation);
        var actualReward = driver.DecideReward(observation);
        Assert.That(actualReward.OptionIndex, Is.EqualTo(expectedReward.OptionIndex));
        expectedActions.Add(SealedLlmActionCodec.EncodeReward(expectedReward));

        var expectedRecruit = referenceForExpected.DecideRecruit(rosterObservation);
        var actualRecruit = driver.DecideRecruit(rosterObservation);
        Assert.That(actualRecruit.OfferIndex, Is.EqualTo(expectedRecruit.OfferIndex));
        expectedActions.Add(SealedLlmActionCodec.EncodeRecruit(expectedRecruit));

        var expectedPassive = referenceForExpected.DecidePassiveAllocation(rosterObservation);
        var actualPassive = driver.DecidePassive(rosterObservation);
        Assert.That(PassiveSignature(actualPassive), Is.EqualTo(PassiveSignature(expectedPassive)));
        expectedActions.Add(SealedLlmActionCodec.EncodePassive(expectedPassive));

        var expectedRefit = referenceForExpected.DecideRefit(rosterObservation);
        var actualRefit = driver.DecideRefit(rosterObservation);
        Assert.That(RefitSignature(actualRefit), Is.EqualTo(RefitSignature(expectedRefit)));
        expectedActions.Add(SealedLlmActionCodec.EncodeRefit(expectedRefit));

        var trace = harness.Builder.Build();
        Assert.That(trace.Entries.Count, Is.EqualTo(5));
        Assert.That(
            trace.Entries.Select(entry => entry.SeamKey.SeamType),
            Is.EqualTo(new[]
            {
                SealedLlmSeamTypes.Deployment,
                SealedLlmSeamTypes.Reward,
                SealedLlmSeamTypes.Recruit,
                SealedLlmSeamTypes.Passive,
                SealedLlmSeamTypes.Refit,
            }));
        Assert.That(trace.Entries.Select(entry => entry.SelectedAction), Is.EqualTo(expectedActions));
        for (var index = 0; index < trace.Entries.Count; index++)
        {
            var entry = trace.Entries[index];
            Assert.That(entry.SeamKey.DecisionIndex, Is.EqualTo(index));
            Assert.That(entry.SeamKey.Ordinal, Is.Zero);
            Assert.That(entry.ObservationCanonicalBytes, Is.Not.Empty);
            Assert.That(entry.RequestCanonicalBytes, Is.Not.Empty);
            Assert.That(entry.ResponseCanonicalBytes, Is.Not.Empty);
            Assert.That(entry.ObservationHash, Is.EqualTo(
                SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.ObservationCanonicalBytes)));
            Assert.That(entry.RequestHash, Is.EqualTo(
                SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.RequestCanonicalBytes)));
            Assert.That(entry.ResponseHash, Is.EqualTo(
                SealedDecisionTraceHash.ComputeCanonicalPayloadHash(entry.ResponseCanonicalBytes)));
            Assert.That(entry.HistoryPrefixHash, Is.EqualTo(entry.PreviousEntryHash));
            Assert.That(entry.TerminalFailure, Is.False);
        }

        Assert.That(
            SealedDecisionTraceReplayVerifier.Verify(trace, harness.Builder.Build()).VerificationPassed,
            Is.True);
    }

    [Test]
    public void MultiEntrySequence_PreviousEntryHashUsesHeaderThenPriorEntry()
    {
        var trace = BuildScriptedTrace();

        Assert.That(
            trace.Entries[0].PreviousEntryHash,
            Is.EqualTo(SealedDecisionTraceHash.ComputeHeaderHash(trace.Header)));
        for (var index = 1; index < trace.Entries.Count; index++)
        {
            Assert.That(
                trace.Entries[index].PreviousEntryHash,
                Is.EqualTo(SealedDecisionTraceHash.ComputeEntryHash(trace.Entries[index - 1])));
        }
    }

    [Test]
    public void OffMenuSelectedAction_SealsTerminalFailureWithoutFallbackAndOnlyAllowsRunReport()
    {
        var harness = CreateHarness(new FixedDecisionSource("99"));
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();

        var failure = Assert.Throws<SealedLlmTerminalFailureException>(
            () => harness.Bridge.DecideReward(observation));
        var decodeFailure = failure.InnerException as SealedLlmActionDecodeException;
        Assert.That(decodeFailure, Is.Not.Null);
        Assert.That(decodeFailure.Reason, Is.EqualTo(SealedLlmActionDecodeReason.OffMenu));
        Assert.That(harness.Builder.PendingTerminalFailure, Is.True);
        var failedKey = harness.Builder.PendingSeamKey;
        Assert.That(failedKey, Is.Not.Null);

        var terminal = harness.Builder.CompleteTerminalFailure(failedKey, "pre-terminal-state");
        Assert.That(terminal.TerminalFailure, Is.True);
        Assert.That(terminal.SelectedAction, Is.EqualTo("99"));
        Assert.That(terminal.AppliedActionHash, Is.EqualTo(
            SealedDecisionTraceBuilder.TerminalAppliedActionHashSentinel));
        Assert.That(terminal.ResultEventHash, Is.EqualTo(
            SealedDecisionTraceBuilder.TerminalResultEventHashSentinel));
        Assert.That(terminal.PostStateHash, Is.EqualTo(terminal.PreStateHash));
        Assert.Throws<InvalidOperationException>(() => harness.Bridge.DecideReward(observation));

        Assert.DoesNotThrow(() => harness.Bridge.SealRunReport(
            Encoding.UTF8.GetBytes("fixed-run-report-request"),
            "pre-terminal-state"));
        Assert.Throws<InvalidOperationException>(() => harness.Bridge.DecideReward(observation));

        var trace = harness.Builder.Build();
        Assert.That(trace.Entries.Count, Is.EqualTo(2));
        Assert.That(trace.Entries[1].SeamKey, Is.EqualTo(
            new SealedDecisionSeamKey(1, SealedLlmSeamTypes.RunReport, 0)));
        Assert.That(trace.Entries[1].PreviousEntryHash, Is.EqualTo(
            SealedDecisionTraceHash.ComputeEntryHash(trace.Entries[0])));
        Assert.That(SealedDecisionTraceReplayVerifier.Verify(trace, trace).VerificationPassed, Is.True);
    }

    [Test]
    public void Complete_WithDifferentSeamKey_ThrowsAndPreservesPendingExchange()
    {
        var reference = new ConceptCommitPolicy(RosterIntent());
        var harness = CreateHarness(new SyntheticStandInSource(reference, reference));
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        harness.Bridge.DecideReward(observation);
        var pending = harness.Builder.PendingSeamKey;
        var wrong = pending with { Ordinal = pending.Ordinal + 1 };

        Assert.Throws<InvalidOperationException>(() => harness.Builder.Complete(
            wrong,
            "pre-state",
            "applied-action",
            "result-event",
            "post-state"));
        Assert.That(harness.Builder.PendingSeamKey, Is.EqualTo(pending));
        Assert.DoesNotThrow(() => harness.Builder.Complete(
            pending,
            "pre-state",
            "applied-action",
            "result-event",
            "post-state"));
    }

    [Test]
    public void SyntheticStandInTrace_IsCaptureLockedAndNotCertificationEligible()
    {
        var trace = BuildScriptedTrace();

        Assert.That(trace.Header.CaptureSource, Is.EqualTo(
            SealedDecisionTraceCaptureSource.SyntheticStandIn));
        Assert.That(trace.Header.CertificationEligible, Is.False);
        Assert.That(trace.CertificationEligible, Is.False);
    }

    [Test]
    public void SameScriptedSequence_TwoBuildsHaveByteIdenticalManifestInputsAndHash()
    {
        var first = BuildScriptedTrace();
        var second = BuildScriptedTrace();
        var firstManifest = SealedDecisionTraceHash.ComputeManifest(first);
        var secondManifest = SealedDecisionTraceHash.ComputeManifest(second);

        Assert.That(Encoding.UTF8.GetBytes(secondManifest), Is.EqualTo(Encoding.UTF8.GetBytes(firstManifest)));
        Assert.That(
            SealedDecisionTraceHash.GetHeaderCanonicalBytes(second.Header),
            Is.EqualTo(SealedDecisionTraceHash.GetHeaderCanonicalBytes(first.Header)));
        Assert.That(second.Entries.Count, Is.EqualTo(first.Entries.Count));
        for (var index = 0; index < first.Entries.Count; index++)
        {
            Assert.That(
                SealedDecisionTraceHash.GetEntryCanonicalBytes(second.Entries[index]),
                Is.EqualTo(SealedDecisionTraceHash.GetEntryCanonicalBytes(first.Entries[index])));
        }
    }

    private static SealedDecisionTraceV1 BuildScriptedTrace()
    {
        var reference = new ConceptCommitPolicy(RosterIntent());
        var harness = CreateHarness(new SyntheticStandInSource(reference, reference));
        var driver = new InMemoryDecisionDriver(harness.Bridge, harness.Builder);
        var observation = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var rosterObservation = CreateRosterObservation(observation.Roster);

        driver.DecideDeployment(observation);
        driver.DecideReward(observation);
        driver.DecideRecruit(rosterObservation);
        driver.DecidePassive(rosterObservation);
        driver.DecideRefit(rosterObservation);
        return harness.Builder.Build();
    }

    private static CaptureHarness CreateHarness(ISealedDecisionSource source)
    {
        var manifest = new LlmPromptManifestV1(
            "capture-core-template-v1",
            "fixed prompt template",
            "fixed cold-start briefing",
            "model-snapshot-fixed",
            "temperature=0;top_p=1");
        var builder = new SealedDecisionTraceBuilder(
            "capture-core-run",
            "capture-core-scenario",
            1701,
            "build-manifest-fixed",
            "content-manifest-fixed",
            "visible-surface-fixed",
            manifest.PromptSchemaHash,
            "scorer-config-fixed",
            SealedDecisionTraceCaptureSource.SyntheticStandIn);
        return new CaptureHarness(builder, new SealedLlmBridgePolicy(source, builder, manifest));
    }

    private static HeadlessConceptIntent RosterIntent()
        => new(
            "coverage-roster-window",
            "coverage",
            new[]
            {
                "build.count_tag(human)>=4",
                "owned:passive:node-target",
                "owned:affix:affix-target",
            },
            new[]
            {
                "build.count_tag(human)=4/4",
                "acquire:passive:node-prereq",
                "acquire:passive:node-target",
            },
            "beat.roster_payoff",
            Array.Empty<string>(),
            new[] { "formation:any_legal" },
            Array.Empty<string>(),
            "aspirational",
            new[] { "visible_roster_track_unavailable" });

    private static HeadlessRosterPolicyObservation CreateRosterObservation(
        IReadOnlyList<HeadlessHeroObservation> roster)
    {
        var board = new HeadlessPassiveBoardObservation(
            "board-alpha",
            new[]
            {
                Node("node-prereq", 0),
                Node("node-target", 1, new[] { "node-prereq" }),
            });
        var passiveHeroes = roster.Select(hero => new HeadlessPassiveHeroObservation(
            hero.HeroId,
            hero.Level,
            "board-alpha",
            Array.Empty<string>(),
            5,
            1,
            new[] { board })).ToArray();
        var observation = new HeadlessRosterPolicyObservation(
            1701,
            "chapter-1",
            "site-1",
            12,
            roster,
            new HeadlessWalletObservation(10, 30),
            new[]
            {
                new HeadlessRecruitOfferObservation(
                    0, "hunter", "human", "ranger", "carry", "skill-shot", "passive-hunt", 4,
                    "Common", "OnPlan", false),
                new HeadlessRecruitOfferObservation(
                    1, "hexer", "undead", "mystic", "controller", "skill-hex", "passive-hex", 4,
                    "Common", "OffPlan", false),
            },
            passiveHeroes,
            new[]
            {
                new HeadlessRefitItemObservation(
                    "item-blade",
                    "item-instance",
                    "hero-1",
                    new[] { "weapon" },
                    "weapon-sword",
                    15,
                    new[]
                    {
                        new HeadlessRefitSlotObservation(
                            0,
                            new HeadlessAffixMechanicsObservation(
                                "affix-old",
                                Array.Empty<string>(),
                                Array.Empty<string>(),
                                Array.Empty<string>(),
                                Array.Empty<HeadlessStatModifierObservation>(),
                                Array.Empty<HeadlessRuleModifierObservation>()),
                            true),
                    }),
            },
            RosterEvidence(roster));
        HeadlessRosterPolicyGuard.ValidateObservation(observation);
        return observation;
    }

    private static HeadlessPassiveNodeObservation Node(
        string nodeId,
        int depth,
        IReadOnlyList<string> prerequisites = null)
        => new(
            nodeId,
            depth,
            "Small",
            prerequisites ?? Array.Empty<string>(),
            Array.Empty<string>(),
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<HeadlessStatModifierObservation>(),
            Array.Empty<HeadlessRuleModifierObservation>());

    private static IReadOnlyDictionary<string, string> RosterEvidence(
        IReadOnlyList<HeadlessHeroObservation> roster)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeadlessRosterPolicyEvidence.CampaignContextSignal] = "fact-context",
            [HeadlessRosterPolicyEvidence.WalletSignal] = "fact-wallet",
            [HeadlessRosterPolicyEvidence.RecruitSurfaceSignal] = "fact-recruit",
            [HeadlessRosterPolicyEvidence.PassiveSurfaceSignal] = "fact-passive",
            [HeadlessRosterPolicyEvidence.RefitSurfaceSignal] = "fact-refit",
            [HeadlessRosterPolicyEvidence.RecruitOfferSignal(0)] = "fact-offer-0",
            [HeadlessRosterPolicyEvidence.RecruitOfferSignal(1)] = "fact-offer-1",
            [HeadlessRosterPolicyEvidence.RefitSlotSignal("item-instance", 0)] = "fact-refit-slot",
        };
        foreach (var hero in roster)
        {
            result[HeadlessRosterPolicyEvidence.PassiveHeroSignal(hero.HeroId)] = $"fact-passive-{hero.HeroId}";
            result[HeadlessRosterPolicyEvidence.PassiveNodeSignal(hero.HeroId, "node-prereq")] =
                $"fact-{hero.HeroId}-prereq";
            result[HeadlessRosterPolicyEvidence.PassiveNodeSignal(hero.HeroId, "node-target")] =
                $"fact-{hero.HeroId}-target";
        }

        return result;
    }

    private static string PlacementSignature(HeadlessDeploymentDecision decision)
        => string.Join(";", decision.Placements
            .OrderBy(placement => placement.Anchor.ToString(), StringComparer.Ordinal)
            .Select(placement => $"{placement.Anchor}={placement.HeroId}"));

    private static string PassiveSignature(HeadlessPassiveDecision decision)
        => $"{decision.HeroId}:{decision.BoardId}:{decision.NodeId}";

    private static string RefitSignature(HeadlessRefitDecision decision)
        => $"{decision.ItemInstanceId}:{decision.AffixSlotIndex}:{string.Join(",", decision.SealedAffixIds)}";

    private sealed record CaptureHarness(
        SealedDecisionTraceBuilder Builder,
        SealedLlmBridgePolicy Bridge);

    /// <summary>Pure in-memory stand-in for the later game hook: decide, then supply synthetic game hashes.</summary>
    private sealed class InMemoryDecisionDriver
    {
        private readonly SealedLlmBridgePolicy _bridge;
        private readonly SealedDecisionTraceBuilder _builder;
        private int _completedCount;

        public InMemoryDecisionDriver(
            SealedLlmBridgePolicy bridge,
            SealedDecisionTraceBuilder builder)
        {
            _bridge = bridge;
            _builder = builder;
        }

        public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
            => Decide(() => _bridge.DecideDeployment(observation));

        public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
            => Decide(() => _bridge.DecideReward(observation));

        public HeadlessRecruitDecision DecideRecruit(HeadlessRosterPolicyObservation observation)
            => Decide(() => _bridge.DecideRecruit(observation));

        public HeadlessPassiveDecision DecidePassive(HeadlessRosterPolicyObservation observation)
            => Decide(() => _bridge.DecidePassiveAllocation(observation));

        public HeadlessRefitDecision DecideRefit(HeadlessRosterPolicyObservation observation)
            => Decide(() => _bridge.DecideRefit(observation));

        private TDecision Decide<TDecision>(Func<TDecision> decide)
        {
            var decision = decide();
            var index = _completedCount++;
            _builder.Complete(
                _builder.PendingSeamKey,
                $"pre-state-{index}",
                $"applied-action-{index}",
                $"result-event-{index}",
                $"post-state-{index}");
            return decision;
        }
    }

    private sealed class FixedDecisionSource : ISealedDecisionSource
    {
        private readonly string _selectedAction;

        public FixedDecisionSource(string selectedAction)
        {
            _selectedAction = selectedAction;
        }

        public LlmDecisionResponseV1 RequestDecision(SealedLlmDecisionRequest request)
            => new(
                _selectedAction,
                new LlmDeclaredIntentV1(
                    "fixed-test-intent",
                    Array.Empty<string>(),
                    "exercise terminal failure",
                    Array.Empty<string>(),
                    "none",
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    1d),
                "fixed-test-intent-ref",
                Array.Empty<LlmBuildHypothesisV1>());

        public LlmRunReportResponseV1 RequestRunReport(SealedLlmRunReportRequest request)
            => new(
                "fixed retrospective",
                "fixed near miss",
                "fixed next concept",
                Array.Empty<string>(),
                Array.Empty<LlmEvaluationSentenceV1>(),
                "none");
    }
}
