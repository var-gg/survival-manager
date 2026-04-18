using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record PassiveBoardSelectionValidationResult(
    bool IsValid,
    IReadOnlyList<string> NormalizedNodeIds,
    string Error)
{
    public static PassiveBoardSelectionValidationResult Success(IReadOnlyList<string> normalizedNodeIds)
        => new(true, normalizedNodeIds, string.Empty);

    public static PassiveBoardSelectionValidationResult Fail(string error, IReadOnlyList<string> normalizedNodeIds)
        => new(false, normalizedNodeIds, error);
}

public static class PassiveBoardSelectionValidator
{
    public const int MaxActiveNodeCount = 5;
    public const int MaxKeystoneCount = 1;

    public static PassiveBoardSelectionValidationResult Normalize(
        string boardId,
        IReadOnlyCollection<string> requestedNodeIds,
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById)
    {
        if (string.IsNullOrWhiteSpace(boardId) || nodesById.Count == 0)
        {
            return PassiveBoardSelectionValidationResult.Success(Array.Empty<string>());
        }

        var orderedCandidates = requestedNodeIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select((id, index) => new OrderedPassiveNodeSelection(id, index))
            .Where(entry => nodesById.TryGetValue(entry.NodeId, out var node)
                            && string.Equals(node.BoardId, boardId, StringComparison.Ordinal))
            .OrderBy(entry => nodesById[entry.NodeId].BoardDepth)
            .ThenBy(entry => entry.Order)
            .ThenBy(entry => entry.NodeId, StringComparer.Ordinal)
            .ToList();

        var accepted = new List<string>(MaxActiveNodeCount);
        foreach (var candidate in orderedCandidates)
        {
            if (TryGetSelectionError(boardId, accepted, candidate.NodeId, nodesById, out _))
            {
                continue;
            }

            accepted.Add(candidate.NodeId);
        }

        return PassiveBoardSelectionValidationResult.Success(accepted);
    }

    public static PassiveBoardSelectionValidationResult Toggle(
        string boardId,
        IReadOnlyCollection<string> currentNodeIds,
        string nodeId,
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById)
    {
        var normalizedCurrent = Normalize(boardId, currentNodeIds, nodesById).NormalizedNodeIds.ToList();
        if (normalizedCurrent.Contains(nodeId, StringComparer.Ordinal))
        {
            normalizedCurrent.RemoveAll(existing => string.Equals(existing, nodeId, StringComparison.Ordinal));
            return Normalize(boardId, normalizedCurrent, nodesById);
        }

        if (TryGetSelectionError(boardId, normalizedCurrent, nodeId, nodesById, out var error))
        {
            return PassiveBoardSelectionValidationResult.Fail(error, normalizedCurrent);
        }

        normalizedCurrent.Add(nodeId);
        return Normalize(boardId, normalizedCurrent, nodesById);
    }

    private static bool TryGetSelectionError(
        string boardId,
        IReadOnlyCollection<string> selectedNodeIds,
        string candidateNodeId,
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById,
        out string error)
    {
        error = string.Empty;
        if (!nodesById.TryGetValue(candidateNodeId, out var candidate))
        {
            error = "패시브 노드를 찾을 수 없습니다.";
            return true;
        }

        if (!string.Equals(candidate.BoardId, boardId, StringComparison.Ordinal))
        {
            error = "선택한 노드는 현재 보드 소속이 아닙니다.";
            return true;
        }

        foreach (var prerequisiteNodeId in (candidate.PrerequisiteNodeIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!selectedNodeIds.Contains(prerequisiteNodeId, StringComparer.Ordinal))
            {
                error = "선행 노드가 필요합니다.";
                return true;
            }
        }

        if (selectedNodeIds.Count >= MaxActiveNodeCount)
        {
            error = $"패시브 노드는 최대 {MaxActiveNodeCount}개까지 활성화할 수 있습니다.";
            return true;
        }

        if (candidate.NodeKind == PassiveNodeKindValue.Keystone)
        {
            var keystoneCount = selectedNodeIds
                .Where(nodesById.ContainsKey)
                .Count(existingNodeId => nodesById[existingNodeId].NodeKind == PassiveNodeKindValue.Keystone);
            if (keystoneCount >= MaxKeystoneCount)
            {
                error = "Keystone은 하나만 활성화할 수 있습니다.";
                return true;
            }
        }

        var selectedExclusionTags = selectedNodeIds
            .Where(nodesById.ContainsKey)
            .SelectMany(existingNodeId => GetTagIds(nodesById[existingNodeId]))
            .ToHashSet(StringComparer.Ordinal);
        if (GetTagIds(candidate).Any(tagId => selectedExclusionTags.Contains(tagId)))
        {
            error = "상호 배타적인 패시브 노드입니다.";
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetTagIds(PassiveNodeTemplate definition)
    {
        return definition.MutualExclusionTagIds?
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            ?? Array.Empty<string>();
    }

    private readonly record struct OrderedPassiveNodeSelection(string NodeId, int Order);
}
