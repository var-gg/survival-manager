using System;
using System.Linq;
using SM.Atlas.Model;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenView
{
    private readonly VisualElement _root;
    private readonly VisualElement _content;
    private readonly VisualElement _boardPane;
    private readonly VisualElement _board;
    private readonly VisualElement _mapOverlay;
    private readonly VisualElement _layerOverlay;
    private readonly VisualElement _candidateOverlay;
    private readonly VisualElement _sigilPool;
    private readonly VisualElement _stageCandidateList;
    private readonly VisualElement _spineProgressStrip;
    private readonly VisualElement _siteTrackStrip;
    private readonly Label _regionTitle;
    private readonly Label _placementSummary;
    private readonly Label _previewTitle;
    private readonly Label _judgement;
    private readonly Label _actionRibbon;
    private readonly Label _threatBand;
    private readonly Label _enemy;
    private readonly Label _enemyIntel;
    private readonly Label _modifiers;
    private readonly Label _reward;
    private readonly Label _recommendations;
    private readonly Label _scout;
    private readonly Label _boundary;
    private readonly Label _hash;
    private readonly Button _continueButton;
    private int? _lastLayerStageIndex;

    public AtlasScreenView(VisualElement root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _content = Require<VisualElement>("atlas-content");
        _boardPane = Require<VisualElement>("atlas-board-pane");
        _board = Require<VisualElement>("atlas-board");
        _mapOverlay = Require<VisualElement>("atlas-map-overlay");
        _layerOverlay = Require<VisualElement>("atlas-layer-overlay");
        _candidateOverlay = Require<VisualElement>("atlas-stage-candidate-overlay");
        _sigilPool = Require<VisualElement>("atlas-sigil-pool");
        _stageCandidateList = Require<VisualElement>("atlas-stage-candidate-list");
        _spineProgressStrip = Require<VisualElement>("atlas-spine-progress-strip");
        _siteTrackStrip = Require<VisualElement>("atlas-sitetrack-strip");
        _regionTitle = Require<Label>("atlas-region-title");
        _placementSummary = Require<Label>("atlas-placement-summary");
        _previewTitle = Require<Label>("atlas-preview-title");
        _judgement = Require<Label>("atlas-preview-judgement");
        _actionRibbon = Require<Label>("atlas-preview-action-ribbon");
        _threatBand = Require<Label>("atlas-preview-threat-band");
        _enemy = Require<Label>("atlas-preview-enemy");
        _enemyIntel = Require<Label>("atlas-preview-enemy-intel");
        _modifiers = Require<Label>("atlas-preview-modifiers");
        _reward = Require<Label>("atlas-preview-reward");
        _recommendations = Require<Label>("atlas-preview-recommendations");
        // wave-25 presenter: scout slot label binding. UXML default placeholder text "(정찰원 미선택)" 유지.
        _scout = Require<Label>("atlas-preview-scout");
        _boundary = Require<Label>("atlas-boundary-note");
        _hash = Require<Label>("atlas-debug-hash");
        _hash.style.display = DisplayStyle.None;
        _continueButton = Require<Button>("atlas-continue-button");
        _continueButton.clicked += () => ContinueSelected?.Invoke();

        _boardPane.pickingMode = PickingMode.Position;
        _board.pickingMode = PickingMode.Ignore;
        _board.style.display = DisplayStyle.None;
        _mapOverlay.pickingMode = PickingMode.Ignore;
        _layerOverlay.pickingMode = PickingMode.Position;
        _candidateOverlay.pickingMode = PickingMode.Position;
        _root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            _root.EnableInClassList("atlas-narrow", evt.newRect.width <= 720f);
        });
    }

    public event Action<string>? SigilSelected;
    public event Action<string>? AnchorSelected;
    public event Action<string>? StageCandidateSelected;
    public event Action? ContinueSelected;

    public void Render(AtlasScreenViewState state)
    {
        _regionTitle.text = state.RegionTitle;
        _placementSummary.text = state.PlacementSummary;
        RenderBoard(state);
        RenderSigilPool(state);
        RenderSpineProgress(state);
        RenderSiteTrack(state);
        RenderStageCandidates(state);
        RenderPreview(state.Preview);
        _content.MarkDirtyRepaint();
    }

    private void RenderBoard(AtlasScreenViewState state)
    {
        // The 3D Atlas scene renders hex geometry and aura rings. UITK keeps the center as a transparent spacer so panels float over the world.
        _board.Clear();
        _mapOverlay.Clear();
        _layerOverlay.Clear();
        _candidateOverlay.Clear();
        RenderMapSurface(state);
        var currentLayerStage = state.SpineStages.FirstOrDefault(stage => stage.IsCurrent)?.StageIndex;
        var shouldPulseLayer = _lastLayerStageIndex.HasValue
                               && currentLayerStage.HasValue
                               && _lastLayerStageIndex.Value != currentLayerStage.Value;
        RenderLayerFocus(state, shouldPulseLayer);
        _lastLayerStageIndex = currentLayerStage;
        foreach (var tile in state.Tiles)
        {
            RenderHexHitZone(tile);
            RenderStageCandidateBadge(tile);
            RenderAnchorMarker(tile);
            RenderHexChips(tile);
        }
    }

    private void RenderMapSurface(AtlasScreenViewState state)
    {
        var tileByNodeId = state.Tiles.ToDictionary(tile => tile.NodeId, StringComparer.Ordinal);
        var stageTiles = state.SpineStages
            .OrderBy(stage => stage.StageIndex)
            .Select(stage => !string.IsNullOrWhiteSpace(stage.NodeId) && tileByNodeId.TryGetValue(stage.NodeId, out var tile)
                ? tile
                : state.Tiles.ElementAtOrDefault(stage.SiteNodeIndex))
            .Where(tile => tile != null)
            .Cast<AtlasHexTileViewState>()
            .ToArray();
        AddMapStatusStrip(state);
        for (var i = 1; i < stageTiles.Length; i++)
        {
            AddMapRouteSegment(stageTiles[i - 1], stageTiles[i]);
        }

        foreach (var tile in state.Tiles)
        {
            if (!ShouldRenderMapNode(tile))
            {
                continue;
            }

            var node = new Label(ResolveMapNodeIcon(tile.Kind))
            {
                tooltip = string.IsNullOrWhiteSpace(tile.LockReason) ? tile.Label : tile.LockReason,
            };
            node.AddToClassList("atlas-map-node");
            node.AddToClassList(ResolveMapNodeClass(tile.Kind));
            node.EnableInClassList("is-selected", tile.IsSelected);
            node.EnableInClassList("is-current", tile.IsCurrentStageCandidate);
            node.EnableInClassList("is-locked", !tile.CanEnter);
            var (x, y) = AtlasHexOverlayBinder.GetTileCenter(tile);
            node.style.left = x - (tile.IsSelected || tile.IsCurrentStageCandidate ? 21f : 15f);
            node.style.top = y - (tile.IsSelected || tile.IsCurrentStageCandidate ? 21f : 15f);
            _mapOverlay.Add(node);
            AddMapNodeLabel(tile, x, y);
        }
    }

    private void AddMapStatusStrip(AtlasScreenViewState state)
    {
        var currentStage = state.SpineStages.FirstOrDefault(stage => stage.IsCurrent)
                           ?? state.SpineStages.OrderBy(stage => stage.StageIndex).FirstOrDefault();
        var strip = new VisualElement { pickingMode = PickingMode.Ignore };
        strip.AddToClassList("atlas-map-status-strip");
        AddMapStatusChip(strip, "작전", currentStage == null ? "단계 확인 중" : $"{currentStage.StageIndex}/5 {currentStage.Label}", "stage");
        AddMapStatusChip(strip, "선택", CompactMapLine(state.Preview.Title, 18), "selection");
        AddMapStatusChip(strip, "위협", string.IsNullOrWhiteSpace(state.Preview.ThreatBandLabel) ? "안정" : state.Preview.ThreatBandLabel, "threat");
        AddMapStatusChip(strip, "보상", CompactMapLine(state.Preview.RewardPreview, 20), "reward");
        _mapOverlay.Add(strip);
    }

    private static void AddMapStatusChip(VisualElement strip, string key, string value, string tone)
    {
        var chip = new VisualElement { pickingMode = PickingMode.Ignore };
        chip.AddToClassList("atlas-map-status-chip");
        chip.AddToClassList($"atlas-map-status-chip--{tone}");

        var keyLabel = new Label(key) { pickingMode = PickingMode.Ignore };
        keyLabel.AddToClassList("atlas-map-status-chip__key");
        chip.Add(keyLabel);

        var valueLabel = new Label(value) { pickingMode = PickingMode.Ignore };
        valueLabel.AddToClassList("atlas-map-status-chip__value");
        chip.Add(valueLabel);

        strip.Add(chip);
    }

    private void AddMapNodeLabel(AtlasHexTileViewState tile, float x, float y)
    {
        if (!tile.IsSelected && !tile.IsCurrentStageCandidate)
        {
            return;
        }

        var label = new Label(CompactMapLine(tile.Label, 14))
        {
            pickingMode = PickingMode.Ignore,
            tooltip = tile.Label,
        };
        label.AddToClassList("atlas-map-node-label");
        label.EnableInClassList("is-selected", tile.IsSelected);
        label.EnableInClassList("is-current", tile.IsCurrentStageCandidate);
        label.style.left = x - 54f;
        label.style.top = y + 25f;
        _mapOverlay.Add(label);
    }

    private void AddMapRouteSegment(AtlasHexTileViewState from, AtlasHexTileViewState to)
    {
        var (x1, y1) = AtlasHexOverlayBinder.GetTileCenter(from);
        var (x2, y2) = AtlasHexOverlayBinder.GetTileCenter(to);
        var dx = x2 - x1;
        var dy = y2 - y1;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var segment = new VisualElement();
        segment.AddToClassList("atlas-map-route-segment");
        segment.AddToClassList(ResolveRouteClass(dx, dy));
        segment.style.left = Math.Min(x1, x2);
        segment.style.top = Math.Min(y1, y2) + Math.Abs(dy) * 0.5f - 2f;
        segment.style.width = (float)distance;
        segment.pickingMode = PickingMode.Ignore;
        _mapOverlay.Add(segment);
    }

    private static bool ShouldRenderMapNode(AtlasHexTileViewState tile)
    {
        return tile.IsSelected
               || tile.IsCurrentStageCandidate
               || tile.StageBadgeVisibility != AtlasStageBadgeVisibility.Resolved
               || tile.Kind is AtlasNodeKind.Boss or AtlasNodeKind.Extract or AtlasNodeKind.Reward or AtlasNodeKind.SigilAnchor;
    }

    private static string ResolveRouteClass(float dx, float dy)
    {
        if (Math.Abs(dy) < 16f)
        {
            return "atlas-map-route-segment--flat";
        }

        return dx * dy >= 0f ? "atlas-map-route-segment--fall" : "atlas-map-route-segment--rise";
    }

    private static string ResolveMapNodeIcon(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Boss => "☠",
            AtlasNodeKind.Elite => "★",
            AtlasNodeKind.Extract => "⇧",
            AtlasNodeKind.Reward or AtlasNodeKind.Cache => "◆",
            AtlasNodeKind.SigilAnchor => "✦",
            AtlasNodeKind.Event or AtlasNodeKind.ScoutVantage or AtlasNodeKind.Echo => "◈",
            _ => "✚",
        };
    }

    private static string ResolveMapNodeClass(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Skirmish => "atlas-map-node--skirmish",
            AtlasNodeKind.Elite => "atlas-map-node--elite",
            AtlasNodeKind.Boss => "atlas-map-node--boss",
            AtlasNodeKind.Extract => "atlas-map-node--extract",
            AtlasNodeKind.Reward or AtlasNodeKind.Cache => "atlas-map-node--reward",
            AtlasNodeKind.Event or AtlasNodeKind.ScoutVantage or AtlasNodeKind.Echo => "atlas-map-node--event",
            AtlasNodeKind.SigilAnchor => "atlas-map-node--anchor",
            _ => "atlas-map-node--skirmish",
        };
    }

    private static string CompactMapLine(string value, int maxLength)
    {
        var line = (value ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
        if (line.Length <= maxLength)
        {
            return string.IsNullOrWhiteSpace(line) ? "-" : line;
        }

        return $"{line.Substring(0, Math.Max(0, maxLength - 3))}...";
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

    private void RenderSiteTrack(AtlasScreenViewState state)
    {
        _siteTrackStrip.Clear();
        var ordered = state.SpineStages.OrderBy(stage => stage.SiteNodeIndex).ToArray();
        foreach (var stage in ordered)
        {
            var node = new VisualElement
            {
                tooltip = $"{stage.SiteNodeIndex + 1}/{ordered.Length} · {stage.Label}",
            };
            node.AddToClassList("sitetrack-node");
            var kindToken = string.IsNullOrWhiteSpace(stage.StageKind) ? "unknown" : stage.StageKind;
            node.AddToClassList($"sitetrack-node__kind-{kindToken}");
            node.EnableInClassList("sitetrack-node--current", stage.IsCurrent);
            node.EnableInClassList("sitetrack-node--completed", stage.IsCompleted && !stage.IsCurrent);
            node.EnableInClassList("sitetrack-node--locked", stage.IsLocked && !stage.IsCurrent);

            var icon = new Label(ResolveSiteTrackIcon(kindToken, stage.IsCompleted, stage.IsCurrent));
            icon.AddToClassList("sitetrack-node__icon");
            icon.pickingMode = PickingMode.Ignore;
            node.Add(icon);

            var label = new Label(stage.Label);
            label.AddToClassList("sitetrack-node__label");
            label.pickingMode = PickingMode.Ignore;
            node.Add(label);

            _siteTrackStrip.Add(node);
        }
    }

    private static string ResolveSiteTrackIcon(string kindToken, bool isCompleted, bool isCurrent)
    {
        if (isCompleted && !isCurrent)
        {
            return "✓";
        }

        return kindToken switch
        {
            "skirmish" => "⚔",
            "elite" => "★",
            "boss" => "☠",
            "extract" => "⇧",
            _ => "•",
        };
    }

    private void RenderStageCandidates(AtlasScreenViewState state)
    {
        _stageCandidateList.Clear();
        foreach (var candidate in state.StageCandidates)
        {
            var button = new Button(() => StageCandidateSelected?.Invoke(candidate.HexId))
            {
                text = $"{candidate.Badge} {candidate.Label}\n{candidate.Summary}",
                tooltip = string.IsNullOrWhiteSpace(candidate.LockReason) ? candidate.HexId : candidate.LockReason,
            };
            button.AddToClassList("atlas-stage-candidate-button");
            button.EnableInClassList("is-current", candidate.IsCurrentStage);
            button.EnableInClassList("is-selected", candidate.IsSelected);
            button.EnableInClassList("is-enterable", candidate.CanEnter);
            button.EnableInClassList("is-locked", !candidate.CanEnter);
            _stageCandidateList.Add(button);
        }
    }

    private void RenderPreview(AtlasPreviewPanelViewState preview)
    {
        _previewTitle.text = preview.Title;
        _judgement.text = preview.JudgementLine;
        _actionRibbon.text = BuildActionRibbon(preview);
        RenderThreatBand(preview.ThreatBandLabel);
        _enemy.text = preview.EnemyPreview;
        RenderEnemyIntel(preview.EnemyIntel ?? AtlasEnemyIntelViewState.Empty);
        _modifiers.text = preview.ModifierStack;
        _reward.text = preview.RewardPreview;
        _recommendations.text = preview.RecommendedCharacters;
        // wave-25 presenter: scout text — empty면 UXML placeholder "(정찰원 미선택)" 유지.
        if (!string.IsNullOrEmpty(preview.ScoutHint))
        {
            _scout.text = preview.ScoutHint;
        }
        _boundary.text = preview.BoundaryNote;
        _hash.text = string.Empty;
        _hash.style.display = DisplayStyle.None;
    }

    private static string BuildActionRibbon(AtlasPreviewPanelViewState preview)
    {
        var intel = preview.EnemyIntel ?? AtlasEnemyIntelViewState.Empty;
        var intelState = intel.IsVisible ? "적 징후 확인" : "적 징후 제한";
        return $"선택 노드 · {intelState} · 진행 전 예상";
    }

    private void RenderEnemyIntel(AtlasEnemyIntelViewState intel)
    {
        if (!intel.IsVisible)
        {
            _enemyIntel.text = intel.ForecastLine;
            return;
        }

        _enemyIntel.text = string.Join("\n", new[]
        {
            intel.Header,
            intel.ForecastLine,
            intel.EnemyNamesLine,
            intel.ThreatLine,
            intel.FactionLine,
            intel.RewardLine,
            intel.BossOverlayLine,
        }.Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    private void RenderThreatBand(string label)
    {
        // task-atlas-modifier-application-v1 acceptance #5: AtlasModifierApplicationService.ComputeThreatBand
        // 결과를 chip으로 노출. RewardScreen settlement summary와 같은 band 분기를 공유하면서
        // Atlas preview는 base("안정")도 명시해 현재 노드 위협 상태를 항상 한 줄로 읽히도록 한다.
        _threatBand.text = string.IsNullOrEmpty(label) ? string.Empty : $"위협 {label}";
        _threatBand.EnableInClassList("is-elevated", label == "고조");
        _threatBand.EnableInClassList("is-high", label == "과중");
        _threatBand.EnableInClassList("is-severe", label == "극단");
    }

    private void RenderStageCandidateBadge(AtlasHexTileViewState tile)
    {
        if (string.IsNullOrWhiteSpace(tile.StageCandidateBadge))
        {
            return;
        }

        if (tile.StageBadgeVisibility == AtlasStageBadgeVisibility.Resolved)
        {
            return;
        }

        var text = tile.StageBadgeVisibility == AtlasStageBadgeVisibility.Highlighted
            ? tile.StageCandidateBadge
            : tile.CanEnter ? "•" : "🔒";
        var badge = new Button(() => StageCandidateSelected?.Invoke(tile.NodeId))
        {
            text = text,
            tooltip = string.IsNullOrWhiteSpace(tile.LockReason) ? tile.Label : tile.LockReason,
        };
        badge.AddToClassList("atlas-stage-badge");
        badge.EnableInClassList("is-current", tile.StageBadgeVisibility == AtlasStageBadgeVisibility.Highlighted);
        badge.EnableInClassList("is-faded", tile.StageBadgeVisibility == AtlasStageBadgeVisibility.Faded);
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
        hitZone.EnableInClassList("is-stage-candidate", !string.IsNullOrWhiteSpace(tile.StageCandidateBadge));
        hitZone.EnableInClassList("is-stage-candidate-current", tile.StageBadgeVisibility == AtlasStageBadgeVisibility.Highlighted);
        hitZone.EnableInClassList("is-stage-candidate-future", tile.StageBadgeVisibility == AtlasStageBadgeVisibility.Faded);
        hitZone.EnableInClassList("is-pin-anchor", tile.IsSigilAnchor);
        hitZone.EnableInClassList("is-current-selected", tile.IsCurrentStageCandidate && tile.IsSelected);
        AtlasHexOverlayBinder.ApplyHitZoneLayout(hitZone, tile);
        _candidateOverlay.Add(hitZone);
    }

    private void RenderAnchorMarker(AtlasHexTileViewState tile)
    {
        if (!tile.IsSigilAnchor || tile.AnchorVisibilityState == AtlasAnchorVisibilityState.Inactive)
        {
            return;
        }

        var marker = new Button(() => AnchorSelected?.Invoke(tile.NodeId))
        {
            tooltip = tile.PlacedSigilName,
        };
        marker.AddToClassList("atlas-anchor-marker");
        marker.EnableInClassList("is-active", tile.AnchorVisibilityState == AtlasAnchorVisibilityState.Active);
        marker.EnableInClassList("is-future", tile.AnchorVisibilityState == AtlasAnchorVisibilityState.Future);
        AtlasHexOverlayBinder.ApplyAnchorLayout(marker, tile);
        _candidateOverlay.Add(marker);
    }

    private void RenderLayerFocus(AtlasScreenViewState state, bool shouldPulse)
    {
        var currentStage = state.SpineStages.FirstOrDefault(stage => stage.IsCurrent);
        if (currentStage == null)
        {
            return;
        }

        var layer = new VisualElement();
        layer.AddToClassList("atlas-layer-focus");
        layer.AddToClassList($"atlas-layer-focus--stage-{currentStage.StageIndex}");
        layer.EnableInClassList("is-pulsing", shouldPulse);
        layer.style.opacity = shouldPulse ? 0.16f : 0.03f;
        layer.RegisterCallback<MouseEnterEvent>(_ => layer.style.opacity = 0.16f);
        layer.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            if (!layer.ClassListContains("is-pulsing"))
            {
                layer.style.opacity = 0.03f;
            }
        });
        if (shouldPulse)
        {
            layer.schedule.Execute(() =>
            {
                layer.RemoveFromClassList("is-pulsing");
                layer.style.opacity = 0.03f;
            }).StartingIn(3500);
        }

        AtlasHexOverlayBinder.ApplyLayerFocusLayout(layer, currentStage.StageIndex);
        _layerOverlay.Add(layer);
    }

    private void RenderHexChips(AtlasHexTileViewState tile)
    {
        if (tile.ModifierChips.Count == 0)
        {
            return;
        }

        if (!tile.IsSelected
            && !tile.IsCurrentStageCandidate
            && tile.AnchorVisibilityState != AtlasAnchorVisibilityState.Active)
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
