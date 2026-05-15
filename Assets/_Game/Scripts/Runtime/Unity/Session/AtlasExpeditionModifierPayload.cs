using System.Collections.Generic;
using SM.Atlas.Model;

namespace SM.Unity;

public sealed record AtlasExpeditionModifierPayload(
    string RegionId,
    string AtlasNodeId,
    int SiteNodeIndex,
    string ExpeditionNodeId,
    string StageCandidatePathHash,
    string NodeOverlayHash,
    string BattleContextHash,
    int RewardBiasPercent,
    int ThreatPressurePercent,
    int AffinityBoostPercent,
    IReadOnlyList<AtlasResolvedModifier> ResolvedModifiers)
{
    public bool HasAnyModifier => RewardBiasPercent > 0 || ThreatPressurePercent > 0 || AffinityBoostPercent > 0;
}
