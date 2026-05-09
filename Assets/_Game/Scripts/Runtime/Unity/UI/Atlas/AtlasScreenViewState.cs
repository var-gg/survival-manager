using System.Collections.Generic;
using SM.Atlas.Model;

namespace SM.Unity.UI.Atlas;

public sealed record AtlasModifierChipViewState(
    string Label,
    AtlasModifierCategory Category,
    int Percent,
    bool IsCapped,
    string Tooltip);

public sealed record AtlasHexTileViewState(
    string NodeId,
    string Label,
    AtlasHexCoordinate Hex,
    AtlasNodeKind Kind,
    bool IsSelected,
    bool IsRouteNode,
    bool IsSigilAnchor,
    string PlacedSigilName,
    IReadOnlyList<AtlasModifierChipViewState> Chips);

public sealed record AtlasSigilPoolItemViewState(
    string SigilId,
    string DisplayName,
    int Radius,
    string CategorySummary,
    bool IsSelected,
    bool IsPlaced);

public sealed record AtlasRouteCandidateViewState(
    string RouteId,
    string Label,
    string Summary,
    bool IsSelected);

public sealed record AtlasPreviewPanelViewState(
    string Title,
    string JudgementLine,
    string EnemyPreview,
    string ModifierStack,
    string RewardPreview,
    string RecommendedCharacters,
    string BoundaryNote,
    string DebugHashLine);

public sealed record AtlasScreenViewState(
    string RegionTitle,
    string PlacementSummary,
    IReadOnlyList<AtlasHexTileViewState> Tiles,
    IReadOnlyList<AtlasSigilPoolItemViewState> SigilPool,
    IReadOnlyList<AtlasRouteCandidateViewState> Routes,
    AtlasPreviewPanelViewState Preview);
