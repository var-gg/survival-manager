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
    bool IsLocked,
    int SiteNodeIndex = 0,
    string StageKind = "",
    string NodeId = "");

public sealed record AtlasStageCandidateViewState(
    string HexId,
    string Badge,
    string Label,
    string Summary,
    bool IsCurrentStage,
    bool CanEnter,
    bool IsSelected,
    string LockReason);

public sealed record AtlasEnemyIntelViewState(
    bool IsVisible,
    string Header,
    string ForecastLine,
    string EnemyNamesLine,
    string ThreatLine,
    string FactionLine,
    string RewardLine,
    string BossOverlayLine)
{
    public static AtlasEnemyIntelViewState Empty { get; } = new(
        false,
        "부분 정보 없음",
        "예상 가능한 적 정보가 아직 없습니다.",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);
}

public sealed record AtlasPreviewPanelViewState(
    string Title,
    string JudgementLine,
    string EnemyPreview,
    string ModifierStack,
    string RewardPreview,
    string RecommendedCharacters,
    string BoundaryNote,
    string DebugHashLine,
    string ThreatBandLabel = "",
    AtlasEnemyIntelViewState? EnemyIntel = null,
    // wave-25 presenter (GPT Pro P0): Guide/Scout slot — squad 4-6 외 1명, preview depth/reward bias/lore unlock에만 영향.
    // decision-atlas-regional-sigil 결정의 별도 layer. default = "" (UXML placeholder text "(정찰원 미선택)"이 fallback으로 표시).
    string ScoutHint = "");

public sealed record AtlasScreenViewState(
    string RegionTitle,
    string PlacementSummary,
    IReadOnlyList<AtlasHexTileViewState> Tiles,
    IReadOnlyList<AtlasSigilPoolItemViewState> SigilPool,
    IReadOnlyList<AtlasSpineStageViewState> SpineStages,
    IReadOnlyList<AtlasStageCandidateViewState> StageCandidates,
    AtlasPreviewPanelViewState Preview);
