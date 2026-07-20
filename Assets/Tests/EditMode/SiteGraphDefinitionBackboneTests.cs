using System;
using System.Collections.Generic;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Services;
using SM.Tests.EditMode.Fakes;
using SM.Unity.ContentConversion;
using SM.Unity.ContentParsing;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class SiteGraphDefinitionBackboneTests
{
    [Test]
    public void BranchDefinition_FileParserAndAuthoredConversion_HavePayloadParity()
    {
        var authored = CreateAuthoredBranchDefinition();
        var parsed = SiteGraphFileParser.Parse(CreateBranchYaml());
        try
        {
            var expected = CampaignConverter.BuildSiteGraphTemplate(authored);
            var actual = CampaignConverter.BuildSiteGraphTemplate(parsed);

            AssertGraphEqual(expected, actual);
            var lookup = EditorFreeCombatContentFixture.CreateRunLoopLookup(actual);
            var track = new EncounterResolutionService(lookup.Snapshot)
                .BuildSiteTrack("chapter_alpha", "site_alpha_gate");
            Assert.That(track, Has.Count.EqualTo(4));
            Assert.That(track[0].NodeId, Is.EqualTo("briefing"));
            Assert.That(track[0].NextNodeIndices, Is.EqualTo(new[] { 1, 2 }));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authored);
            UnityEngine.Object.DestroyImmediate(parsed);
        }
    }

    private static SiteGraphDefinition CreateAuthoredBranchDefinition()
    {
        var definition = ScriptableObject.CreateInstance<SiteGraphDefinition>();
        definition.Id = "site_graph_branch_test";
        definition.SiteId = "site_alpha_gate";
        definition.Nodes = new List<SiteGraphNodeDefinition>
        {
            new()
            {
                NodeId = "briefing",
                Rank = 0,
                Kind = SiteNodeKindValue.Briefing,
                LaneTag = "safe",
                NextNodeIds = new List<string> { "safe", "risk" },
                EchoIncome = 2,
            },
            new()
            {
                NodeId = "safe",
                Rank = 1,
                Kind = SiteNodeKindValue.Skirmish,
                LaneTag = "safe",
                EncounterId = "site_alpha_gate:skirmish_a",
                NextNodeIds = new List<string> { "extract" },
                RewardSourceId = "reward_source_skirmish",
            },
            new()
            {
                NodeId = "risk",
                Rank = 1,
                Kind = SiteNodeKindValue.Event,
                LaneTag = "risk",
                EventId = "event_alpha_risk",
                NextNodeIds = new List<string> { "extract" },
                EchoIncome = 3,
                RewardSourceId = "reward_source_shrine_event",
            },
            new()
            {
                NodeId = "extract",
                Rank = 2,
                Kind = SiteNodeKindValue.Extract,
                LaneTag = "safe",
                NextNodeIds = new List<string>(),
                RewardSourceId = "reward_source_extract",
            },
        };
        return definition;
    }

    private static string[] CreateBranchYaml()
    {
        return new[]
        {
            "MonoBehaviour:",
            "  Id: site_graph_branch_test",
            "  SiteId: site_alpha_gate",
            "  Nodes:",
            "  - NodeId: briefing",
            "    Rank: 0",
            "    Kind: 1",
            "    LaneTag: safe",
            "    EncounterId:",
            "    EventId:",
            "    NextNodeIds:",
            "    - safe",
            "    - risk",
            "    EchoIncome: 2",
            "    RewardSourceId:",
            "  - NodeId: safe",
            "    Rank: 1",
            "    Kind: 2",
            "    LaneTag: safe",
            "    EncounterId: site_alpha_gate:skirmish_a",
            "    EventId:",
            "    NextNodeIds:",
            "    - extract",
            "    EchoIncome: 0",
            "    RewardSourceId: reward_source_skirmish",
            "  - NodeId: risk",
            "    Rank: 1",
            "    Kind: 5",
            "    LaneTag: risk",
            "    EncounterId:",
            "    EventId: event_alpha_risk",
            "    NextNodeIds:",
            "    - extract",
            "    EchoIncome: 3",
            "    RewardSourceId: reward_source_shrine_event",
            "  - NodeId: extract",
            "    Rank: 2",
            "    Kind: 12",
            "    LaneTag: safe",
            "    EncounterId:",
            "    EventId:",
            "    NextNodeIds: []",
            "    EchoIncome: 0",
            "    RewardSourceId: reward_source_extract",
        };
    }

    private static void AssertGraphEqual(SiteGraphTemplate expected, SiteGraphTemplate actual)
    {
        Assert.That(actual.Id, Is.EqualTo(expected.Id));
        Assert.That(actual.SiteId, Is.EqualTo(expected.SiteId));
        Assert.That(actual.Nodes, Has.Count.EqualTo(expected.Nodes.Count));

        for (var index = 0; index < expected.Nodes.Count; index++)
        {
            var expectedNode = expected.Nodes[index];
            var actualNode = actual.Nodes[index];
            Assert.That(actualNode.NodeId, Is.EqualTo(expectedNode.NodeId), $"node[{index}].NodeId");
            Assert.That(actualNode.Rank, Is.EqualTo(expectedNode.Rank), $"node[{index}].Rank");
            Assert.That(actualNode.Kind, Is.EqualTo(expectedNode.Kind), $"node[{index}].Kind");
            Assert.That(actualNode.LaneTag, Is.EqualTo(expectedNode.LaneTag), $"node[{index}].LaneTag");
            Assert.That(actualNode.EncounterId, Is.EqualTo(expectedNode.EncounterId), $"node[{index}].EncounterId");
            Assert.That(actualNode.EventId, Is.EqualTo(expectedNode.EventId), $"node[{index}].EventId");
            Assert.That(actualNode.NextNodeIds, Is.EqualTo(expectedNode.NextNodeIds), $"node[{index}].NextNodeIds");
            Assert.That(actualNode.EchoIncome, Is.EqualTo(expectedNode.EchoIncome), $"node[{index}].EchoIncome");
            Assert.That(actualNode.RewardSourceId, Is.EqualTo(expectedNode.RewardSourceId), $"node[{index}].RewardSourceId");
        }
    }
}
