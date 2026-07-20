using System;
using System.Collections.Generic;
using System.Linq;
using SM.Content.Definitions;

namespace SM.Editor.Validation;

internal sealed class SiteGraphCatalogValidator : ICatalogValidationRule
{
    public void Validate(CatalogValidationContext context, ICollection<ContentValidationIssue> issues)
    {
        foreach (var duplicateSite in context.SiteGraphs
                     .Where(graph => !string.IsNullOrWhiteSpace(graph.SiteId))
                     .GroupBy(graph => graph.SiteId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            foreach (var graph in duplicateSite)
            {
                AddError(context, issues, graph, "site_graph.duplicate_site", $"Site '{duplicateSite.Key}' has more than one graph definition.");
            }
        }

        foreach (var graph in context.SiteGraphs)
        {
            ValidateGraph(context, graph, issues);
        }
    }

    private static void ValidateGraph(
        CatalogValidationContext context,
        SiteGraphDefinition graph,
        ICollection<ContentValidationIssue> issues)
    {
        var nodes = (graph.Nodes ?? new List<SiteGraphNodeDefinition>())
            .Where(node => node != null)
            .ToList();
        if (string.IsNullOrWhiteSpace(graph.SiteId) || !context.Sites.ContainsKey(graph.SiteId))
        {
            AddError(context, issues, graph, "site_graph.site_ref", $"Site graph '{graph.Id}' references missing site '{graph.SiteId}'.");
        }

        if (nodes.Count == 0)
        {
            AddError(context, issues, graph, "site_graph.empty", $"Site graph '{graph.Id}' must contain at least one node.");
            return;
        }

        var duplicateIds = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        foreach (var duplicateId in duplicateIds)
        {
            AddError(context, issues, graph, "site_graph.duplicate_node_id", $"Site graph '{graph.Id}' contains duplicate NodeId '{duplicateId}'.");
        }

        foreach (var node in nodes.Where(node => string.IsNullOrWhiteSpace(node.NodeId)))
        {
            AddError(context, issues, graph, "site_graph.node_id", $"Site graph '{graph.Id}' contains a node without NodeId.");
        }

        var nodeById = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.NodeId))
            .GroupBy(node => node.NodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (nodeById.Count == 0)
        {
            return;
        }

        var outgoing = nodeById.Keys.ToDictionary(
            id => id,
            id => new List<string>(),
            StringComparer.Ordinal);
        foreach (var node in nodes.Where(node => !string.IsNullOrWhiteSpace(node.NodeId)))
        {
            ValidateNodeReferences(context, graph, node, issues);
            foreach (var targetId in node.NextNodeIds ?? new List<string>())
            {
                if (!nodeById.ContainsKey(targetId))
                {
                    AddError(context, issues, graph, "site_graph.unknown_target", $"Node '{node.NodeId}' points to unknown target '{targetId}'.");
                    continue;
                }

                if (!outgoing[node.NodeId].Contains(targetId, StringComparer.Ordinal))
                {
                    outgoing[node.NodeId].Add(targetId);
                }
            }
        }

        var targetedIds = outgoing.Values.SelectMany(ids => ids).ToHashSet(StringComparer.Ordinal);
        var entryIds = nodeById.Keys.Where(id => !targetedIds.Contains(id)).ToList();
        if (entryIds.Count != 1)
        {
            AddError(
                context,
                issues,
                graph,
                "site_graph.entry_count",
                $"Site graph '{graph.Id}' must have exactly one entry node. Found {entryIds.Count}.");
        }

        var entryId = entryIds.FirstOrDefault() ?? nodeById.Keys.First();
        ValidateSafeLaneIdentity(context, graph, nodes, entryId, nodeById, issues);

        var reachable = Traverse(entryId, outgoing);
        foreach (var orphanId in nodeById.Keys
                     .Where(id => !reachable.Contains(id))
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            AddError(context, issues, graph, "site_graph.orphan_node", $"Node '{orphanId}' is unreachable from entry '{entryId}'.");
        }

        var terminalReachable = ResolveNodesThatCanReachTerminal(outgoing);
        var trappedIds = reachable
            .Where(id => !terminalReachable.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        if (trappedIds.Count > 0)
        {
            AddError(
                context,
                issues,
                graph,
                "site_graph.no_terminal_path",
                $"Nodes [{string.Join(", ", trappedIds)}] cannot reach a terminal node and would loop the expedition.");
        }
    }

    private static void ValidateSafeLaneIdentity(
        CatalogValidationContext context,
        SiteGraphDefinition graph,
        IReadOnlyList<SiteGraphNodeDefinition> nodes,
        string entryId,
        IReadOnlyDictionary<string, SiteGraphNodeDefinition> nodeById,
        ICollection<ContentValidationIssue> issues)
    {
        if (context.Sites.TryGetValue(graph.SiteId, out var site))
        {
            var legacyEncounterIds = (site.EncounterIds ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            for (var index = 0; index < legacyEncounterIds.Count; index++)
            {
                var legacyEncounterId = legacyEncounterIds[index];
                if (index >= nodes.Count
                    || !string.Equals(nodes[index].EncounterId, legacyEncounterId, StringComparison.Ordinal)
                    || !string.Equals(nodes[index].LaneTag, "safe", StringComparison.Ordinal))
                {
                    AddError(
                        context,
                        issues,
                        graph,
                        "site_graph.safe_lane_coordinate",
                        $"Safe lane node[{index}] must preserve legacy encounter '{legacyEncounterId}' at the same index.");
                    continue;
                }

                if (context.Encounters.TryGetValue(legacyEncounterId, out var encounter))
                {
                    var effectiveRewardSourceId = string.IsNullOrWhiteSpace(nodes[index].RewardSourceId)
                        ? encounter.RewardSourceId
                        : nodes[index].RewardSourceId;
                    if (!string.Equals(effectiveRewardSourceId, encounter.RewardSourceId, StringComparison.Ordinal))
                    {
                        AddError(
                            context,
                            issues,
                            graph,
                            "site_graph.safe_lane_reward_source",
                            $"Safe lane node[{index}] must preserve legacy reward source '{encounter.RewardSourceId}'.");
                    }
                }
            }
        }

        if (!nodeById.TryGetValue(entryId, out var entry)
            || entry.NextNodeIds == null
            || entry.NextNodeIds.Count == 0
            || !nodeById.TryGetValue(entry.NextNodeIds[0], out var defaultTarget)
            || !string.Equals(defaultTarget.LaneTag, "safe", StringComparison.Ordinal))
        {
            AddError(
                context,
                issues,
                graph,
                "site_graph.default_route_risk",
                $"Entry node '{entryId}' must point to the safe lane with NextNodeIds[0].");
        }
    }

    private static void ValidateNodeReferences(
        CatalogValidationContext context,
        SiteGraphDefinition graph,
        SiteGraphNodeDefinition node,
        ICollection<ContentValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(node.EncounterId) && !context.Encounters.ContainsKey(node.EncounterId))
        {
            AddError(context, issues, graph, "site_graph.encounter_ref", $"Node '{node.NodeId}' references missing encounter '{node.EncounterId}'.");
        }

        if (!string.IsNullOrWhiteSpace(node.EventId) && !context.SiteEventIds.Contains(node.EventId))
        {
            AddError(context, issues, graph, "site_graph.event_ref", $"Node '{node.NodeId}' references missing event '{node.EventId}'.");
        }

        if (!string.IsNullOrWhiteSpace(node.RewardSourceId) && !context.RewardSources.ContainsKey(node.RewardSourceId))
        {
            AddError(context, issues, graph, "site_graph.reward_source_ref", $"Node '{node.NodeId}' references missing reward source '{node.RewardSourceId}'.");
        }
    }

    private static HashSet<string> Traverse(
        string startId,
        IReadOnlyDictionary<string, List<string>> outgoing)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(startId);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!visited.Add(id) || !outgoing.TryGetValue(id, out var targets))
            {
                continue;
            }

            foreach (var target in targets)
            {
                pending.Push(target);
            }
        }

        return visited;
    }

    private static HashSet<string> ResolveNodesThatCanReachTerminal(
        IReadOnlyDictionary<string, List<string>> outgoing)
    {
        var reverse = outgoing.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        foreach (var pair in outgoing)
        {
            foreach (var target in pair.Value)
            {
                reverse[target].Add(pair.Key);
            }
        }

        var canReachTerminal = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(outgoing.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key));
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!canReachTerminal.Add(id))
            {
                continue;
            }

            foreach (var predecessor in reverse[id])
            {
                pending.Push(predecessor);
            }
        }

        return canReachTerminal;
    }

    private static void AddError(
        CatalogValidationContext context,
        ICollection<ContentValidationIssue> issues,
        SiteGraphDefinition graph,
        string code,
        string message)
    {
        ContentValidationIssueFactory.AddError(issues, code, message, context.GetPath(graph));
    }
}
