using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Core.Content;
using SM.Core.Stats;
using SM.Meta.Model;
using SM.Meta.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class PassiveBoardSelectionValidatorTests
{
    [Test]
    public void Toggle_RejectsNodeFromDifferentBoard()
    {
        var nodesById = new Dictionary<string, PassiveNodeTemplate>
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
        var nodesById = new Dictionary<string, PassiveNodeTemplate>
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
        var exclusionTag = "tag_exclusive";
        var nodesById = new Dictionary<string, PassiveNodeTemplate>
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
        var nodesById = new Dictionary<string, PassiveNodeTemplate>
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
        var nodesById = new Dictionary<string, PassiveNodeTemplate>();
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

    private static PassiveNodeTemplate CreateNode(
        string id,
        string boardId,
        int depth,
        PassiveNodeKindValue nodeKind = PassiveNodeKindValue.Small,
        IReadOnlyList<string>? prerequisiteIds = null,
        IReadOnlyList<string>? mutualExclusionTags = null)
    {
        return new PassiveNodeTemplate(
            id,
            new CombatModifierPackage(id, ModifierSource.Other, System.Array.Empty<StatModifier>()),
            System.Array.Empty<string>(),
            null,
            boardId,
            depth,
            nodeKind,
            prerequisiteIds,
            mutualExclusionTags);
    }
}
