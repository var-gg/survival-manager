using System;
using System.Linq;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenView
{
    private readonly VisualElement _root;
    private readonly VisualElement _content;
    private readonly VisualElement _boardPane;
    private readonly VisualElement _board;
    private readonly VisualElement _candidateOverlay;
    private readonly VisualElement _sigilPool;
    private readonly VisualElement _stageCandidateList;
    private readonly VisualElement _spineProgressStrip;
    private readonly Label _regionTitle;
    private readonly Label _placementSummary;
    private readonly Label _previewTitle;
    private readonly Label _judgement;
    private readonly Label _enemy;
    private readonly Label _modifiers;
    private readonly Label _reward;
    private readonly Label _recommendations;
    private readonly Label _boundary;
    private readonly Label _hash;

    public AtlasScreenView(VisualElement root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _content = Require<VisualElement>("atlas-content");
        _boardPane = Require<VisualElement>("atlas-board-pane");
        _board = Require<VisualElement>("atlas-board");
        _candidateOverlay = Require<VisualElement>("atlas-stage-candidate-overlay");
        _sigilPool = Require<VisualElement>("atlas-sigil-pool");
        _stageCandidateList = Require<VisualElement>("atlas-stage-candidate-list");
        _spineProgressStrip = Require<VisualElement>("atlas-spine-progress-strip");
        _regionTitle = Require<Label>("atlas-region-title");
        _placementSummary = Require<Label>("atlas-placement-summary");
        _previewTitle = Require<Label>("atlas-preview-title");
        _judgement = Require<Label>("atlas-preview-judgement");
        _enemy = Require<Label>("atlas-preview-enemy");
        _modifiers = Require<Label>("atlas-preview-modifiers");
        _reward = Require<Label>("atlas-preview-reward");
        _recommendations = Require<Label>("atlas-preview-recommendations");
        _boundary = Require<Label>("atlas-boundary-note");
        _hash = Require<Label>("atlas-debug-hash");

        _boardPane.pickingMode = PickingMode.Position;
        _board.pickingMode = PickingMode.Ignore;
        _board.style.display = DisplayStyle.None;
        _candidateOverlay.pickingMode = PickingMode.Position;
        _root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            _root.EnableInClassList("atlas-narrow", evt.newRect.width <= 720f);
        });
    }

    public event Action<string>? SigilSelected;
    public event Action<string>? AnchorSelected;
    public event Action<string>? StageCandidateSelected;

    public void Render(AtlasScreenViewState state)
    {
        _regionTitle.text = state.RegionTitle;
        _placementSummary.text = state.PlacementSummary;
        RenderBoard(state);
        RenderSigilPool(state);
        RenderSpineProgress(state);
        RenderStageCandidates(state);
        RenderPreview(state.Preview);
        _content.MarkDirtyRepaint();
    }

    private void RenderBoard(AtlasScreenViewState state)
    {
        // The 3D Atlas scene renders hex geometry and aura rings. UITK keeps the center as a transparent spacer so panels float over the world.
        _board.Clear();
        _candidateOverlay.Clear();
        foreach (var tile in state.Tiles)
        {
            RenderHexHitZone(tile);
            RenderStageCandidateBadge(tile);
            RenderAnchorMarker(tile);
            RenderHexChips(tile);
        }
    }

    private void RenderSigilPool(AtlasScreenViewState state)
    {
        _sigilPool.Clear();
        foreach (var sigil in state.SigilPool)
        {
            var button = new Button(() => SigilSelected?.Invoke(sigil.SigilId))
            {
                text = $"{sigil.DisplayName}\n{sigil.CategorySummary}",
                tooltip = sigil.SigilId,
            };
            button.AddToClassList("atlas-sigil-button");
            button.EnableInClassList("is-selected", sigil.IsSelected);
            button.EnableInClassList("is-placed", sigil.IsPlaced);
            _sigilPool.Add(button);
        }
    }

    private void RenderSpineProgress(AtlasScreenViewState state)
    {
        _spineProgressStrip.Clear();
        foreach (var stage in state.SpineStages)
        {
            var label = stage.IsCompleted ? $"✓ {stage.Label}" : stage.Label;
            var element = new Label(label)
            {
                tooltip = $"{stage.StageIndex}단계",
            };
            element.AddToClassList("atlas-spine-stage");
            element.EnableInClassList("is-current", stage.IsCurrent);
            element.EnableInClassList("is-completed", stage.IsCompleted);
            element.EnableInClassList("is-locked", stage.IsLocked);
            _spineProgressStrip.Add(element);
        }
    }

    private void RenderStageCandidates(AtlasScreenViewState state)
    {
        _stageCandidateList.Clear();
        foreach (var candidate in state.StageCandidates.Where(candidate => candidate.IsCurrentStage || candidate.IsSelected || !candidate.CanEnter))
        {
            var button = new Button(() => StageCandidateSelected?.Invoke(candidate.HexId))
            {
                text = $"{candidate.Badge} {candidate.Label}\n{candidate.Summary}",
                tooltip = string.IsNullOrWhiteSpace(candidate.LockReason) ? candidate.HexId : candidate.LockReason,
            };
            button.AddToClassList("atlas-stage-candidate-button");
            button.EnableInClassList("is-selected", candidate.IsSelected);
            button.EnableInClassList("is-locked", !candidate.CanEnter);
            _stageCandidateList.Add(button);
        }
    }

    private void RenderPreview(AtlasPreviewPanelViewState preview)
    {
        _previewTitle.text = preview.Title;
        _judgement.text = preview.JudgementLine;
        _enemy.text = preview.EnemyPreview;
        _modifiers.text = preview.ModifierStack;
        _reward.text = preview.RewardPreview;
        _recommendations.text = preview.RecommendedCharacters;
        _boundary.text = preview.BoundaryNote;
        _hash.text = preview.DebugHashLine;
    }

    private void RenderStageCandidateBadge(AtlasHexTileViewState tile)
    {
        if (string.IsNullOrWhiteSpace(tile.StageCandidateBadge))
        {
            return;
        }

        var badge = new Button(() => StageCandidateSelected?.Invoke(tile.NodeId))
        {
            text = tile.StageCandidateBadge,
            tooltip = string.IsNullOrWhiteSpace(tile.LockReason) ? tile.Label : tile.LockReason,
        };
        badge.AddToClassList("atlas-stage-badge");
        badge.EnableInClassList("is-current", tile.IsCurrentStageCandidate);
        badge.EnableInClassList("is-locked", !tile.CanEnter);
        AtlasHexOverlayBinder.ApplyBadgeLayout(badge, tile);
        _candidateOverlay.Add(badge);
    }

    private void RenderHexHitZone(AtlasHexTileViewState tile)
    {
        var hitZone = new Button(() =>
        {
            if (tile.IsSigilAnchor)
            {
                AnchorSelected?.Invoke(tile.NodeId);
                return;
            }

            StageCandidateSelected?.Invoke(tile.NodeId);
        })
        {
            tooltip = string.IsNullOrWhiteSpace(tile.LockReason) ? tile.Label : tile.LockReason,
        };
        hitZone.AddToClassList("atlas-hex-hit-zone");
        hitZone.EnableInClassList("is-current", tile.IsCurrentStageCandidate);
        hitZone.EnableInClassList("is-selected", tile.IsSelected);
        hitZone.EnableInClassList("is-locked", !tile.CanEnter);
        hitZone.EnableInClassList("is-anchor", tile.IsSigilAnchor);
        AtlasHexOverlayBinder.ApplyHitZoneLayout(hitZone, tile);
        _candidateOverlay.Add(hitZone);
    }

    private void RenderAnchorMarker(AtlasHexTileViewState tile)
    {
        if (!tile.IsSigilAnchor || tile.AnchorHighlightState == "hidden")
        {
            return;
        }

        var marker = new Button(() => AnchorSelected?.Invoke(tile.NodeId))
        {
            tooltip = tile.PlacedSigilName,
        };
        marker.AddToClassList("atlas-anchor-marker");
        marker.EnableInClassList("is-future", tile.AnchorHighlightState == "future");
        AtlasHexOverlayBinder.ApplyAnchorLayout(marker, tile);
        _candidateOverlay.Add(marker);
    }

    private void RenderHexChips(AtlasHexTileViewState tile)
    {
        if (tile.ModifierChips.Count == 0)
        {
            return;
        }

        var row = new VisualElement();
        row.AddToClassList("atlas-hex-chip-overlay");
        foreach (var chip in tile.ModifierChips.Take(2))
        {
            AddBadge(row, chip);
        }

        row.pickingMode = PickingMode.Ignore;
        AtlasHexOverlayBinder.ApplyChipLayout(row, tile);
        _candidateOverlay.Add(row);
    }

    private T Require<T>(string name) where T : VisualElement
    {
        var element = _root.Q<T>(name);
        if (element == null)
        {
            throw new InvalidOperationException($"AtlasScreen.uxml is missing '{name}'.");
        }

        return element;
    }

    private static void AddBadge(VisualElement row, AtlasHexBadgeViewState badge)
    {
        if (string.IsNullOrWhiteSpace(badge.Label))
        {
            return;
        }

        var element = new Label(badge.Label) { tooltip = badge.Tooltip };
        element.AddToClassList("atlas-chip");
        foreach (var className in badge.CssClass.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            element.AddToClassList(className);
        }

        row.Add(element);
    }

}
