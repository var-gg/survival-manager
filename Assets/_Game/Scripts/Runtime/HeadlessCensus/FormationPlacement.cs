using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.HeadlessCensus;

/// <summary>Tank/Damage/Ranged/Healer role slot을 여섯 anchor에 배정한 P(6,4) 원소.</summary>
public sealed record FormationPlacement(
    int PlacementIndex,
    string Signature,
    IReadOnlyList<DeploymentAnchorId> AnchorsByMemberIndex,
    FormationFeatures Features);
