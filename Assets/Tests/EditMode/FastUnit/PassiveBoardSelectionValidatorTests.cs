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

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", new[] { "node_a" }, "node_b", nodesById, PassiveBoardSelectionValidator.BaseActiveNodeCount);

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

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", System.Array.Empty<string>(), "locked", nodesById, PassiveBoardSelectionValidator.BaseActiveNodeCount);

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

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", new[] { "left" }, "right", nodesById, PassiveBoardSelectionValidator.BaseActiveNodeCount);

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

        var result = PassiveBoardSelectionValidator.Toggle("board_alpha", new[] { "keystone_a" }, "keystone_b", nodesById, PassiveBoardSelectionValidator.BaseActiveNodeCount);

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
            nodesById,
            PassiveBoardSelectionValidator.BaseActiveNodeCount);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.NormalizedNodeIds, Has.Count.EqualTo(PassiveBoardSelectionValidator.BaseActiveNodeCount));
        Assert.That(result.NormalizedNodeIds, Does.Not.Contain("wrong_board"));
        Assert.That(result.NormalizedNodeIds, Does.Not.Contain("node_5"));
    }

    [Test]
    public void ResolveMaxActiveNodeCount_GrowsWithLevelSteps_AndClampsAtCap()
    {
        // 오너 게이트③ 채택(2026-07-12) — 노드 예산 성장 계단: 시작 5, 문턱 {4,6,8}마다 +1, 상한 8.
        // 과거 고정 5는 키스톤(도달 비용 6)·심층 notable 20노드를 수학적으로 사장시켰다.
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(1), Is.EqualTo(5));
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(3), Is.EqualTo(5));
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(4), Is.EqualTo(6),
            "L4(캠페인 중반)부터 키스톤 1선(비용 6)이 열려야 한다");
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(5), Is.EqualTo(6));
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(6), Is.EqualTo(7));
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(8), Is.EqualTo(8));
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(99), Is.EqualTo(PassiveBoardSelectionValidator.ActiveNodeCountCap),
            "상한 초과 레벨도 cap(8)에 고정 — 무한모드 무한 성장 차단");
        Assert.That(PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(0), Is.EqualTo(5),
            "비정상 레벨(0 이하)은 기본 예산으로 방어");
    }

    [Test]
    public void Normalize_HigherLevelBudget_AcceptsMoreNodes()
    {
        var nodesById = new Dictionary<string, PassiveNodeTemplate>();
        for (var i = 0; i < 9; i++)
        {
            var nodeId = $"node_{i}";
            nodesById[nodeId] = CreateNode(nodeId, "board_alpha", i);
        }

        var requested = new[] { "node_0", "node_1", "node_2", "node_3", "node_4", "node_5", "node_6", "node_7", "node_8" };
        var capBudget = PassiveBoardSelectionValidator.ResolveMaxActiveNodeCount(8);
        var result = PassiveBoardSelectionValidator.Normalize("board_alpha", requested, nodesById, capBudget);

        Assert.That(result.NormalizedNodeIds, Has.Count.EqualTo(PassiveBoardSelectionValidator.ActiveNodeCountCap),
            "L8 예산(8)은 기본 예산(5)에서 사장되던 6~8번째 노드를 수용해야 한다");
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
