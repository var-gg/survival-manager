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

public sealed record AtlasRouteCandidate(
    string RouteId,
    string Label,
    IReadOnlyList<string> NodeIds);

public sealed record AtlasRegionDefinition(
    string RegionId,
    string DisplayName,
    IReadOnlyList<AtlasRegionNode> Nodes,
    IReadOnlyList<AtlasHexCoordinate> SigilAnchors,
    IReadOnlyList<AtlasSigilDefinition> SigilPool,
    IReadOnlyList<AtlasCharacterPreview> Roster,
    IReadOnlyList<AtlasRouteCandidate> Routes);
