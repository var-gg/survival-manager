using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Editor.SeedData;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Persistence.Abstractions.Models;
using SM.Tests.EditMode.Fakes;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class AshenGateSiteGraphTests
{
    private const string ChapterId = "chapter_ashen_gate";
    private const string SiteId = "site_ashen_gate";
    private const string RiskEncounterId = "site_ashen_gate_risk_skirmish_1";
    private const string AidStationEventId = "site_event_collapsed_aid_station";

    [SetUp]
    public void SetUp()
    {
        SampleSeedGenerator.RequireCanonicalSampleContentReady(nameof(AshenGateSiteGraphTests));
    }

    [Test]
    public void AuthoredGraph_PreservesCanonicalSafeBattleCoordinatesAndHashes()
    {
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        var snapshot = lookup.Snapshot;
        var site = snapshot.ExpeditionSites![SiteId];
        Assert.That(site.Graph, Is.Not.Null);

        var authoredResolver = new EncounterResolutionService(snapshot);
        var authoredTrack = authoredResolver.BuildSiteTrack(ChapterId, SiteId);
        Assert.That(authoredTrack, Has.Count.EqualTo(8));
        Assert.That(authoredTrack[5].NodeId, Is.EqualTo("site_ashen_gate:entry"));
        Assert.That(authoredTrack[5].NextNodeIndices, Is.EqualTo(new[] { 0, 6 }),
            "The first/default edge stays canonical-safe; risk requires an explicit second-edge choice.");
        Assert.That(authoredTrack[6].EncounterId, Is.EqualTo(RiskEncounterId));
        Assert.That(authoredTrack[6].RewardSourceId, Is.EqualTo("reward_source_elite"));
        Assert.That(authoredTrack[7].EventId, Is.EqualTo(AidStationEventId));
        Assert.That(authoredTrack[7].NextNodeIndices, Is.EqualTo(new[] { 2 }));

        var legacySites = snapshot.ExpeditionSites.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        legacySites[SiteId] = site with { Graph = null };
        var legacyResolver = new EncounterResolutionService(snapshot with { ExpeditionSites = legacySites });
        var legacyTrack = legacyResolver.BuildSiteTrack(ChapterId, SiteId);

        Assert.That(legacyTrack, Has.Count.EqualTo(5));
        for (var index = 0; index < legacyTrack.Count; index++)
        {
            Assert.That(authoredTrack[index].Index, Is.EqualTo(legacyTrack[index].Index), $"node[{index}].Index");
            Assert.That(authoredTrack[index].NodeId, Is.EqualTo(legacyTrack[index].NodeId), $"node[{index}].NodeId");
            Assert.That(authoredTrack[index].NodeKind, Is.EqualTo(legacyTrack[index].NodeKind), $"node[{index}].NodeKind");
            Assert.That(authoredTrack[index].EncounterId, Is.EqualTo(legacyTrack[index].EncounterId), $"node[{index}].EncounterId");
            Assert.That(authoredTrack[index].RewardSourceId, Is.EqualTo(legacyTrack[index].RewardSourceId), $"node[{index}].RewardSourceId");
            Assert.That(authoredTrack[index].RequiresBattle, Is.EqualTo(legacyTrack[index].RequiresBattle), $"node[{index}].RequiresBattle");
        }

        var run = RunStateService.StartRun(SiteId, CreateBlueprint(), isQuickBattle: false);
        for (var index = 0; index < 4; index++)
        {
            var authored = authoredResolver.BuildBattleContext(run, ChapterId, SiteId, index);
            var legacy = legacyResolver.BuildBattleContext(run, ChapterId, SiteId, index);
            Assert.That(authored.BattleContextHash, Is.EqualTo(legacy.BattleContextHash), $"battle[{index}].hash");
            Assert.That(authored.BattleSeed, Is.EqualTo(legacy.BattleSeed), $"battle[{index}].seed");
        }
    }

    [Test]
    public void ExplicitRiskRoute_ThenAidStationChoice_ReplaysDeterministically()
    {
        var first = TraverseRiskAndAidStation();
        var second = TraverseRiskAndAidStation();

        Assert.That(second, Is.EqualTo(first));
        Assert.That(first.CurrentNodeId, Is.EqualTo("site_ashen_gate_elite_1"));
        Assert.That(first.ResolvedNodeSignature, Does.Contain(RiskEncounterId));
        Assert.That(first.ResolvedNodeSignature, Does.Contain("site_ashen_gate:event:collapsed_aid_station"));
        Assert.That(first.ResolvedNodeSignature, Does.Not.Contain("site_ashen_gate_skirmish_1"),
            "The explicit risk branch must not silently resolve its canonical-safe sibling.");
    }

    private static TraversalWitness TraverseRiskAndAidStation()
    {
        var session = GameSessionTestFactory.Create(new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true));
        session.BindProfile(new SaveProfile { ProfileId = "ashen_gate_branch_witness" });
        session.SetCurrentScene(SceneNames.Town);
        session.BeginNewExpedition();

        Assert.That(session.SelectedCampaignChapterId, Is.EqualTo(ChapterId));
        Assert.That(session.SelectedCampaignSiteId, Is.EqualTo(SiteId));
        Assert.That(session.GetCurrentExpeditionNode()?.GraphNodeId, Is.EqualTo("site_ashen_gate:entry"));
        Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.True);
        Assert.That(session.SelectNextExpeditionNode(6), Is.True);
        session.ReturnToTownAfterReward();

        Assert.That(session.GetCurrentExpeditionNode()?.Id, Is.EqualTo(RiskEncounterId));
        ResolveBattleAndReturn(session);
        Assert.That(session.GetCurrentExpeditionNode()?.Id, Is.EqualTo("site_ashen_gate_skirmish_2"));
        ResolveBattleAndReturn(session);

        Assert.That(session.GetCurrentExpeditionNode()?.EventId, Is.EqualTo(AidStationEventId));
        Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.True);
        Assert.That(session.SiteEvents.GetLegalActions().Select(action => action.ChoiceId), Does.Contain("burn_it"));
        Assert.That(session.SiteEvents.ApplyChoice("burn_it"), Is.True);
        session.ReturnToTownAfterReward();

        return new TraversalWitness(
            session.GetCurrentExpeditionNode()?.GraphNodeId ?? string.Empty,
            session.Profile.Currencies.Echo,
            string.Join("|", session.Profile.ActiveRun.ResolvedExpeditionNodeIds.OrderBy(id => id, StringComparer.Ordinal)));
    }

    private static void ResolveBattleAndReturn(GameSessionState session)
    {
        Assert.That(session.PrepareSelectedBattleNodeHandoff(), Is.True);
        session.MarkBattleResolved(victory: true, stepCount: 8, eventCount: 4);
        if (session.PendingRewardChoices.Count > 0)
        {
            Assert.That(session.ApplyRewardChoice(0), Is.True);
        }

        session.ReturnToTownAfterReward();
    }

    private static SquadBlueprintState CreateBlueprint()
    {
        return new SquadBlueprintState(
            "ashen-safe-identity",
            "Ashen safe identity",
            TeamPostureType.StandardAdvance,
            string.Empty,
            new Dictionary<DeploymentAnchorId, string>(),
            Array.Empty<string>(),
            new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed record TraversalWitness(
        string CurrentNodeId,
        int Echo,
        string ResolvedNodeSignature);
}
