using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;

namespace SM.Atlas.Services;

public static class AtlasSessionService
{
    public static AtlasSessionState CreateInitial(
        AtlasRegionDefinition region,
        AtlasSessionIdentity identity,
        AtlasTraversalMode traversalMode,
        IReadOnlyList<AtlasPlacedSigil>? defaultPlacements = null,
        string selectedSigilId = "",
        string selectedNodeId = "")
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (identity == null)
        {
            throw new ArgumentNullException(nameof(identity));
        }

        var initialSigilId = !string.IsNullOrWhiteSpace(selectedSigilId)
                             && region.SigilPool.Any(sigil => string.Equals(sigil.SigilId, selectedSigilId, StringComparison.Ordinal))
            ? selectedSigilId
            : region.SigilPool.FirstOrDefault()?.SigilId ?? string.Empty;
        var initialNodeId = !string.IsNullOrWhiteSpace(selectedNodeId)
                            && region.Nodes.Any(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal))
            ? selectedNodeId
            : AtlasSpineProgressionService.CurrentCandidates(region, siteSpineIndex: 0, bossResolved: false)
                  .FirstOrDefault()?.HexId
              ?? region.Nodes.FirstOrDefault()?.NodeId
              ?? string.Empty;

        return new AtlasSessionState(
            identity,
            traversalMode,
            initialSigilId,
            initialNodeId,
            SiteSpineIndex: 0,
            BossResolved: false,
            StageCandidatePath: Array.Empty<string>(),
            Placements: NormalizePlacements(region, traversalMode, defaultPlacements ?? Array.Empty<AtlasPlacedSigil>()));
    }

    public static AtlasSessionState SelectSigil(
        AtlasRegionDefinition region,
        AtlasSessionState state,
        string sigilId)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return region.SigilPool.Any(sigil => string.Equals(sigil.SigilId, sigilId, StringComparison.Ordinal))
            ? state with { SelectedSigilId = sigilId }
            : state;
    }

    public static AtlasSessionState SelectNode(
        AtlasRegionDefinition region,
        AtlasSessionState state,
        string nodeId)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (!region.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            return state;
        }

        var next = state with { SelectedNodeId = nodeId };
        var candidate = AtlasSpineProgressionService.FindCandidate(region, nodeId);
        if (candidate == null
            || !AtlasSpineProgressionService.CanEnterStoryCandidate(region, nodeId, state.SiteSpineIndex, state.BossResolved))
        {
            return next;
        }

        var path = AppendStageCandidate(next.StageCandidatePath, candidate);
        var bossResolved = next.BossResolved || candidate.SiteStageIndex == AtlasSpineProgressionService.BossStageIndex;
        var siteSpineIndex = AtlasSpineProgressionService.AdvanceSpineIndex(candidate, next.SiteSpineIndex);
        return next with
        {
            StageCandidatePath = path,
            BossResolved = bossResolved,
            SiteSpineIndex = siteSpineIndex,
        };
    }

    public static AtlasSessionState PlaceSelectedSigil(
        AtlasRegionDefinition region,
        AtlasSessionState state,
        string nodeId)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var node = region.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
        var slot = region.SigilAnchorSlots.FirstOrDefault(candidate => string.Equals(candidate.HexId, nodeId, StringComparison.Ordinal));
        if (node == null || slot == null || string.IsNullOrWhiteSpace(state.SelectedSigilId))
        {
            return SelectNode(region, state, nodeId);
        }

        if (!region.SigilPool.Any(sigil => string.Equals(sigil.SigilId, state.SelectedSigilId, StringComparison.Ordinal)))
        {
            return state with { SelectedNodeId = nodeId };
        }

        var placements = state.Placements
            .Where(placement =>
                !string.Equals(placement.SigilId, state.SelectedSigilId, StringComparison.Ordinal)
                && !string.Equals(placement.AnchorId, slot.AnchorId, StringComparison.Ordinal))
            .ToList();
        var activeSigilCap = ResolveActiveSigilCap(state.TraversalMode);
        if (placements.Count >= activeSigilCap)
        {
            placements.RemoveAt(0);
        }

        placements.Add(new AtlasPlacedSigil(state.SelectedSigilId, node.Hex, slot.AnchorId));
        return state with
        {
            SelectedNodeId = nodeId,
            Placements = placements.ToArray(),
        };
    }

    public static AtlasSessionResolution Resolve(
        AtlasRegionDefinition region,
        AtlasSessionState state,
        AtlasSigilMathConfig? config = null)
    {
        if (region == null)
        {
            throw new ArgumentNullException(nameof(region));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        config ??= AtlasSigilMathConfig.CreateDefault();
        var currentStageIndex = ResolveCurrentStageIndex(state);
        var mathResolution = AtlasSigilInfluenceMathService.Resolve(
            region,
            state.Placements,
            state.TraversalMode,
            currentStageIndex,
            config);
        var modifierResolution = AtlasSigilMathResolutionAdapter.ToModifierResolution(region, mathResolution, config);
        var selectedNode = region.Nodes.FirstOrDefault(node => string.Equals(node.NodeId, state.SelectedNodeId, StringComparison.Ordinal))
                           ?? region.Nodes.First();
        var selectedStack = modifierResolution.FindNode(selectedNode.NodeId)
                            ?? new AtlasNodeModifierStack(selectedNode.NodeId, selectedNode.Hex, Array.Empty<AtlasSigilInfluence>(), Array.Empty<AtlasResolvedModifier>());
        var selectedCandidate = AtlasSpineProgressionService.FindCandidate(region, selectedNode.NodeId);
        var stageCandidatePath = AtlasSpineProgressionService.StageCandidatePath(state.StageCandidatePath, selectedCandidate);
        var stageCandidatePathHash = AtlasContextHasher.BuildStageCandidatePathHash(stageCandidatePath);
        var sigilSnapshotHash = AtlasContextHasher.BuildSigilSnapshotHash(
            region,
            state.Identity.SiteId,
            state.TraversalMode.ToString(),
            state.Placements,
            state.Identity.CycleSalt);

        return new AtlasSessionResolution(
            state,
            currentStageIndex,
            ResolveActiveSigilCap(state.TraversalMode),
            mathResolution,
            modifierResolution,
            selectedNode,
            selectedStack,
            selectedCandidate,
            stageCandidatePath,
            stageCandidatePathHash,
            sigilSnapshotHash);
    }

    public static int ResolveCurrentStageIndex(AtlasSessionState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return state.BossResolved
            ? AtlasSpineProgressionService.ExtractStageIndex
            : Math.Min(state.SiteSpineIndex + 1, AtlasSpineProgressionService.BossStageIndex);
    }

    public static int ResolveActiveSigilCap(AtlasTraversalMode traversalMode)
    {
        return traversalMode == AtlasTraversalMode.EndlessRegion ? 3 : 2;
    }

    private static IReadOnlyList<AtlasPlacedSigil> NormalizePlacements(
        AtlasRegionDefinition region,
        AtlasTraversalMode traversalMode,
        IReadOnlyList<AtlasPlacedSigil> placements)
    {
        var normalized = new List<AtlasPlacedSigil>();
        foreach (var placement in placements ?? Array.Empty<AtlasPlacedSigil>())
        {
            if (!region.SigilPool.Any(sigil => string.Equals(sigil.SigilId, placement.SigilId, StringComparison.Ordinal)))
            {
                continue;
            }

            var slot = ResolveAnchorSlot(region, placement);
            if (slot == null)
            {
                continue;
            }

            var node = region.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, slot.HexId, StringComparison.Ordinal));
            if (node == null)
            {
                continue;
            }

            normalized.RemoveAll(existing =>
                string.Equals(existing.SigilId, placement.SigilId, StringComparison.Ordinal)
                || string.Equals(existing.AnchorId, slot.AnchorId, StringComparison.Ordinal));
            if (normalized.Count >= ResolveActiveSigilCap(traversalMode))
            {
                normalized.RemoveAt(0);
            }

            normalized.Add(new AtlasPlacedSigil(placement.SigilId, node.Hex, slot.AnchorId));
        }

        return normalized.ToArray();
    }

    private static SigilAnchorSlot? ResolveAnchorSlot(AtlasRegionDefinition region, AtlasPlacedSigil placement)
    {
        if (!string.IsNullOrWhiteSpace(placement.AnchorId))
        {
            var byId = region.SigilAnchorSlots.FirstOrDefault(slot => string.Equals(slot.AnchorId, placement.AnchorId, StringComparison.Ordinal));
            if (byId != null)
            {
                return byId;
            }
        }

        return region.SigilAnchorSlots.FirstOrDefault(slot =>
        {
            var node = region.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, slot.HexId, StringComparison.Ordinal));
            return node != null && node.Hex.Equals(placement.AnchorHex);
        });
    }

    private static IReadOnlyList<string> AppendStageCandidate(
        IReadOnlyList<string> stageCandidatePath,
        AtlasStageCandidate candidate)
    {
        var path = new List<string>(stageCandidatePath ?? Array.Empty<string>());
        if (candidate.SiteStageIndex is >= 1 and <= 3 && !path.Contains(candidate.HexId, StringComparer.Ordinal))
        {
            path.Add(candidate.HexId);
        }

        return path.ToArray();
    }
}
