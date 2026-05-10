using System.Collections.Generic;

namespace SM.Atlas.Model;

public enum AtlasNodeKind
{
    Normal = 0,
    Skirmish = 1,
    Elite = 2,
    Boss = 3,
    Extract = 4,
    Reward = 5,
    Event = 6,
    SigilAnchor = 7,
    Cache = 8,
    ScoutVantage = 9,
    Echo = 10,
}

public enum AtlasHexDirection
{
    East = 0,
    NorthEast = 1,
    NorthWest = 2,
    West = 3,
    SouthWest = 4,
    SouthEast = 5,
}

public enum AtlasAnchorVisibilityState
{
    Inactive = 0,
    Future = 1,
    Active = 2,
}

public sealed record AtlasRegionNode(
    string NodeId,
    AtlasHexCoordinate Hex,
    AtlasNodeKind Kind,
    string Label,
    string EnemyPreview,
    string RewardFamily,
    string AnswerLane,
    int SiteNodeIndex = -1);

public sealed record AtlasCharacterPreview(
    string CharacterId,
    string DisplayName,
    string Role,
    string AnswerLane,
    IReadOnlyList<string> Affinities);

public sealed record AtlasStageCandidate(
    int SiteStageIndex,
    string CandidateBadge,
    string HexId,
    IReadOnlyList<string> ConnectedFromStageHexes);

public sealed record SigilAnchorSlot(
    string AnchorId,
    string HexId,
    string LayerId,
    string StageBand,
    string AnchorRole,
    AtlasHexDirection OrientationToCore,
    IReadOnlyList<string> CoveragePreview,
    IReadOnlyList<string> ValidInTraversalModes);

public sealed record AtlasRegionDefinition(
    string RegionId,
    string DisplayName,
    IReadOnlyList<AtlasRegionNode> Nodes,
    IReadOnlyList<AtlasHexCoordinate> SigilAnchors,
    IReadOnlyList<AtlasSigilDefinition> SigilPool,
    IReadOnlyList<AtlasCharacterPreview> Roster,
    IReadOnlyList<AtlasStageCandidate> StageCandidates,
    IReadOnlyList<SigilAnchorSlot> SigilAnchorSlots,
    string AnchorSlotVersion = "anchor_slots_v2_19hex",
    string FootprintProfileVersion = "footprint_profiles_v2_shape_category");
