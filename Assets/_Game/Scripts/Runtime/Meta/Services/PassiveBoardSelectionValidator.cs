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
            if (TryGetSelectionError(boardId, accepted, candidate.NodeId, nodesById, maxActiveNodeCount, out _))
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

        if (TryGetSelectionError(boardId, normalizedCurrent, nodeId, nodesById, maxActiveNodeCount, out var error))
        {
            return PassiveBoardSelectionValidationResult.Fail(error, normalizedCurrent);
        }

        normalizedCurrent.Add(nodeId);
        return Normalize(boardId, normalizedCurrent, nodesById, maxActiveNodeCount);
    }

    private static bool TryGetSelectionError(
        string boardId,
        IReadOnlyCollection<string> selectedNodeIds,
        string candidateNodeId,
        IReadOnlyDictionary<string, PassiveNodeTemplate> nodesById,
        int maxActiveNodeCount,
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

        if (selectedNodeIds.Count >= maxActiveNodeCount)
        {
            error = $"패시브 노드는 최대 {maxActiveNodeCount}개까지 활성화할 수 있습니다.";
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
