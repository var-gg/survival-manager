using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SM.HeadlessPolicies;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class HeadlessRosterPolicyFastTests
{
    [Test]
    public void RecruitObservationBuilder_MapsCurrentSessionOfferFieldsWithoutFutureStream()
    {
        var source = File.ReadAllText(Path.Combine(
            "Assets", "_Game", "Scripts", "Editor", "Validation", "H100RosterPolicyObservationBuilder.cs"));

        Assert.That(source, Does.Contain("session.RecruitOffers.Select((offer, index)"));
        Assert.That(source, Does.Contain("offer.UnitBlueprintId"));
        Assert.That(source, Does.Contain("archetype?.RaceId"));
        Assert.That(source, Does.Contain("archetype?.ClassId"));
        Assert.That(source, Does.Contain("offer.FlexActiveId"));
        Assert.That(source, Does.Contain("offer.FlexPassiveId"));
        Assert.That(source, Does.Contain("offer.Metadata?.GoldCost"));
        Assert.That(source, Does.Contain("offer.Metadata?.Tier"));
        Assert.That(source, Does.Contain("offer.Metadata?.PlanFit"));
        Assert.That(source, Does.Not.Contain("RerollRecruitOffers"));
        Assert.That(source, Does.Not.Contain("PendingScoutDirective"));
    }

    [Test]
    public void Guard_RejectsUnaffordableCapAndMissingPrerequisiteDecisions()
    {
        var baseline = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var observation = CreateRosterObservation(baseline.Roster);
        var recruit = new HeadlessRecruitDecision(0, "test", 0d, new[] { "fact" });
        var unaffordable = Copy(observation, wallet: new HeadlessWalletObservation(0, observation.Wallet.Echo));
        var capped = Copy(observation, rosterCapacity: observation.Roster.Count);
        var passive = new HeadlessPassiveDecision("hero-1", "board-alpha", "node-target", "test", 0d, new[] { "fact" });

        Assert.Throws<InvalidOperationException>(() => HeadlessRosterPolicyGuard.ValidateRecruitDecision(unaffordable, recruit));
        Assert.Throws<InvalidOperationException>(() => HeadlessRosterPolicyGuard.ValidateRecruitDecision(capped, recruit));
        Assert.Throws<InvalidOperationException>(() => HeadlessRosterPolicyGuard.ValidatePassiveDecision(observation, passive));
    }

    [Test]
    public void SameSeed_ProducesSameRecruitAndPrerequisiteNodeDecision()
    {
        var baseline = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var observation = CreateRosterObservation(baseline.Roster);
        var first = new ConceptCommitPolicy(RosterIntent());
        var repeated = new ConceptCommitPolicy(RosterIntent());
        first.DecideDeployment(baseline);
        repeated.DecideDeployment(baseline);

        var firstRecruit = first.DecideRecruit(observation);
        var repeatedRecruit = repeated.DecideRecruit(observation);
        var firstNode = first.DecidePassiveAllocation(observation);
        var repeatedNode = repeated.DecidePassiveAllocation(observation);

        Assert.That(firstRecruit.OfferIndex, Is.EqualTo(repeatedRecruit.OfferIndex));
        Assert.That(firstRecruit.Rationale, Is.EqualTo(repeatedRecruit.Rationale));
        Assert.That(firstRecruit.EvidenceFactIds, Is.EqualTo(repeatedRecruit.EvidenceFactIds));
        Assert.That(firstRecruit.OfferIndex, Is.EqualTo(0));
        Assert.That(firstNode.HeroId, Is.EqualTo(repeatedNode.HeroId));
        Assert.That(firstNode.BoardId, Is.EqualTo(repeatedNode.BoardId));
        Assert.That(firstNode.NodeId, Is.EqualTo(repeatedNode.NodeId));
        Assert.That(firstNode.Rationale, Is.EqualTo(repeatedNode.Rationale));
        Assert.That(firstNode.EvidenceFactIds, Is.EqualTo(repeatedNode.EvidenceFactIds));
        Assert.That(firstNode.NodeId, Is.EqualTo("node-prereq"));
    }

    [Test]
    public void PassiveDecision_SelectsUnlistedPrerequisiteBeforeRelevantTarget()
    {
        var baseline = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var intent = new HeadlessConceptIntent(
            "coverage-prerequisite-chain",
            "coverage",
            new[] { "owned:passive:node-target" },
            new[] { "acquire:passive:node-target" },
            "beat.passive_target",
            Array.Empty<string>(),
            new[] { "formation:any_legal" },
            Array.Empty<string>(),
            "aspirational",
            new[] { "visible_node_track_unavailable" });
        var policy = new ConceptCommitPolicy(intent);
        policy.DecideDeployment(baseline);

        var decision = policy.DecidePassiveAllocation(CreateRosterObservation(baseline.Roster));

        Assert.That(decision.NodeId, Is.EqualTo("node-prereq"));
        Assert.That(decision.Rationale, Does.Contain("path=prerequisite"));
    }

    [Test]
    public void PassiveDecision_AfterPrerequisite_AdvancesTargetMilestoneInTrace()
    {
        var baseline = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var policy = new ConceptCommitPolicy(RosterIntent());
        policy.DecideDeployment(baseline);

        var prerequisite = policy.DecidePassiveAllocation(CreateRosterObservation(baseline.Roster));
        var target = policy.DecidePassiveAllocation(CreateRosterObservation(
            baseline.Roster,
            selectedNodeIds: new[] { prerequisite.NodeId }));
        var trace = policy.LastIntentDecision;

        Assert.That(prerequisite.NodeId, Is.EqualTo("node-prereq"));
        Assert.That(target.NodeId, Is.EqualTo("node-target"));
        Assert.That(trace.DecisionKind, Is.EqualTo("level_node"));
        Assert.That(trace.MilestoneAdvanced, Is.True);
        Assert.That(trace.ScarceResourceInvested, Is.True);
        Assert.That(trace.StateSnapshot.CompletedMilestones, Does.Contain("acquire:passive:node-target"));
    }

    [Test]
    public void RecruitDecision_AdvancesDeclaredMilestoneAndRecordsScarceInvestment()
    {
        var baseline = IntentPolicyObservationFixture.CreateRecruitBaseline();
        var policy = new ConceptCommitPolicy(RosterIntent());
        policy.DecideDeployment(baseline);

        var decision = policy.DecideRecruit(CreateRosterObservation(baseline.Roster));
        var trace = policy.LastIntentDecision;

        Assert.That(decision.IsNoOp, Is.False);
        Assert.That(trace.DecisionKind, Is.EqualTo("recruit"));
        Assert.That(trace.MilestoneAdvanced, Is.True);
        Assert.That(trace.ScarceResourceInvested, Is.True);
        Assert.That(trace.StateSnapshot.CompletedMilestones, Does.Contain("build.count_tag(human)=4/4"));
    }

    [Test]
    public void ExistingSixProductionPolicies_DoNotOptIntoRosterWindow()
    {
        foreach (var policyId in HeadlessPolicyFactory.ProductionPolicyIds)
        {
            Assert.That(HeadlessPolicyFactory.Create(policyId), Is.Not.InstanceOf<IHeadlessRosterPolicy>(), policyId);
        }
    }

    [Test]
    public void EightSeedTownTrace_ContainsRecruitNodeAndRefitRowsWithoutMissingKinds()
    {
        for (var seed = 1701; seed < 1709; seed++)
        {
            var baseline = IntentPolicyObservationFixture.CreateRecruitBaseline(seed);
            var observation = CreateRosterObservation(baseline.Roster, seed);
            var policy = new ConceptCommitPolicy(RosterIntent());
            policy.DecideDeployment(baseline);
            policy.DecideRecruit(observation);
            policy.DecidePassiveAllocation(observation);
            policy.DecideRefit(observation);

            Assert.That(
                policy.DecisionTrace.Select(value => value.DecisionKind),
                Is.EqualTo(new[] { "deployment", "recruit", "level_node", "refit" }),
                $"seed={seed}");
            Assert.That(policy.DecisionTrace.All(value => !string.IsNullOrWhiteSpace(value.Action)), Is.True, $"seed={seed}");
            Assert.That(policy.DecisionTrace.Count(value => value.ScarceResourceInvested), Is.GreaterThanOrEqualTo(3), $"seed={seed}");
        }
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
        IReadOnlyList<HeadlessHeroObservation> roster,
        int decisionSeed = 1701,
        IReadOnlyList<string> selectedNodeIds = null)
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
            selectedNodeIds ?? Array.Empty<string>(),
            5,
            1,
            new[] { board })).ToArray();
        var observation = new HeadlessRosterPolicyObservation(
            decisionSeed,
            "chapter-1",
            "site-1",
            12,
            roster,
            new HeadlessWalletObservation(10, 30),
            new[]
            {
                new HeadlessRecruitOfferObservation(0, "hunter", "human", "ranger", "carry", "skill-shot", "passive-hunt", 4, "Common", "OnPlan", false),
                new HeadlessRecruitOfferObservation(1, "hexer", "undead", "mystic", "controller", "skill-hex", "passive-hex", 4, "Common", "OffPlan", false),
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
            Evidence());
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

    private static HeadlessRosterPolicyObservation Copy(
        HeadlessRosterPolicyObservation source,
        HeadlessWalletObservation wallet = null,
        int? rosterCapacity = null)
        => new(
            source.DecisionSeed,
            source.ChapterId,
            source.SiteId,
            rosterCapacity ?? source.RosterCapacity,
            source.Roster,
            wallet ?? source.Wallet,
            source.RecruitOffers,
            source.PassiveHeroes,
            source.RefitItems,
            source.EvidenceFactIdsBySignal);

    private static IReadOnlyDictionary<string, string> Evidence()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HeadlessRosterPolicyEvidence.CampaignContextSignal] = "fact-context",
            [HeadlessRosterPolicyEvidence.WalletSignal] = "fact-wallet",
            [HeadlessRosterPolicyEvidence.RecruitSurfaceSignal] = "fact-recruit",
            [HeadlessRosterPolicyEvidence.PassiveSurfaceSignal] = "fact-passive",
            [HeadlessRosterPolicyEvidence.RefitSurfaceSignal] = "fact-refit",
        };
        for (var index = 0; index < 2; index++)
        {
            result[HeadlessRosterPolicyEvidence.RecruitOfferSignal(index)] = $"fact-offer-{index}";
        }

        for (var hero = 1; hero <= 5; hero++)
        {
            var heroId = $"hero-{hero}";
            result[HeadlessRosterPolicyEvidence.PassiveHeroSignal(heroId)] = $"fact-passive-{heroId}";
            result[HeadlessRosterPolicyEvidence.PassiveNodeSignal(heroId, "node-prereq")] = $"fact-{heroId}-prereq";
            result[HeadlessRosterPolicyEvidence.PassiveNodeSignal(heroId, "node-target")] = $"fact-{heroId}-target";
        }

        result[HeadlessRosterPolicyEvidence.RefitSlotSignal("item-instance", 0)] = "fact-refit-slot";
        return result;
    }
}
