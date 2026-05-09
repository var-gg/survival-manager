using System;
using SM.Atlas.Model;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

public sealed class AtlasScreenView
{
    private readonly VisualElement _root;
    private readonly VisualElement _content;
    private readonly VisualElement _board;
    private readonly VisualElement _sigilPool;
    private readonly VisualElement _routeList;
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
        _board = Require<VisualElement>("atlas-board");
        _sigilPool = Require<VisualElement>("atlas-sigil-pool");
        _routeList = Require<VisualElement>("atlas-route-list");
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

        _root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            _root.EnableInClassList("atlas-narrow", evt.newRect.width <= 720f);
        });
    }

    public event Action<string>? NodeSelected;
    public event Action<string>? AnchorSelected;
    public event Action<string>? SigilSelected;
    public event Action<string>? RouteSelected;

    public void Render(AtlasScreenViewState state)
    {
        _regionTitle.text = state.RegionTitle;
        _placementSummary.text = state.PlacementSummary;
        RenderBoard(state);
        RenderSigilPool(state);
        RenderRoutes(state);
        RenderPreview(state.Preview);
        _content.MarkDirtyRepaint();
    }

    private void RenderBoard(AtlasScreenViewState state)
    {
        _board.Clear();
        foreach (var tile in state.Tiles)
        {
            var button = new Button(() =>
            {
                if (tile.IsSigilAnchor)
                {
                    AnchorSelected?.Invoke(tile.NodeId);
                }
                else
                {
                    NodeSelected?.Invoke(tile.NodeId);
                }
            })
            {
                name = $"atlas-hex-{tile.NodeId}",
            };

            button.AddToClassList("atlas-hex");
            button.AddToClassList(AtlasHexOverlayBinder.ToKindClass(tile.Kind));
            button.EnableInClassList("is-selected", tile.IsSelected);
            button.EnableInClassList("is-route", tile.IsRouteNode);
            button.EnableInClassList("is-anchor", tile.IsSigilAnchor);
            button.EnableInClassList("has-sigil", !string.IsNullOrWhiteSpace(tile.PlacedSigilName));
            button.EnableInClassList("has-aura", tile.AuraCategories.Count > 0);
            button.EnableInClassList("has-overlap", tile.AuraCategories.Count > 1);
            AtlasHexOverlayBinder.ApplyTileLayout(button, tile);

            var auraLayer = new VisualElement { name = "atlas-hex-aura" };
            auraLayer.AddToClassList("atlas-hex-aura");
            foreach (var aura in tile.AuraCategories)
            {
                var swatch = new VisualElement();
                swatch.AddToClassList("atlas-aura-swatch");
                swatch.AddToClassList(ToAuraClass(aura));
                auraLayer.Add(swatch);
            }

            button.Add(auraLayer);
            var tokenRow = new VisualElement { name = "atlas-hex-token-row" };
            tokenRow.AddToClassList("atlas-hex-token-row");
            AddBadge(tokenRow, tile.TypeChip);
            AddBadge(tokenRow, tile.RewardFamilyChip);
            AddBadge(tokenRow, tile.DifficultyChip);
            button.Add(tokenRow);

            button.Add(new Label(tile.Label) { name = "atlas-hex-label" });
            if (!string.IsNullOrWhiteSpace(tile.PlacedSigilName))
            {
                button.Add(new Label(tile.PlacedSigilName) { name = "atlas-hex-sigil" });
            }

            var chipRow = new VisualElement { name = "atlas-hex-chip-row" };
            chipRow.AddToClassList("atlas-hex-chip-row");
            foreach (var chip in tile.ModifierChips)
            {
                AddBadge(chipRow, chip);
            }

            button.Add(chipRow);
            _board.Add(button);
        }
    }

    private void RenderSigilPool(AtlasScreenViewState state)
    {
        _sigilPool.Clear();
        foreach (var sigil in state.SigilPool)
        {
            var button = new Button(() => SigilSelected?.Invoke(sigil.SigilId))
            {
                text = $"{sigil.DisplayName}  반경 {sigil.Radius}\n{sigil.CategorySummary}",
                tooltip = sigil.SigilId,
            };
            button.AddToClassList("atlas-sigil-button");
            button.EnableInClassList("is-selected", sigil.IsSelected);
            button.EnableInClassList("is-placed", sigil.IsPlaced);
            _sigilPool.Add(button);
        }
    }

    private void RenderRoutes(AtlasScreenViewState state)
    {
        _routeList.Clear();
        foreach (var route in state.Routes)
        {
            var button = new Button(() => RouteSelected?.Invoke(route.RouteId))
            {
                text = $"{route.Label}\n{route.Summary}",
                tooltip = route.RouteId,
            };
            button.AddToClassList("atlas-route-button");
            button.EnableInClassList("is-selected", route.IsSelected);
            _routeList.Add(button);
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

    private static string ToAuraClass(AtlasModifierCategory category)
    {
        return category switch
        {
            AtlasModifierCategory.RewardBias => "atlas-aura--reward",
            AtlasModifierCategory.ThreatPressure => "atlas-aura--threat",
            AtlasModifierCategory.AffinityBoost => "atlas-aura--affinity",
            _ => "atlas-aura--neutral",
        };
    }
}
