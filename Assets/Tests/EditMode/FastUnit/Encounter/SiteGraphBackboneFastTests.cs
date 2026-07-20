using System;
using System.Collections.Generic;
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
public sealed class SiteGraphBackboneFastTests
{
    [Test]
    public void BuildSiteTrack_WithoutGraph_PreservesLegacyLinearProjection()
    {
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup();
        var resolver = new EncounterResolutionService(lookup.Snapshot);

        var track = resolver.BuildSiteTrack("chapter_alpha", "site_alpha_gate");

        Assert.That(track, Has.Count.EqualTo(5));
        Assert.That(track[0].NodeId, Is.EqualTo("site_alpha_gate:skirmish_a"));
        Assert.That(track[0].NodeKind, Is.EqualTo(SiteNodeKindValue.Skirmish));
        Assert.That(track[0].EncounterId, Is.EqualTo("site_alpha_gate:skirmish_a"));
        Assert.That(track[0].NextNodeIndices, Is.EqualTo(new[] { 1 }));
        Assert.That(track[1].NextNodeIndices, Is.EqualTo(new[] { 2 }));
        Assert.That(track[2].NodeKind, Is.EqualTo(SiteNodeKindValue.Elite));
        Assert.That(track[2].NextNodeIndices, Is.EqualTo(new[] { 3 }));
        Assert.That(track[3].NodeKind, Is.EqualTo(SiteNodeKindValue.Boss));
        Assert.That(track[3].NextNodeIndices, Is.EqualTo(new[] { 4 }));
        Assert.That(track[4].NodeId, Is.EqualTo("site_alpha_gate:extract"));
        Assert.That(track[4].NodeKind, Is.EqualTo(SiteNodeKindValue.Extract));
        Assert.That(track[4].RequiresBattle, Is.False);
        Assert.That(track[4].NextNodeIndices, Is.Empty);
    }

    [Test]
    public void BranchGraph_ProjectsEdges_AndChosenEdgeFlowIsDeterministic()
    {
        var graph = CreateBranchGraph();
        var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup(graph);
        var resolver = new EncounterResolutionService(lookup.Snapshot);

        var track = resolver.BuildSiteTrack("chapter_alpha", "site_alpha_gate");

        Assert.That(track, Has.Count.EqualTo(6));
        Assert.That(track[0].NodeId, Is.EqualTo("briefing"));
        Assert.That(track[0].NodeKind, Is.EqualTo(SiteNodeKindValue.Briefing));
        Assert.That(track[0].NextNodeIndices, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(track[1].NodeId, Is.EqualTo("safe"));
        Assert.That(track[1].NextNodeIndices, Is.EqualTo(new[] { 3 }));
        Assert.That(track[2].NodeId, Is.EqualTo("risk"));
        Assert.That(track[2].EncounterId, Is.EqualTo("site_alpha_gate:skirmish_b"));
        Assert.That(track[2].NextNodeIndices, Is.EqualTo(new[] { 3 }));
        Assert.That(track[5].NodeKind, Is.EqualTo(SiteNodeKindValue.Extract));
        Assert.That(track[5].NextNodeIndices, Is.Empty);

        Assert.That(ResolveBriefingThroughRiskEdge(lookup), Is.EqualTo(2));
        Assert.That(ResolveBriefingThroughRiskEdge(lookup), Is.EqualTo(2));
    }

    private static int ResolveBriefingThroughRiskEdge(FakeCombatContentLookup lookup)
    {
        var session = GameSessionTestFactory.Create(lookup);
        session.BindProfile(new SaveProfile
        {
            Heroes = new List<HeroInstanceRecord>
            {
                CreateHero("hero-1", "vanguard"),
                CreateHero("hero-2", "ranger"),
                CreateHero("hero-3", "duelist"),
                CreateHero("hero-4", "mystic"),
            },
        });
        session.SetCurrentScene(SceneNames.Town);
        session.BeginNewExpedition();

        Assert.That(session.GetCurrentExpeditionNode()?.NodeId, Is.EqualTo("briefing"));
        Assert.That(session.ResolveSelectedNodeToRewardSettlement(), Is.True);
        Assert.That(session.SelectNextExpeditionNode(2), Is.True);
        session.ReturnToTownAfterReward();

        Assert.That(session.GetCurrentExpeditionNode()?.NodeId, Is.EqualTo("risk"));
        return session.CurrentExpeditionNodeIndex;
    }

    private static SiteGraphTemplate CreateBranchGraph()
    {
        return new SiteGraphTemplate(
            "site_graph_alpha_gate_test",
            "site_alpha_gate",
            new[]
            {
                Node("briefing", 0, SiteNodeKindValue.Briefing, next: new[] { "safe", "risk" }),
                Node("safe", 1, SiteNodeKindValue.Skirmish, "site_alpha_gate:skirmish_a", next: new[] { "elite" }),
                Node("risk", 1, SiteNodeKindValue.Skirmish, "site_alpha_gate:skirmish_b", "risk", new[] { "elite" }),
                Node("elite", 2, SiteNodeKindValue.Elite, "site_alpha_gate:elite", next: new[] { "boss" }),
                Node("boss", 3, SiteNodeKindValue.Boss, "site_alpha_gate:boss", next: new[] { "extract" }),
                Node("extract", 4, SiteNodeKindValue.Extract, rewardSourceId: "reward_source_extract"),
            });
    }

    private static SiteGraphNodeTemplate Node(
        string nodeId,
        int rank,
        SiteNodeKindValue kind,
        string encounterId = "",
        string laneTag = "safe",
        IReadOnlyList<string>? next = null,
        string rewardSourceId = "")
    {
        return new SiteGraphNodeTemplate(
            nodeId,
            rank,
            kind,
            laneTag,
            encounterId,
            string.Empty,
            next ?? Array.Empty<string>(),
            0,
            rewardSourceId);
    }

    private static HeroInstanceRecord CreateHero(string heroId, string classId)
    {
        return new HeroInstanceRecord
        {
            HeroId = heroId,
            Name = heroId,
            ArchetypeId = $"{classId}_archetype",
            RaceId = "human",
            ClassId = classId,
            PositiveTraitId = "trait_positive",
            NegativeTraitId = "trait_negative",
            EquippedItemIds = new List<string>(),
        };
    }
}
