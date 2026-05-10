using System.Collections.Generic;
using SM.Atlas.Model;

namespace SM.Unity.UI.Atlas;

public sealed record AtlasHexBadgeViewState(
    string Label,
    string Tooltip,
    string CssClass);

public enum AtlasStageBadgeVisibility
{
    Resolved = 0,
    Faded = 1,
    Highlighted = 2,
}

public sealed record AtlasHexTileViewState(
    string NodeId,
    string Label,
    AtlasHexCoordinate Hex,
    AtlasNodeKind Kind,
    bool IsSelected,
    bool IsCurrentStageCandidate,
    bool IsSigilAnchor,
    string StageCandidateBadge,
    AtlasAnchorVisibilityState AnchorVisibilityState,
    AtlasStageBadgeVisibility StageBadgeVisibility,
    bool CanEnter,
    string LockReason,
    string PlacedSigilName,
    AtlasHexBadgeViewState TypeChip,
    AtlasHexBadgeViewState RewardFamilyChip,
    IReadOnlyList<AtlasHexBadgeViewState> ModifierChips,
    AtlasHexBadgeViewState DifficultyChip,
    IReadOnlyList<AtlasModifierCategory> AuraCategories,
    IReadOnlyList<AtlasFootprintShape> AuraShapes,
    bool HasOverlapPulse);

public sealed record AtlasSigilPoolItemViewState(
    string SigilId,
    string DisplayName,
    string CategorySummary,
    bool IsSelected,
    bool IsPlaced);

public sealed record AtlasSpineStageViewState(
    int StageIndex,
    string Label,
    bool IsCompleted,
    bool IsCurrent,
    bool IsLocked);

public sealed record AtlasStageCandidateViewState(
    string HexId,
    string Badge,
    string Label,
    string Summary,
    bool IsCurrentStage,
    bool CanEnter,
    bool IsSelected,
    string LockReason);

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
    IReadOnlyList<AtlasSpineStageViewState> SpineStages,
    IReadOnlyList<AtlasStageCandidateViewState> StageCandidates,
    AtlasPreviewPanelViewState Preview);
