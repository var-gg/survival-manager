using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core.Content;
using SM.Core.Results;
using SM.Meta.Model;

namespace SM.Meta.Services;

public sealed record PassiveBoardSelectionValidationResult(
    bool IsValid,
    IReadOnlyList<string> NormalizedNodeIds,
    OperationFailure? Failure)
{
    public string Error => Failure?.Diagnostic ?? string.Empty;

    public static PassiveBoardSelectionValidationResult Success(IReadOnlyList<string> normalizedNodeIds)
        => new(true, normalizedNodeIds, null);

    public static PassiveBoardSelectionValidationResult Fail(
        OperationFailure failure,
        IReadOnlyList<string> normalizedNodeIds)
        => new(false, normalizedNodeIds, failure);
}

public static class PassiveBoardSelectionValidator
{
    // 노드 예산 성장 계단(오너 게이트③ 채택, 2026-07-12) — 시작 5, 레벨 문턱 {4,6,8}마다 +1, 상한 8.
    // 과거 고정 5는 심층 notable 06~08·키스톤(도달 비용 6+) 20노드를 수학적으로 사장시켰다. 상한 8 =
    // 키스톤 1선 완주(6) + 여유 2로 빌드 선택 압력 유지. 문턱은 실측 레벨 커브 기반 V1 후보치(승리당
    // 50xp, ExperienceToNextLevel=100+50L → 완주 캠페인 최대 40승 ≈ L7 도달) — 수치 sweep 재료.
    public const int BaseActiveNodeCount = 5;
    public const int ActiveNodeCountCap = 8;
    public const int MaxKeystoneCount = 1;
    private static readonly int[] BudgetLevelThresholds = { 4, 6, 8 };

    /// <summary>영웅 레벨이 허용하는 최대 활성 노드 수 — 예산은 메타 검증 레이어 소유(sim 무접촉).</summary>
    public static int ResolveMaxActiveNodeCount(int heroLevel)
    {
        var budget = BaseActiveNodeCount;
        foreach (var threshold in BudgetLevelThresholds)
        {
            if (heroLevel >= threshold)
            {
                budget++;
            }
        }

        return Math.Min(ActiveNodeCountCap, budget);
    }

    public static PassiveBoardSelectionValidationResult Normalize(
        string boardId,
        IReadOnlyCollection<string> requestedNodeIds,
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById,
        int maxActiveNodeCount)
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

        var accepted = new List<string>(maxActiveNodeCount);
        foreach (var candidate in orderedCandidates)
        {
            if (TryGetSelectionFailure(boardId, accepted, candidate.NodeId, nodesById, maxActiveNodeCount, out _))
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
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById,
        int maxActiveNodeCount)
    {
        var normalizedCurrent = Normalize(boardId, currentNodeIds, nodesById, maxActiveNodeCount).NormalizedNodeIds.ToList();
        if (normalizedCurrent.Contains(nodeId, StringComparer.Ordinal))
        {
            normalizedCurrent.RemoveAll(existing => string.Equals(existing, nodeId, StringComparison.Ordinal));
            return Normalize(boardId, normalizedCurrent, nodesById, maxActiveNodeCount);
        }

        if (TryGetSelectionFailure(
                boardId,
                normalizedCurrent,
                nodeId,
                nodesById,
                maxActiveNodeCount,
                out var failure))
        {
            return PassiveBoardSelectionValidationResult.Fail(failure!, normalizedCurrent);
        }

        normalizedCurrent.Add(nodeId);
        return Normalize(boardId, normalizedCurrent, nodesById, maxActiveNodeCount);
    }

    private static bool TryGetSelectionFailure(
        string boardId,
        IReadOnlyCollection<string> selectedNodeIds,
        string candidateNodeId,
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById,
        int maxActiveNodeCount,
        out OperationFailure? failure)
    {
        failure = null;
        if (!nodesById.TryGetValue(candidateNodeId, out var candidate))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.PassiveNodeMissing,
                $"Passive node '{candidateNodeId}' was not present while toggling board '{boardId}'.");
            return true;
        }

        if (!string.Equals(candidate.BoardId, boardId, StringComparison.Ordinal))
        {
            failure = OperationFailure.Invariant(
                MetaOperationFailureCodes.PassiveNodeWrongBoard,
                $"Passive node '{candidateNodeId}' belongs to board '{candidate.BoardId}', not active board '{boardId}'.");
            return true;
        }

        foreach (var prerequisiteNodeId in (candidate.PrerequisiteNodeIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            if (!selectedNodeIds.Contains(prerequisiteNodeId, StringComparer.Ordinal))
            {
                failure = OperationFailure.Refusal(
                    MetaOperationFailureCodes.PassivePrerequisiteRequired,
                    $"Passive node '{candidateNodeId}' requires prerequisite '{prerequisiteNodeId}'.");
                return true;
            }
        }

        if (selectedNodeIds.Count >= maxActiveNodeCount)
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.PassiveActiveNodeLimitReached,
                $"Passive board '{boardId}' already has {selectedNodeIds.Count} active nodes; limit is {maxActiveNodeCount}.",
                maxActiveNodeCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return true;
        }

        if (candidate.NodeKind == PassiveNodeKindValue.Keystone)
        {
            var keystoneCount = selectedNodeIds
                .Where(nodesById.ContainsKey)
                .Count(existingNodeId => nodesById[existingNodeId].NodeKind == PassiveNodeKindValue.Keystone);
            if (keystoneCount >= MaxKeystoneCount)
            {
                failure = OperationFailure.Refusal(
                    MetaOperationFailureCodes.PassiveKeystoneLimitReached,
                    $"Passive board '{boardId}' already has {keystoneCount} active keystone nodes; limit is {MaxKeystoneCount}.",
                    MaxKeystoneCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
        }

        var selectedExclusionTags = selectedNodeIds
            .Where(nodesById.ContainsKey)
            .SelectMany(existingNodeId => GetTagIds(nodesById[existingNodeId]))
            .ToHashSet(StringComparer.Ordinal);
        if (GetTagIds(candidate).Any(tagId => selectedExclusionTags.Contains(tagId)))
        {
            failure = OperationFailure.Refusal(
                MetaOperationFailureCodes.PassiveMutualExclusion,
                $"Passive node '{candidateNodeId}' conflicts with an active node on board '{boardId}'.");
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
