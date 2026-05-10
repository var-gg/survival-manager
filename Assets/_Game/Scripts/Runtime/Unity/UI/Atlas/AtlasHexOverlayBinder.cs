using SM.Atlas.Model;
using UnityEngine.UIElements;

namespace SM.Unity.UI.Atlas;

public static class AtlasHexOverlayBinder
{
    private const float TileWidth = 86f;
    private const float TileHeight = 72f;
    private const float OriginX = 246f;
    private const float OriginY = 178f;

    public static void ApplyTileLayout(VisualElement tile, AtlasHexTileViewState state)
    {
        var x = OriginX + (state.Hex.Q * TileWidth) + (state.Hex.R * TileWidth * 0.5f);
        var y = OriginY + (state.Hex.R * TileHeight);
        tile.style.left = x;
        tile.style.top = y;
        tile.style.width = TileWidth;
        tile.style.height = TileHeight;
        tile.tooltip = $"{state.Label} ({state.Hex})";
    }

    public static void ApplyBadgeLayout(VisualElement badge, AtlasHexTileViewState state)
    {
        var x = OriginX + (state.Hex.Q * TileWidth) + (state.Hex.R * TileWidth * 0.5f);
        var y = OriginY + (state.Hex.R * TileHeight);
        badge.style.left = x + 20f;
        badge.style.top = y + 8f;
    }

    public static void ApplyHitZoneLayout(VisualElement hitZone, AtlasHexTileViewState state)
    {
        var x = OriginX + (state.Hex.Q * TileWidth) + (state.Hex.R * TileWidth * 0.5f);
        var y = OriginY + (state.Hex.R * TileHeight);
        hitZone.style.left = x;
        hitZone.style.top = y;
        hitZone.style.width = TileWidth;
        hitZone.style.height = TileHeight;
        hitZone.tooltip = $"{state.Label} ({state.Hex})";
    }

    public static void ApplyChipLayout(VisualElement row, AtlasHexTileViewState state)
    {
        var x = OriginX + (state.Hex.Q * TileWidth) + (state.Hex.R * TileWidth * 0.5f);
        var y = OriginY + (state.Hex.R * TileHeight);
        row.style.left = x + 8f;
        row.style.top = y + 38f;
    }

    public static void ApplyAnchorLayout(VisualElement marker, AtlasHexTileViewState state)
    {
        var x = OriginX + (state.Hex.Q * TileWidth) + (state.Hex.R * TileWidth * 0.5f);
        var y = OriginY + (state.Hex.R * TileHeight);
        marker.style.left = x + 32f;
        marker.style.top = y + 24f;
    }

    public static void ApplyLayerFocusLayout(VisualElement layer, int stageIndex)
    {
        var normalizedStage = stageIndex switch
        {
            <= 1 => 1,
            >= 5 => 5,
            _ => stageIndex,
        };
        var inset = normalizedStage switch
        {
            1 => 0f,
            2 => 42f,
            3 => 84f,
            _ => 120f,
        };

        layer.style.left = OriginX - 104f + inset;
        layer.style.top = OriginY - 92f + inset * 0.58f;
        layer.style.width = 438f - inset * 2f;
        layer.style.height = 358f - inset * 1.16f;
    }

    public static string ToKindClass(AtlasNodeKind kind)
    {
        return kind switch
        {
            AtlasNodeKind.Skirmish => "atlas-hex--skirmish",
            AtlasNodeKind.Elite => "atlas-hex--elite",
            AtlasNodeKind.Boss => "atlas-hex--boss",
            AtlasNodeKind.Extract => "atlas-hex--extract",
            AtlasNodeKind.Reward => "atlas-hex--reward",
            AtlasNodeKind.Event => "atlas-hex--event",
            AtlasNodeKind.SigilAnchor => "atlas-hex--anchor",
            AtlasNodeKind.Cache => "atlas-hex--reward",
            AtlasNodeKind.ScoutVantage => "atlas-hex--event",
            AtlasNodeKind.Echo => "atlas-hex--event",
            _ => "atlas-hex--normal",
        };
    }
}
