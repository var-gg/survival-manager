using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SM.Atlas.Model;
using SM.Atlas.Services;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenPresenter
{
    private readonly AtlasNodePreviewBuilder _previewBuilder = new();
    private readonly AtlasRegionDefinition _region;
    private AtlasSessionState _session;

    public AtlasScreenPresenter(AtlasRegionDefinition region, AtlasTraversalMode traversalMode = AtlasTraversalMode.StoryFirstClear)
    {
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _session = AtlasSessionService.CreateInitial(
            _region,
            AtlasSessionIdentity.GrayboxDefault,
            traversalMode,
            AtlasGrayboxDataFactory.CreateDefaultPlacements(_region));
    }

    public AtlasScreenPresenter(AtlasRegionDefinition region, AtlasSessionState session)
    {
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public int SiteSpineIndex => _session.SiteSpineIndex;
    public bool BossResolved => _session.BossResolved;
    public AtlasSessionState Session => _session;

    public void SetSession(AtlasSessionState session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void SelectSigil(string sigilId)
    {
        _session = AtlasSessionService.SelectSigil(_region, _session, sigilId);
    }

    public void SelectNode(string nodeId)
    {
        _session = AtlasSessionService.SelectNode(_region, _session, nodeId);
    }

    public void PlaceSelectedSigil(string nodeId)
    {
        _session = AtlasSessionService.PlaceSelectedSigil(_region, _session, nodeId);
    }

    public AtlasScreenViewState Build()
    {
        var sessionResolution = AtlasSessionService.Resolve(_region, _session);
        var preview = _previewBuilder.Build(
            _region,
            sessionResolution.SelectedNode,
            sessionResolution.SelectedStack,
            sessionResolution.StageCandidatePathHash,
            sessionResolution.State.Identity.RunId,
            sessionResolution.State.Identity.ChapterId,
            sessionResolution.State.Identity.SiteId,
            sessionResolution.State.Identity.EncounterId,
            sessionResolution.State.Identity.CycleSalt,
            sessionResolution.State.Identity.SquadSnapshotId);

        return new AtlasScreenViewState(
            AtlasReadabilityFormatter.FormatRegionTitle(_region.DisplayName),
            BuildPlacementSummary(sessionResolution.SelectedStack, sessionResolution.SigilSnapshotHash, sessionResolution.ActiveSigilCap),
            _region.Nodes
                .OrderBy(node => node.Hex.R)
                .ThenBy(node => node.Hex.Q)
                .Select(node => BuildTile(node, sessionResolution))
                .ToArray(),
            _region.SigilPool.Select(BuildSigilPoolItem).ToArray(),
            BuildSpineStages(sessionResolution),
            BuildStageCandidates(sessionResolution),
            BuildPreview(preview, sessionResolution.SelectedNode, sessionResolution.SelectedStack, sessionResolution.StageCandidatePathHash));
    }

    private AtlasHexTileViewState BuildTile(AtlasRegionNode node, AtlasSessionResolution sessionResolution)
    {
        var stack = sessionResolution.ModifierResolution.FindNode(node.NodeId);
        var placed = _session.Placements.FirstOrDefault(placement => placement.AnchorHex.Equals(node.Hex));
        var placedSigil = string.IsNullOrWhiteSpace(placed?.SigilId)
            ? null
            : _region.SigilPool.FirstOrDefault(sigil => sigil.SigilId == placed.SigilId);
        var stageCandidate = AtlasSpineProgressionService.FindCandidate(_region, node.NodeId);
        var isCurrentStage = stageCandidate != null
                             && stageCandidate.SiteStageIndex == sessionResolution.CurrentStageIndex;
        var canEnter = stageCandidate == null
                       || AtlasSpineProgressionService.CanEnterStoryCandidate(_region, node.NodeId, _session.SiteSpineIndex, _session.BossResolved);
        var slot = _region.SigilAnchorSlots.FirstOrDefault(anchor => string.Equals(anchor.HexId, node.NodeId, StringComparison.Ordinal));

        return new AtlasHexTileViewState(
            node.NodeId,
            AtlasReadabilityFormatter.FormatNodeLabel(node.Label),
            node.Hex,
            node.Kind,
            string.Equals(node.NodeId, _session.SelectedNodeId, StringComparison.Ordinal),
            isCurrentStage,
            slot != null,
            stageCandidate?.CandidateBadge ?? string.Empty,
            ResolveAnchorHighlightState(slot),
            ResolveStageBadgeVisibility(stageCandidate, sessionResolution.CurrentStageIndex),
            canEnter,
            AtlasSpineProgressionService.ResolveLockReason(_region, node.NodeId, _session.SiteSpineIndex, _session.BossResolved),
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
        var isCapped = modifier.SameCategoryCapped || modifier.HardCapped || modifier.RiskBackedCapped;
        var cap = modifier.RiskBackedCapped
            ? " / 위험 연동 cap 적용"
            : isCapped
                ? " / cap 적용"
                : string.Empty;
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
            string.Equals(sigil.SigilId, _session.SelectedSigilId, StringComparison.Ordinal),
            _session.Placements.Any(placement => string.Equals(placement.SigilId, sigil.SigilId, StringComparison.Ordinal)));
    }

    private IReadOnlyList<AtlasSpineStageViewState> BuildSpineStages(AtlasSessionResolution sessionResolution)
    {
        return Enumerable.Range(1, 5)
            .Select(stage => new AtlasSpineStageViewState(
                stage,
                AtlasReadabilityFormatter.FormatSpineStageLabel(stage),
                stage <= _session.SiteSpineIndex || (stage == 5 && _session.BossResolved),
                stage == sessionResolution.CurrentStageIndex,
                stage == AtlasSpineProgressionService.BossStageIndex && _session.SiteSpineIndex < 3
                || stage == AtlasSpineProgressionService.ExtractStageIndex && !_session.BossResolved))
            .ToArray();
    }

    private IReadOnlyList<AtlasStageCandidateViewState> BuildStageCandidates(AtlasSessionResolution sessionResolution)
    {
        var currentStage = sessionResolution.CurrentStageIndex;
        var visibleCandidates = _region.StageCandidates
            .OrderBy(candidate => candidate.SiteStageIndex)
            .ThenBy(candidate => candidate.CandidateBadge, StringComparer.Ordinal)
            .Where(candidate => candidate.SiteStageIndex == currentStage);
        var gateCandidate = _region.StageCandidates
            .OrderBy(candidate => candidate.SiteStageIndex)
            .ThenBy(candidate => candidate.CandidateBadge, StringComparer.Ordinal)
            .FirstOrDefault(candidate => candidate.SiteStageIndex >= AtlasSpineProgressionService.BossStageIndex
                                         && !AtlasSpineProgressionService.CanEnterStoryCandidate(_region, candidate.HexId, _session.SiteSpineIndex, _session.BossResolved));

        if (gateCandidate != null && gateCandidate.SiteStageIndex != currentStage)
        {
            visibleCandidates = visibleCandidates.Concat(new[] { gateCandidate });
        }

        return visibleCandidates
            .Take(4)
            .Select(candidate =>
            {
                var node = _region.Nodes.First(item => item.NodeId == candidate.HexId);
                var stack = sessionResolution.ModifierResolution.FindNode(node.NodeId);
                var canEnter = AtlasSpineProgressionService.CanEnterStoryCandidate(_region, node.NodeId, _session.SiteSpineIndex, _session.BossResolved);
                return new AtlasStageCandidateViewState(
                    node.NodeId,
                    candidate.CandidateBadge,
                    AtlasReadabilityFormatter.FormatNodeLabel(node.Label),
                    AtlasReadabilityFormatter.BuildCandidateSummary(node, stack),
                    candidate.SiteStageIndex == currentStage,
                    canEnter,
                    string.Equals(node.NodeId, _session.SelectedNodeId, StringComparison.Ordinal),
                    AtlasSpineProgressionService.ResolveLockReason(_region, node.NodeId, _session.SiteSpineIndex, _session.BossResolved));
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
                var cap = modifier.RiskBackedCapped
                    ? " (위험 연동 cap 적용)"
                    : modifier.SameCategoryCapped || modifier.HardCapped
                        ? " (cap 적용)"
                        : string.Empty;
                return $"{AtlasReadabilityFormatter.FormatModifierCategory(modifier.Category)} {AtlasReadabilityFormatter.FormatModifierIntensity(modifier.Category, modifier.Percent)} - {sources}{cap}";
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

    private string BuildPlacementSummary(AtlasNodeModifierStack? selectedStack, string sigilSnapshotHash, int activeSigilCap)
    {
        return $"{AtlasReadabilityFormatter.BuildPlacementSummary(_session.Placements, activeSigilCap, _region.SigilPool, _region.SigilAnchorSlots, selectedStack)} SigilSnapshotHash={sigilSnapshotHash[..12]}.";
    }

    private AtlasAnchorVisibilityState ResolveAnchorHighlightState(SigilAnchorSlot? slot)
    {
        if (slot == null)
        {
            return AtlasAnchorVisibilityState.Inactive;
        }

        var stageRange = ResolveStageBandRange(slot.StageBand);
        var currentStage = _session.SiteSpineIndex + 1;
        if (currentStage >= stageRange.MinStage && currentStage <= stageRange.MaxStage)
        {
            return AtlasAnchorVisibilityState.Active;
        }

        return stageRange.MinStage > _session.SiteSpineIndex
            ? AtlasAnchorVisibilityState.Future
            : AtlasAnchorVisibilityState.Inactive;
    }

    private static AtlasStageBadgeVisibility ResolveStageBadgeVisibility(AtlasStageCandidate? candidate, int currentStage)
    {
        if (candidate == null)
        {
            return AtlasStageBadgeVisibility.Resolved;
        }

        if (candidate.SiteStageIndex == currentStage)
        {
            return AtlasStageBadgeVisibility.Highlighted;
        }

        return candidate.SiteStageIndex > currentStage
            ? AtlasStageBadgeVisibility.Faded
            : AtlasStageBadgeVisibility.Resolved;
    }

    private static (int MinStage, int MaxStage) ResolveStageBandRange(string stageBand)
    {
        if (string.IsNullOrWhiteSpace(stageBand))
        {
            return (0, 0);
        }

        var tokens = stageBand.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var stages = tokens
            .Skip(1)
            .Select(ParseStageToken)
            .Where(stage => stage > 0)
            .ToArray();
        return stages.Length == 0
            ? (0, 0)
            : (stages.Min(), stages.Max());
    }

    private static int ParseStageToken(string token)
    {
        return token switch
        {
            "boss" => AtlasSpineProgressionService.BossStageIndex,
            "extract" => AtlasSpineProgressionService.ExtractStageIndex,
            _ when int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stage) => stage,
            _ => 0,
        };
    }

}
