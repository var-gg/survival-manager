using System;
using System.Collections.Generic;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenPresenter
{
    private const string RunId = "atlas_graybox_run";
    private const string ChapterId = "chapter_graybox";
    private const string SiteId = "site_wolfpine";
    private const string EncounterId = "encounter_graybox";
    private const string CycleSalt = "cycle_2026_05_p2";
    private const string SquadSnapshotId = "squad:dawn|pack|echo|grave";

    private readonly AtlasNodePreviewBuilder _previewBuilder = new();
    private readonly List<AtlasPlacedSigil> _placements = new();
    private readonly AtlasRegionDefinition _region;
    private string _selectedSigilId;
    private string _selectedNodeId;
    private string _selectedRouteId;

    public AtlasScreenPresenter(AtlasRegionDefinition region)
    {
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _selectedSigilId = _region.SigilPool.FirstOrDefault()?.SigilId ?? string.Empty;
        _selectedRouteId = _region.Routes.FirstOrDefault()?.RouteId ?? string.Empty;
        _selectedNodeId = _region.Routes.FirstOrDefault()?.NodeIds.FirstOrDefault()
                          ?? _region.Nodes.FirstOrDefault()?.NodeId
                          ?? string.Empty;

        SeedDefaultPlacement();
    }

    public void SelectSigil(string sigilId)
    {
        if (_region.SigilPool.Any(sigil => string.Equals(sigil.SigilId, sigilId, StringComparison.Ordinal)))
        {
            _selectedSigilId = sigilId;
        }
    }

    public void SelectNode(string nodeId)
    {
        if (_region.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            _selectedNodeId = nodeId;
        }
    }

    public void SelectRoute(string routeId)
    {
        if (_region.Routes.Any(route => string.Equals(route.RouteId, routeId, StringComparison.Ordinal)))
        {
            _selectedRouteId = routeId;
            _selectedNodeId = _region.Routes.First(route => route.RouteId == routeId).NodeIds.FirstOrDefault() ?? _selectedNodeId;
        }
    }

    public void PlaceSelectedSigil(string nodeId)
    {
        var node = _region.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
        if (node == null || !_region.SigilAnchors.Contains(node.Hex) || string.IsNullOrWhiteSpace(_selectedSigilId))
        {
            SelectNode(nodeId);
            return;
        }

        _placements.RemoveAll(placement =>
            string.Equals(placement.SigilId, _selectedSigilId, StringComparison.Ordinal)
            || placement.AnchorHex.Equals(node.Hex));

        if (_placements.Count >= 2)
        {
            _placements.RemoveAt(0);
        }

        _placements.Add(new AtlasPlacedSigil(_selectedSigilId, node.Hex));
        _selectedNodeId = nodeId;
    }

    public AtlasScreenViewState Build()
    {
        var resolution = SigilPropagationService.Resolve(_region, _placements);
        var selectedRoute = _region.Routes.FirstOrDefault(route => route.RouteId == _selectedRouteId)
                            ?? _region.Routes.First();
        var selectedNode = _region.Nodes.FirstOrDefault(node => node.NodeId == _selectedNodeId)
                           ?? _region.Nodes.First(node => selectedRoute.NodeIds.Contains(node.NodeId));
        var selectedStack = resolution.FindNode(selectedNode.NodeId)
                            ?? new AtlasNodeModifierStack(selectedNode.NodeId, selectedNode.Hex, Array.Empty<AtlasSigilInfluence>(), Array.Empty<AtlasResolvedModifier>());
        var preview = _previewBuilder.Build(
            _region,
            selectedNode,
            selectedStack,
            selectedRoute,
            RunId,
            ChapterId,
            SiteId,
            EncounterId,
            CycleSalt,
            SquadSnapshotId);

        var routeNodeIds = selectedRoute.NodeIds.ToHashSet(StringComparer.Ordinal);
        return new AtlasScreenViewState(
            AtlasReadabilityFormatter.FormatRegionTitle(_region.DisplayName),
            BuildPlacementSummary(resolution.FindNode(selectedNode.NodeId)),
            _region.Nodes
                .OrderBy(node => node.Hex.R)
                .ThenBy(node => node.Hex.Q)
                .Select(node => BuildTile(node, resolution, routeNodeIds))
                .ToArray(),
            _region.SigilPool.Select(BuildSigilPoolItem).ToArray(),
            _region.Routes.Select(route => BuildRoute(route, resolution)).ToArray(),
            BuildPreview(preview, selectedNode, selectedStack));
    }

    private void SeedDefaultPlacement()
    {
        if (_region.SigilPool.Count < 2 || _region.SigilAnchors.Count < 2)
        {
            return;
        }

        _placements.Add(new AtlasPlacedSigil("sigil_beast_spoils", _region.SigilAnchors[1]));
        _placements.Add(new AtlasPlacedSigil("sigil_flank_pressure", _region.SigilAnchors[2]));
    }

    private AtlasHexTileViewState BuildTile(
        AtlasRegionNode node,
        AtlasSigilResolution resolution,
        HashSet<string> routeNodeIds)
    {
        var stack = resolution.FindNode(node.NodeId);
        var placed = _placements.FirstOrDefault(placement => placement.AnchorHex.Equals(node.Hex));
        var placedSigil = string.IsNullOrWhiteSpace(placed?.SigilId)
            ? null
            : _region.SigilPool.FirstOrDefault(sigil => sigil.SigilId == placed.SigilId);

        return new AtlasHexTileViewState(
            node.NodeId,
            AtlasReadabilityFormatter.FormatNodeLabel(node.Label),
            node.Hex,
            node.Kind,
            string.Equals(node.NodeId, _selectedNodeId, StringComparison.Ordinal),
            routeNodeIds.Contains(node.NodeId),
            _region.SigilAnchors.Contains(node.Hex),
            placedSigil == null ? string.Empty : AtlasReadabilityFormatter.FormatSigilName(placedSigil.DisplayName),
            AtlasReadabilityFormatter.BuildTypeBadge(node.Kind),
            AtlasReadabilityFormatter.BuildRewardBadge(node.RewardFamily),
            BuildModifierBadges(stack),
            AtlasReadabilityFormatter.BuildDifficultyBadge(node, stack),
            BuildAuraCategories(stack));
    }

    private static IReadOnlyList<AtlasHexBadgeViewState> BuildModifierBadges(AtlasNodeModifierStack? stack)
    {
        var modifiers = (stack?.ResolvedModifiers ?? Array.Empty<AtlasResolvedModifier>())
            .OrderByDescending(modifier => modifier.Percent)
            .ThenBy(modifier => modifier.Category)
            .Take(2)
            .Select(BuildModifierBadge)
            .ToArray();
        return modifiers.Length == 0
            ? new[] { new AtlasHexBadgeViewState("영향 없음", "이 hex에는 현재 적용된 각인 영향이 없습니다.", "atlas-chip--modifier-neutral") }
            : modifiers;
    }

    private static AtlasHexBadgeViewState BuildModifierBadge(AtlasResolvedModifier modifier)
    {
        var cssClass = modifier.Category switch
        {
            AtlasModifierCategory.RewardBias => "atlas-chip--reward",
            AtlasModifierCategory.ThreatPressure => "atlas-chip--threat",
            AtlasModifierCategory.AffinityBoost => "atlas-chip--affinity",
            _ => "atlas-chip--modifier-neutral",
        };
        var isCapped = modifier.SameCategoryCapped || modifier.HardCapped;
        var cap = isCapped ? " / cap 적용" : string.Empty;
        var sourceNames = string.Join(", ", modifier.Sources
            .Select(source => AtlasReadabilityFormatter.FormatSigilName(source.DisplayName))
            .Distinct(StringComparer.Ordinal));
        return new AtlasHexBadgeViewState(
            AtlasReadabilityFormatter.FormatModifierChipLabel(modifier.Category, modifier.Percent),
            $"{AtlasReadabilityFormatter.FormatModifierCategory(modifier.Category)}: {AtlasReadabilityFormatter.FormatModifierLabel(modifier.Label)} +{modifier.Percent}% ({sourceNames}){cap}",
            isCapped ? $"{cssClass} is-capped" : cssClass);
    }

    private static IReadOnlyList<AtlasModifierCategory> BuildAuraCategories(AtlasNodeModifierStack? stack)
    {
        return (stack?.ResolvedModifiers ?? Array.Empty<AtlasResolvedModifier>())
            .Select(modifier => modifier.Category)
            .Distinct()
            .OrderBy(category => category)
            .ToArray();
    }

    private AtlasSigilPoolItemViewState BuildSigilPoolItem(AtlasSigilDefinition sigil)
    {
        var summary = string.Join(", ", sigil.Modifiers.Select(modifier =>
            $"{AtlasReadabilityFormatter.FormatModifierCategory(modifier.Category)} +{modifier.Percent}%"));
        return new AtlasSigilPoolItemViewState(
            sigil.SigilId,
            AtlasReadabilityFormatter.FormatSigilName(sigil.DisplayName),
            sigil.Radius,
            summary,
            string.Equals(sigil.SigilId, _selectedSigilId, StringComparison.Ordinal),
            _placements.Any(placement => string.Equals(placement.SigilId, sigil.SigilId, StringComparison.Ordinal)));
    }

    private AtlasRouteCandidateViewState BuildRoute(AtlasRouteCandidate route, AtlasSigilResolution resolution)
    {
        var routeStacks = route.NodeIds.Select(id => resolution.FindNode(id)).Where(stack => stack != null).Cast<AtlasNodeModifierStack>().ToArray();
        var reward = routeStacks.Sum(stack => stack.RewardBiasPercent);
        var threat = routeStacks.Sum(stack => stack.ThreatPressurePercent);
        return new AtlasRouteCandidateViewState(
            route.RouteId,
            AtlasReadabilityFormatter.BuildRouteLabel(route, reward, threat),
            AtlasReadabilityFormatter.BuildRouteSummary(route.NodeIds, reward, threat),
            string.Equals(route.RouteId, _selectedRouteId, StringComparison.Ordinal));
    }

    private static AtlasPreviewPanelViewState BuildPreview(
        AtlasNodePreview preview,
        AtlasRegionNode selectedNode,
        AtlasNodeModifierStack selectedStack)
    {
        var modifiers = preview.ModifierStack.Count == 0
            ? "적용된 각인 효과가 없습니다."
            : string.Join("\n", preview.ModifierStack.Select(modifier =>
            {
                var sources = string.Join(", ", modifier.Sources
                    .Select(source => AtlasReadabilityFormatter.FormatSigilName(source.DisplayName))
                    .Distinct(StringComparer.Ordinal));
                var cap = modifier.SameCategoryCapped || modifier.HardCapped ? " (cap 적용)" : string.Empty;
                return $"{AtlasReadabilityFormatter.FormatModifierCategory(modifier.Category)} +{modifier.Percent}% - {sources}{cap}";
            }));
        var recommended = string.Join("\n", preview.RecommendedCharacters.Select(character =>
            $"{AtlasReadabilityFormatter.FormatCharacterName(character.CharacterId, character.DisplayName)} - {AtlasReadabilityFormatter.FormatRole(character.Role)} ({AtlasReadabilityFormatter.FormatRecommendationReason(character.Reason)})"));

        return new AtlasPreviewPanelViewState(
            AtlasReadabilityFormatter.FormatNodeLabel(preview.NodeLabel),
            AtlasReadabilityFormatter.BuildJudgementLine(selectedNode, selectedStack),
            AtlasReadabilityFormatter.FormatEnemyPreview(preview.EnemyPreview),
            modifiers,
            AtlasReadabilityFormatter.BuildRewardPreview(selectedNode, selectedStack),
            recommended,
            AtlasReadabilityFormatter.FormatBoundaryNote(),
            $"NodeOverlayHash={preview.NodeOverlayHash[..12]} / BattleContextHash={preview.BattleContextHash[..12]} / input=runId>chapterId>siteId>nodeIndex>encounterId>NodeOverlayHash>squadSnapshotId");
    }

    private string BuildPlacementSummary(AtlasNodeModifierStack? selectedStack)
    {
        return AtlasReadabilityFormatter.BuildPlacementSummary(_placements, _region.SigilPool, selectedStack);
    }
}
