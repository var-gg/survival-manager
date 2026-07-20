using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Unity;

internal static class ExpeditionGraphTraversal
{
    internal static int ResolveEntryNodeIndex(IReadOnlyList<ExpeditionNodeViewModel> nodes)
    {
        if (nodes.Count == 0)
        {
            return 0;
        }

        var targetedIndices = nodes
            .SelectMany(node => node.NextNodeIndices ?? Array.Empty<int>())
            .Where(index => index >= 0 && index < nodes.Count)
            .ToHashSet();
        var entryIndices = nodes
            .Select(node => node.Index)
            .Where(index => !targetedIndices.Contains(index))
            .ToList();
        return entryIndices.Count == 1 ? entryIndices[0] : 0;
    }

    internal static IReadOnlyList<int> ResolveGuaranteedTraversedNodeIndices(
        IReadOnlyList<ExpeditionNodeViewModel> nodes,
        int currentNodeIndex)
    {
        if (currentNodeIndex < 0 || currentNodeIndex >= nodes.Count)
        {
            return Array.Empty<int>();
        }

        var entryNodeIndex = ResolveEntryNodeIndex(nodes);
        if (!CanReachExpeditionNode(nodes, entryNodeIndex, currentNodeIndex, excludedNodeIndex: -1))
        {
            return Array.Empty<int>();
        }

        var guaranteed = new List<int>();
        for (var candidateIndex = 0; candidateIndex < nodes.Count; candidateIndex++)
        {
            if (candidateIndex == currentNodeIndex
                || !CanReachExpeditionNode(nodes, entryNodeIndex, candidateIndex, excludedNodeIndex: -1)
                || !CanReachExpeditionNode(nodes, candidateIndex, currentNodeIndex, excludedNodeIndex: -1))
            {
                continue;
            }

            if (!CanReachExpeditionNode(nodes, entryNodeIndex, currentNodeIndex, candidateIndex))
            {
                guaranteed.Add(candidateIndex);
            }
        }

        return guaranteed;
    }

    private static bool CanReachExpeditionNode(
        IReadOnlyList<ExpeditionNodeViewModel> nodes,
        int startNodeIndex,
        int targetNodeIndex,
        int excludedNodeIndex)
    {
        if (startNodeIndex == excludedNodeIndex
            || targetNodeIndex == excludedNodeIndex
            || startNodeIndex < 0
            || startNodeIndex >= nodes.Count
            || targetNodeIndex < 0
            || targetNodeIndex >= nodes.Count)
        {
            return false;
        }

        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        pending.Push(startNodeIndex);
        while (pending.Count > 0)
        {
            var nodeIndex = pending.Pop();
            if (!visited.Add(nodeIndex))
            {
                continue;
            }

            if (nodeIndex == targetNodeIndex)
            {
                return true;
            }

            foreach (var nextNodeIndex in nodes[nodeIndex].NextNodeIndices ?? Array.Empty<int>())
            {
                if (nextNodeIndex != excludedNodeIndex
                    && nextNodeIndex >= 0
                    && nextNodeIndex < nodes.Count)
                {
                    pending.Push(nextNodeIndex);
                }
            }
        }

        return false;
    }
}
