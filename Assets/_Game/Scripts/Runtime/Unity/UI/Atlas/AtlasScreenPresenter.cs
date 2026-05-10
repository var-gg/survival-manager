using System;
using System.Collections.Generic;
using System.Globalization;
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
    private const string TraversalMode = "CampaignFirstClear";

    private readonly AtlasNodePreviewBuilder _previewBuilder = new();
    private readonly List<AtlasPlacedSigil> _placements = new();
    private readonly List<string> _stageCandidatePath = new();
    private readonly AtlasRegionDefinition _region;
    private string _selectedSigilId;
    private string _selectedNodeId;
    private int _siteSpineIndex;
    private bool _bossResolved;

    public AtlasScreenPresenter(AtlasRegionDefinition region)
    {
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _selectedSigilId = _region.SigilPool.FirstOrDefault()?.SigilId ?? string.Empty;
        _selectedNodeId = AtlasSpineProgressionService.CurrentCandidates(_region, _siteSpineIndex, _bossResolved)
                              .FirstOrDefault()?.HexId
                          ?? _region.Nodes.FirstOrDefault()?.NodeId
                          ?? string.Empty;

        SeedDefaultPlacement();
    }

    public int SiteSpineIndex => _siteSpineIndex;
    public bool BossResolved => _bossResolved;

    public void SelectSigil(string sigilId)
    {
        if (_region.SigilPool.Any(sigil => string.Equals(sigil.SigilId, sigilId, StringComparison.Ordinal)))
        {
            _selectedSigilId = sigilId;
        }
    }

    public void SelectNode(string nodeId)
    {
        if (!_region.Nodes.Any(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
        {
            return;
        }

        _selectedNodeId = nodeId;
        var candidate = AtlasSpineProgressionService.FindCandidate(_region, nodeId);
        if (candidate == null || !AtlasSpineProgressionService.CanEnterStoryCandidate(_region, nodeId, _siteSpineIndex, _bossResolved))
        {
            return;
        }

        if (candidate.SiteStageIndex is >= 1 and <= 3 && !_stageCandidatePath.Contains(nodeId, StringComparer.Ordinal))
        {
            _stageCandidatePath.Add(nodeId);
        }

        if (candidate.SiteStageIndex == AtlasSpineProgressionService.BossStageIndex)
        {
            _bossResolved = true;
        }

        _siteSpineIndex = AtlasSpineProgressionService.AdvanceSpineIndex(candidate, _siteSpineIndex);
    }

    public void PlaceSelectedSigil(string nodeId)
    {
        var node = _region.Nodes.FirstOrDefault(candidate => string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
        var slot = _region.SigilAnchorSlots.FirstOrDefault(candidate => string.Equals(candidate.HexId, nodeId, StringComparison.Ordinal));
        if (node == null || slot == null || string.IsNullOrWhiteSpace(_selectedSigilId))
        {
            SelectNode(nodeId);
            return;
        }

        _placements.RemoveAll(placement =>
            string.Equals(placement.SigilId, _selectedSigilId, StringComparison.Ordinal)
            || string.Equals(placement.AnchorId, slot.AnchorId, StringComparison.Ordinal));

        if (_placements.Count >= 2)
        {
            _placements.RemoveAt(0);
        }

        _placements.Add(new AtlasPlacedSigil(_selectedSigilId, node.Hex, slot.AnchorId));
        _selectedNodeId = nodeId;
    }

    public AtlasScreenViewState Build()
    {
        var resolution = SigilPropagationService.Resolve(_region, _placements);
        var selectedNode = _region.Nodes.FirstOrDefault(node => node.NodeId == _selectedNodeId)
                           ?? _region.Nodes.First();
        var selectedStack = resolution.FindNode(selectedNode.NodeId)
                            ?? new AtlasNodeModifierStack(selectedNode.NodeId, selectedNode.Hex, Array.Empty<AtlasSigilInfluence>(), Array.Empty<AtlasResolvedModifier>());
        var selectedCandidate = AtlasSpineProgressionService.FindCandidate(_region, selectedNode.NodeId);
        var stageCandidatePath = AtlasSpineProgressionService.StageCandidatePath(_stageCandidatePath, selectedCandidate);
        var stageCandidatePathHash = AtlasContextHasher.BuildStageCandidatePathHash(stageCandidatePath);
        var sigilSnapshotHash = AtlasContextHasher.BuildSigilSnapshotHash(_region, SiteId, TraversalMode, _placements, CycleSalt);
        var preview = _previewBuilder.Build(
            _region,
            selectedNode,
            selectedStack,
            stageCandidatePathHash,
            RunId,
            ChapterId,
            SiteId,
            EncounterId,
            CycleSalt,
            SquadSnapshotId);

        return new AtlasScreenViewState(
            AtlasReadabilityFormatter.FormatRegionTitle(_region.DisplayName),
            BuildPlacementSummary(resolution.FindNode(selectedNode.NodeId), sigilSnapshotHash),
            _region.Nodes
                .OrderBy(node => node.Hex.R)
                .ThenBy(node => node.Hex.Q)
                .Select(node => BuildTile(node, resolution))
                .ToArray(),
            _region.SigilPool.Select(BuildSigilPoolItem).ToArray(),
            BuildSpineStages(),
            BuildStageCandidates(resolution),
            BuildPreview(preview, selectedNode, selectedStack, stageCandidatePathHash));
    }

    private void SeedDefaultPlacement()
    {
        if (_region.SigilPool.Count < 2 || _region.SigilAnchorSlots.Count < 2)
        {
            return;
        }

        AddDefaultPlacement("sigil_beast_spoils", "anchor_middle_pressure");
        AddDefaultPlacement("sigil_flank_pressure", "anchor_inner_evidence");
    }

    private void AddDefaultPlacement(string sigilId, string anchorId)
    {
        var slot = _region.SigilAnchorSlots.FirstOrDefault(candidate => candidate.AnchorId == anchorId);
        var node = slot == null ? null : _region.Nodes.FirstOrDefault(candidate => candidate.NodeId == slot.HexId);
        if (node != null)
        {
            _placements.Add(new AtlasPlacedSigil(sigilId, node.Hex, anchorId));
        }
    }

    private AtlasHexTileViewState BuildTile(AtlasRegionNode node, AtlasSigilResolution resolution)
    {
        var stack = resolution.FindNode(node.NodeId);
        var placed = _placements.FirstOrDefault(placement => placement.AnchorHex.Equals(node.Hex));
        var placedSigil = string.IsNullOrWhiteSpace(placed?.SigilId)
            ? null
            : _region.SigilPool.FirstOrDefault(sigil => sigil.SigilId == placed.SigilId);
        var stageCandidate = AtlasSpineProgressionService.FindCandidate(_region, node.NodeId);
        var isCurrentStage = stageCandidate != null
                             && stageCandidate.SiteStageIndex == ResolveCurrentStageIndex();
        var canEnter = stageCandidate == null
                       || AtlasSpineProgressionService.CanEnterStoryCandidate(_region, node.NodeId, _siteSpineIndex, _bossResolved);
        var slot = _region.SigilAnchorSlots.FirstOrDefault(anchor => string.Equals(anchor.HexId, node.NodeId, StringComparison.Ordinal));

        return new AtlasHexTileViewState(
            node.NodeId,
            AtlasReadabilityFormatter.FormatNodeLabel(node.Label),
            node.Hex,
            node.Kind,
            string.Equals(node.NodeId, _selectedNodeId, StringComparison.Ordinal),
            isCurrentStage,
            slot != null,
            stageCandidate?.CandidateBadge ?? string.Empty,
            ResolveAnchorHighlightState(slot),
            canEnter,
            AtlasSpineProgressionService.ResolveLockReason(_region, node.NodeId, _siteSpineIndex, _bossResolved),
            placedSigil == null ? string.Empty : AtlasReadabilityFormatter.FormatSigilCardLabel(placedSigil),
            AtlasReadabilityFormatter.BuildTypeBadge(node.Kind),
            AtlasReadabilityFormatter.BuildRewardBadge(node.RewardFamily),
            BuildModifierBadges(stack),
            AtlasReadabilityFormatter.BuildDifficultyBadge(node, stack),
            BuildAuraCategories(stack),
            BuildAuraShapes(stack),
            (stack?.ResolvedModifiers.Select(modifier => modifier.Category).Distinct().Count() ?? 0) > 1);
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
            ? Array.Empty<AtlasHexBadgeViewState>()
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
            $"{AtlasReadabilityFormatter.FormatModifierCategory(modifier.Category)}: {AtlasReadabilityFormatter.FormatModifierLabel(modifier.Label)} +{modifier.Percent.ToString(CultureInfo.InvariantCulture)}% ({sourceNames}){cap}",
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

    private static IReadOnlyList<AtlasFootprintShape> BuildAuraShapes(AtlasNodeModifierStack? stack)
    {
        return (stack?.Influences ?? Array.Empty<AtlasSigilInfluence>())
            .Select(influence => influence.FootprintShape)
            .Distinct()
            .OrderBy(shape => shape)
            .ToArray();
    }

    private AtlasSigilPoolItemViewState BuildSigilPoolItem(AtlasSigilDefinition sigil)
    {
        return new AtlasSigilPoolItemViewState(
            sigil.SigilId,
            AtlasReadabilityFormatter.FormatSigilCardLabel(sigil),
            AtlasReadabilityFormatter.FormatSigilTradeoffSummary(sigil),
            string.Equals(sigil.SigilId, _selectedSigilId, StringComparison.Ordinal),
            _placements.Any(placement => string.Equals(placement.SigilId, sigil.SigilId, StringComparison.Ordinal)));
    }

    private IReadOnlyList<AtlasSpineStageViewState> BuildSpineStages()
    {
        return Enumerable.Range(1, 5)
            .Select(stage => new AtlasSpineStageViewState(
                stage,
                AtlasReadabilityFormatter.FormatSpineStageLabel(stage),
                stage <= _siteSpineIndex || (stage == 5 && _bossResolved),
                stage == ResolveCurrentStageIndex(),
                stage == AtlasSpineProgressionService.BossStageIndex && _siteSpineIndex < 3
                || stage == AtlasSpineProgressionService.ExtractStageIndex && !_bossResolved))
            .ToArray();
    }

    private IReadOnlyList<AtlasStageCandidateViewState> BuildStageCandidates(AtlasSigilResolution resolution)
    {
        var currentStage = ResolveCurrentStageIndex();
        var visibleCandidates = _region.StageCandidates
            .OrderBy(candidate => candidate.SiteStageIndex)
            .ThenBy(candidate => candidate.CandidateBadge, StringComparer.Ordinal)
            .Where(candidate => candidate.SiteStageIndex == currentStage);
        var gateCandidate = _region.StageCandidates
            .OrderBy(candidate => candidate.SiteStageIndex)
            .ThenBy(candidate => candidate.CandidateBadge, StringComparer.Ordinal)
            .FirstOrDefault(candidate => candidate.SiteStageIndex >= AtlasSpineProgressionService.BossStageIndex
                                         && !AtlasSpineProgressionService.CanEnterStoryCandidate(_region, candidate.HexId, _siteSpineIndex, _bossResolved));

        if (gateCandidate != null && gateCandidate.SiteStageIndex != currentStage)
        {
            visibleCandidates = visibleCandidates.Concat(new[] { gateCandidate });
        }

        return visibleCandidates
            .Take(4)
            .Select(candidate =>
            {
                var node = _region.Nodes.First(item => item.NodeId == candidate.HexId);
                var stack = resolution.FindNode(node.NodeId);
                var canEnter = AtlasSpineProgressionService.CanEnterStoryCandidate(_region, node.NodeId, _siteSpineIndex, _bossResolved);
                return new AtlasStageCandidateViewState(
                    node.NodeId,
                    candidate.CandidateBadge,
                    AtlasReadabilityFormatter.FormatNodeLabel(node.Label),
                    AtlasReadabilityFormatter.BuildCandidateSummary(node, stack),
                    candidate.SiteStageIndex == ResolveCurrentStageIndex(),
                    canEnter,
                    string.Equals(node.NodeId, _selectedNodeId, StringComparison.Ordinal),
                    AtlasSpineProgressionService.ResolveLockReason(_region, node.NodeId, _siteSpineIndex, _bossResolved));
            })
            .ToArray();
    }

    private static AtlasPreviewPanelViewState BuildPreview(
        AtlasNodePreview preview,
        AtlasRegionNode selectedNode,
        AtlasNodeModifierStack selectedStack,
        string stageCandidatePathHash)
    {
        var modifiers = preview.ModifierStack.Count == 0
            ? "적용된 각인 효과가 없습니다."
            : string.Join("\n", preview.ModifierStack.Select(modifier =>
            {
                var sources = string.Join(", ", modifier.Sources
                    .Select(source => AtlasReadabilityFormatter.FormatSigilName(source.DisplayName))
                    .Distinct(StringComparer.Ordinal));
                var cap = modifier.SameCategoryCapped || modifier.HardCapped ? " (cap 적용)" : string.Empty;
                return $"{AtlasReadabilityFormatter.FormatModifierCategory(modifier.Category)} +{modifier.Percent.ToString(CultureInfo.InvariantCulture)}% - {sources}{cap}";
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
            $"NodeOverlayHash={preview.NodeOverlayHash[..12]} / BattleContextHash={preview.BattleContextHash[..12]} / stageCandidatePathHash={stageCandidatePathHash[..12]} / input=runId>chapterId>siteId>nodeIndex>encounterId>stageCandidatePathHash>NodeOverlayHash>squadSnapshotId");
    }

    private string BuildPlacementSummary(AtlasNodeModifierStack? selectedStack, string sigilSnapshotHash)
    {
        return $"{AtlasReadabilityFormatter.BuildPlacementSummary(_placements, _region.SigilPool, _region.SigilAnchorSlots, selectedStack)} SigilSnapshotHash={sigilSnapshotHash[..12]}.";
    }

    private int ResolveCurrentStageIndex()
    {
        return _bossResolved ? AtlasSpineProgressionService.ExtractStageIndex : Math.Min(_siteSpineIndex + 1, AtlasSpineProgressionService.BossStageIndex);
    }

    private string ResolveAnchorHighlightState(SigilAnchorSlot? slot)
    {
        if (slot == null)
        {
            return string.Empty;
        }

        var currentCandidates = AtlasSpineProgressionService.CurrentCandidates(_region, _siteSpineIndex, _bossResolved)
            .Select(candidate => candidate.HexId)
            .ToHashSet(StringComparer.Ordinal);
        if (slot.CoveragePreview.Any(currentCandidates.Contains))
        {
            return "active";
        }

        return ResolveStageBandEnd(slot.StageBand) > _siteSpineIndex ? "future" : "inactive";
    }

    private static int ResolveStageBandEnd(string stageBand)
    {
        if (string.IsNullOrWhiteSpace(stageBand))
        {
            return 0;
        }

        var tokens = stageBand.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var end = tokens.LastOrDefault() ?? string.Empty;
        return end switch
        {
            "boss" => AtlasSpineProgressionService.BossStageIndex,
            "extract" => AtlasSpineProgressionService.ExtractStageIndex,
            _ when int.TryParse(end, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stage) => stage,
            _ => 0,
        };
    }
}
