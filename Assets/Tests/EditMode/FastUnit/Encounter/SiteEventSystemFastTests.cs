using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class SiteEventSystemFastTests
{
    private static readonly WarWoundSpec WoundSpec = new(0.25f, 0.9f, 1, 3, 1, true);

    [Test]
    public void OutcomeApplier_AppliesAllKindsWithoutRuntimeEntropy()
    {
        var original = CreateResolutionState(activeWounds: new[] { "hero-front-top" });
        var choice = new SiteEventChoiceTemplate(
            "all",
            "event.choice.all",
            new[]
            {
                Outcome(OutcomeKind.GrantItem, "item_iron_sword", "affix_guarded"),
                Outcome(OutcomeKind.GrantEcho, amount: 7),
                Outcome(OutcomeKind.GrantExp, amount: 25, targetRule: OutcomeTargetRule.LowestDeployIndex),
                Outcome(OutcomeKind.CureWound),
                Outcome(OutcomeKind.InflictWound, targetRule: OutcomeTargetRule.LowestDeployIndexFrontline),
                Outcome(OutcomeKind.RouteToNode, "risk"),
                Outcome(OutcomeKind.GrantRecruitOffer, "priest", "trait_frail"),
                Outcome(OutcomeKind.GrantConsumable, "consumable_field_dressing", amount: 2),
                Outcome(OutcomeKind.ExtractBonus, amount: 10),
            });

        var first = SiteEventOutcomeApplier.Apply(choice, original, WoundSpec);
        var second = SiteEventOutcomeApplier.Apply(choice, original, WoundSpec);

        Assert.That(first.IsSuccess, Is.True, first.Error);
        Assert.That(second.IsSuccess, Is.True, second.Error);
        Assert.That(first.State.Echo, Is.EqualTo(17));
        Assert.That(first.State.HeroExperienceById["hero-front-top"], Is.EqualTo(25));
        Assert.That(first.State.Run.ActiveWoundHeroIds, Is.EqualTo(new[] { "hero-front-top" }));
        Assert.That(first.State.SelectedRouteNodeId, Is.EqualTo("risk"));
        Assert.That(first.State.GrantedItems.Single(), Is.EqualTo(new SiteEventItemGrant("item_iron_sword", "affix_guarded")));
        Assert.That(first.State.GrantedConsumableIds, Is.EqualTo(new[] { "consumable_field_dressing", "consumable_field_dressing" }));
        Assert.That(first.State.GrantedRecruitOffers.Single(), Is.EqualTo(new SiteEventRecruitOffer("priest", "trait_frail")));
        Assert.That(first.State.RecruitOffersGrantedAtSite, Is.EqualTo(1));
        Assert.That(first.State.ExtractBonusEcho, Is.EqualTo(10));

        Assert.That(second.State.Echo, Is.EqualTo(first.State.Echo));
        Assert.That(second.State.HeroExperienceById, Is.EquivalentTo(first.State.HeroExperienceById));
        Assert.That(second.State.Run.ActiveWoundHeroIds, Is.EqualTo(first.State.Run.ActiveWoundHeroIds));
        Assert.That(second.State.SelectedRouteNodeId, Is.EqualTo(first.State.SelectedRouteNodeId));
        Assert.That(second.State.GrantedItems, Is.EqualTo(first.State.GrantedItems));
        Assert.That(second.State.GrantedConsumableIds, Is.EqualTo(first.State.GrantedConsumableIds));
        Assert.That(second.State.GrantedRecruitOffers, Is.EqualTo(first.State.GrantedRecruitOffers));
    }

    [Test]
    public void InflictWound_TargetsLowestDeployIndexFrontliner()
    {
        var choice = new SiteEventChoiceTemplate(
            "wound",
            "event.choice.wound",
            new[] { Outcome(OutcomeKind.InflictWound, targetRule: OutcomeTargetRule.LowestDeployIndexFrontline) });

        var result = SiteEventOutcomeApplier.Apply(choice, CreateResolutionState(), WoundSpec);

        Assert.That(result.IsSuccess, Is.True, result.Error);
        Assert.That(result.State.Run.ActiveWoundHeroIds, Is.EqualTo(new[] { "hero-front-top" }));
        Assert.That(result.AffectedHeroIds, Is.EqualTo(new[] { "hero-front-top" }));
    }

    [Test]
    public void RecruitOffer_IsCappedAtOnePerSite()
    {
        var choice = new SiteEventChoiceTemplate(
            "recruit",
            "event.choice.recruit",
            new[] { Outcome(OutcomeKind.GrantRecruitOffer, "priest") });
        var first = SiteEventOutcomeApplier.Apply(choice, CreateResolutionState(), WoundSpec);

        var second = SiteEventOutcomeApplier.Apply(choice, first.State, WoundSpec);

        Assert.That(first.IsSuccess, Is.True);
        Assert.That(second.IsSuccess, Is.False);
        Assert.That(second.State.GrantedRecruitOffers, Has.Count.EqualTo(1));
    }

    [TestCase("safe", "safe")]
    [TestCase("risk", "risk")]
    public void SessionEvent_PresentsMultipleChoices_AndHeadlessActionRoutesChosenEdge(
        string choiceId,
        string expectedNodeId)
    {
        var eventTemplate = CreateFixtureEvent();
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup(
            CreateEventGraph(),
            new Dictionary<string, SiteEventTemplate>(StringComparer.Ordinal)
            {
                [eventTemplate.Id] = eventTemplate,
            },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["site_event_fixture/safe"] = "site_event_choice_safe",
                ["site_event_fixture/risk"] = "site_event_choice_risk",
            });
        var session = CreateSession(lookup);

        Assert.That(session.GetCurrentExpeditionNode()?.GraphNodeId, Is.EqualTo("event"));
        Assert.That(session.GetCurrentExpeditionNode()?.RewardSourceId, Is.EqualTo("reward_source_shrine_event"));
        Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.True);
        Assert.That(session.PendingSiteEvent, Is.Not.Null);
        Assert.That(session.PendingSiteEvent!.Choices.Count, Is.EqualTo(2));
        Assert.That(
            session.PendingSiteEvent.Choices.Select(choice => choice.IconId),
            Is.EqualTo(new[] { "site_event_choice_safe", "site_event_choice_risk" }));
        Assert.That(
            session.PendingSiteEvent.Choices.All(choice => choice.OutcomePreviews.Count > 0),
            Is.True);
        Assert.That(session.SiteEvents.GetLegalActions().Select(action => action.ChoiceId), Is.EquivalentTo(new[] { "safe", "risk" }));

        Assert.That(session.SiteEvents.ApplyChoice(choiceId), Is.True);
        Assert.That(session.PendingSiteEvent, Is.Null);
        Assert.That(session.HasPendingRewardSettlement, Is.True);
        session.ReturnToTownAfterReward();

        Assert.That(session.GetCurrentExpeditionNode()?.GraphNodeId, Is.EqualTo(expectedNodeId));
        Assert.That(session.CurrentExpeditionNodeIndex, Is.EqualTo(choiceId == "safe" ? 1 : 2));
        if (choiceId == "risk")
        {
            Assert.That(session.ActiveRun!.ActiveWoundHeroIds!.Count, Is.EqualTo(1));
            Assert.That(session.PendingSiteRecruitOffers.Count, Is.EqualTo(1));
            Assert.That(session.SiteConsumableIds, Is.EqualTo(new[] { "consumable_field_dressing" }));
        }
    }

    [Test]
    public void OutcomePreview_PreservesEveryAuthoredConsequence_WithoutExactAmountOrBestCaseCollapse()
    {
        var outcomes = new[]
        {
            Outcome(OutcomeKind.GrantEcho, amount: -10),
            Outcome(OutcomeKind.RouteToNode, "risk"),
            Outcome(OutcomeKind.GrantExp, amount: 25, targetRule: OutcomeTargetRule.LowestDeployIndex),
        };

        var previews = SiteEventOutcomePreviewBuilder.Build(outcomes);

        Assert.That(previews.Count, Is.EqualTo(outcomes.Length));
        Assert.That(
            previews.Select(preview => preview.Category),
            Is.EqualTo(new[]
            {
                SiteEventOutcomePreviewCategory.Echo,
                SiteEventOutcomePreviewCategory.Route,
                SiteEventOutcomePreviewCategory.Experience,
            }));
        Assert.That(previews[0].IsCost, Is.True);
        Assert.That(previews[0].IntensityPips, Is.InRange(1, 5));
        Assert.That(previews[2].Certainty, Is.EqualTo(SiteEventOutcomePreviewCertainty.TargetVaries));
        Assert.That(
            typeof(SiteEventOutcomePreviewViewModel).GetProperty("Amount"),
            Is.Null,
            "The qualitative preview must not expose the exact authored amount.");

        var noChange = SiteEventOutcomePreviewBuilder.Build(Array.Empty<SiteEventOutcomeTemplate>());
        Assert.That(noChange.Count, Is.EqualTo(1));
        Assert.That(noChange[0].Category, Is.EqualTo(SiteEventOutcomePreviewCategory.NoChange));
        Assert.That(noChange[0].IntensityPips, Is.Zero);
    }

    private static SiteEventResolutionState CreateResolutionState(IReadOnlyList<string>? activeWounds = null)
    {
        var assignments = new Dictionary<DeploymentAnchorId, string>
        {
            [DeploymentAnchorId.BackTop] = "hero-back-top",
            [DeploymentAnchorId.FrontBottom] = "hero-front-bottom",
            [DeploymentAnchorId.FrontTop] = "hero-front-top",
        };
        var blueprint = new SquadBlueprintState(
            "bp-event",
            "Event",
            TeamPostureType.StandardAdvance,
            "tactic-standard",
            assignments,
            assignments.Values.ToArray(),
            new Dictionary<string, string>(StringComparer.Ordinal));
        var run = RunStateService.StartRun("run-event", blueprint, false) with
        {
            BattleDeployHeroIds = assignments.OrderBy(pair => (int)pair.Key).Select(pair => pair.Value).ToArray(),
            ActiveWoundHeroIds = activeWounds ?? Array.Empty<string>(),
        };
        return new SiteEventResolutionState(
            run,
            10,
            assignments.Values.ToDictionary(id => id, _ => 0, StringComparer.Ordinal),
            0,
            1,
            0,
            Array.Empty<SiteEventItemGrant>(),
            Array.Empty<string>(),
            Array.Empty<SiteEventRecruitOffer>(),
            string.Empty,
            new[] { "safe", "risk" });
    }

    private static SiteEventTemplate CreateFixtureEvent() =>
        new(
            "site_event_fixture",
            "site_alpha_gate",
            "content.site_event.fixture.setup",
            new[]
            {
                new SiteEventChoiceTemplate(
                    "safe",
                    "content.site_event.fixture.choice.safe",
                    new[]
                    {
                        Outcome(OutcomeKind.GrantEcho, amount: 3),
                        Outcome(OutcomeKind.RouteToNode, "safe"),
                    }),
                new SiteEventChoiceTemplate(
                    "risk",
                    "content.site_event.fixture.choice.risk",
                    new[]
                    {
                        Outcome(OutcomeKind.InflictWound, targetRule: OutcomeTargetRule.LowestDeployIndexFrontline),
                        Outcome(OutcomeKind.GrantRecruitOffer, "priest"),
                        Outcome(OutcomeKind.GrantConsumable, "consumable_field_dressing"),
                        Outcome(OutcomeKind.RouteToNode, "risk"),
                    }),
            });

    private static SiteGraphTemplate CreateEventGraph() =>
        new(
            "site_graph_event_fixture",
            "site_alpha_gate",
            new[]
            {
                Node("event", 0, SiteNodeKindValue.Event, new[] { "safe", "risk" }, "site_event_fixture", "reward_source_shrine_event"),
                Node("safe", 1, SiteNodeKindValue.Cache, new[] { "extract" }),
                Node("risk", 1, SiteNodeKindValue.Cache, new[] { "extract" }),
                Node("extract", 2, SiteNodeKindValue.Extract, Array.Empty<string>(), rewardSourceId: "reward_source_extract"),
            });

    private static SiteGraphNodeTemplate Node(
        string id,
        int rank,
        SiteNodeKindValue kind,
        IReadOnlyList<string> next,
        string eventId = "",
        string rewardSourceId = "") =>
        new(id, rank, kind, "fixture", string.Empty, eventId, next, 0, rewardSourceId);

    private static GameSessionState CreateSession(FakeCombatContentLookup lookup)
    {
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                Hero("hero-1", "vanguard"),
                Hero("hero-2", "ranger"),
                Hero("hero-3", "duelist"),
                Hero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        session.BeginNewExpedition();
        return session;
    }

    private static HeroInstanceRecord Hero(string id, string classId) =>
        new()
        {
            HeroId = id,
            Name = id,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };

    private static SiteEventOutcomeTemplate Outcome(
        OutcomeKind kind,
        string payloadId = "",
        string auxiliaryId = "",
        int amount = 0,
        OutcomeTargetRule targetRule = OutcomeTargetRule.None) =>
        new(kind, payloadId, auxiliaryId, amount, targetRule);
}
