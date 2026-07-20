using System.Collections.Generic;
using SM.Core.Content;

namespace SM.Meta.Model;

public sealed record SiteGraphNodeTemplate(
    string NodeId,
    int Rank,
    SiteNodeKindValue Kind,
    string LaneTag,
    string EncounterId,
    string EventId,
    IReadOnlyList<string> NextNodeIds,
    int EchoIncome,
    string RewardSourceId);
