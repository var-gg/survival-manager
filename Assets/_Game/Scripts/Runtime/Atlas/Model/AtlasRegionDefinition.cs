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

public enum AtlasRegionLayer
{
    Outer = 0,
    Middle = 1,
    Inner = 2,
    Core = 3,
}

public sealed record AtlasRegionNode(
    string NodeId,
    AtlasHexCoordinate Hex,
    AtlasNodeKind Kind,
    string Label,
    string EnemyPreview,
    string RewardFamily,
    string AnswerLane,
    int SiteNodeIndex = -1,
    AtlasRegionLayer Layer = AtlasRegionLayer.Outer);

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
    string FootprintProfileVersion = "footprint_profiles_v2_shape_category")
{
    public AtlasRegionLayer ResolveLayer(string nodeId)
    {
        foreach (var node in Nodes)
        {
            if (string.Equals(node.NodeId, nodeId, System.StringComparison.Ordinal))
            {
                return node.Layer;
            }
        }

        throw new System.ArgumentException($"Atlas node '{nodeId}' was not found.", nameof(nodeId));
    }
}
