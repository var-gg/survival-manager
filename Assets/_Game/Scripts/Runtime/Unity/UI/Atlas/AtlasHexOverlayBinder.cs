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
            _ => "atlas-hex--normal",
        };
    }
}
