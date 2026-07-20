using System.Collections.Generic;
using SM.Core.Content;

namespace SM.Unity;

public enum ExpeditionNodeEffectKind
{
    None = 0,
    Gold = 1,
    Echo = 2,
    TraitRerollCurrency = Echo,
    TemporaryAugment = 3,
    PermanentAugmentSlot = 4
}

public sealed record ExpeditionNodeViewModel(
    int Index,
    string Id,
    string LabelKey,
    string PlannedRewardKey,
    string DescriptionKey,
    bool RequiresBattle,
    ExpeditionNodeEffectKind EffectKind,
    int EffectAmount,
    string EffectPayloadId,
    IReadOnlyList<int> NextNodeIndices,
    string NodeId = "",
    SiteNodeKindValue NodeKind = SiteNodeKindValue.Unknown,
    string EventId = "",
    string AuthoredRewardSourceId = "")
{
    public string RewardSourceId => !string.IsNullOrWhiteSpace(AuthoredRewardSourceId)
        ? AuthoredRewardSourceId
        : RequiresBattle ? EffectPayloadId : string.Empty;
    public string GraphNodeId => string.IsNullOrWhiteSpace(NodeId) ? Id : NodeId;
}
