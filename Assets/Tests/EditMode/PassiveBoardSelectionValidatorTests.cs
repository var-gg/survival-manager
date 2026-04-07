using System.Collections.Generic;
using NUnit.Framework;
using SM.Content.Definitions;
using SM.Meta.Services;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PassiveBoardSelectionValidatorTests
{
    [Test]
    public void Toggle_RejectsNodeFromDifferentBoard()
    {
        var nodesById = new Dictionary<string, PassiveNodeDefinition>
        {
            ["node_a"] = CreateNode("node_a", "board_alpha", 0),
            ["node_b"] = CreateNode("node_b", "board_beta", 0),
        };

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", new[] { "node_a" }, "node_b", nodesById);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Error, Does.Contain("보드"));
        Assert.That(result.NormalizedNodeIds, Is.EqualTo(new[] { "node_a" }));
    }

    [Test]
    public void Toggle_RejectsMissingPrerequisite()
    {
        var nodesById = new Dictionary<string, PassiveNodeDefinition>
        {
            ["root"] = CreateNode("root", "board_alpha", 0),
            ["locked"] = CreateNode("locked", "board_alpha", 1, prerequisiteIds: new[] { "root" }),
        };

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", System.Array.Empty<string>(), "locked", nodesById);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Error, Does.Contain("선행"));
    }

    [Test]
    public void Toggle_RejectsMutualExclusion()
    {
        var exclusionTag = CreateTag("tag_exclusive");
        var nodesById = new Dictionary<string, PassiveNodeDefinition>
        {
            ["left"] = CreateNode("left", "board_alpha", 0, mutualExclusionTags: new[] { exclusionTag }),
            ["right"] = CreateNode("right", "board_alpha", 1, mutualExclusionTags: new[] { exclusionTag }),
        };

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", new[] { "left" }, "right", nodesById);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Error, Does.Contain("배타"));
    }

    [Test]
    public void Toggle_RejectsSecondKeystone()
    {
        var nodesById = new Dictionary<string, PassiveNodeDefinition>
        {
            ["keystone_a"] = CreateNode("keystone_a", "board_alpha", 0, nodeKind: PassiveNodeKindValue.Keystone),
            ["keystone_b"] = CreateNode("keystone_b", "board_alpha", 1, nodeKind: PassiveNodeKindValue.Keystone),
        };

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", new[] { "keystone_a" }, "keystone_b", nodesById);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Error, Does.Contain("Keystone"));
    }

    [Test]
    public void Normalize_ClampsSelectionCap_AndDropsInvalidNodes()
    {
        var nodesById = new Dictionary<string, PassiveNodeDefinition>();
        for (var i = 0; i < 7; i++)
        {
            var nodeId = $"node_{i}";
            nodesById[nodeId] = CreateNode(nodeId, "board_alpha", i);
        }

        var result = PassiveBoardSelectionValidator.Normalize(
            "board_alpha",
            new[] { "node_0", "node_1", "node_2", "node_3", "node_4", "node_5", "wrong_board" },
            nodesById);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.NormalizedNodeIds, Has.Count.EqualTo(PassiveBoardSelectionValidator.MaxActiveNodeCount));
        Assert.That(result.NormalizedNodeIds, Does.Not.Contain("wrong_board"));
        Assert.That(result.NormalizedNodeIds, Does.Not.Contain("node_5"));
    }

    private static PassiveNodeDefinition CreateNode(
        string id,
        string boardId,
        int depth,
        PassiveNodeKindValue nodeKind = PassiveNodeKindValue.Small,
        IReadOnlyList<string>? prerequisiteIds = null,
        IReadOnlyList<StableTagDefinition>? mutualExclusionTags = null)
    {
        var node = ScriptableObject.CreateInstance<PassiveNodeDefinition>();
        node.Id = id;
        node.BoardId = boardId;
        node.BoardDepth = depth;
        node.NodeKind = nodeKind;
        node.NameKey = id;
        if (prerequisiteIds != null)
        {
            node.PrerequisiteNodeIds = new List<string>(prerequisiteIds);
        }

        if (mutualExclusionTags != null)
        {
            node.MutualExclusionTags = new List<StableTagDefinition>(mutualExclusionTags);
        }

        return node;
    }

    private static StableTagDefinition CreateTag(string id)
    {
        var tag = ScriptableObject.CreateInstance<StableTagDefinition>();
        tag.Id = id;
        tag.NameKey = id;
        return tag;
    }
}
