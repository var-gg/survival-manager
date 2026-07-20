using System;
using System.Collections.Generic;
using SM.Core.Content;

namespace SM.Content.Definitions;

[Serializable]
public sealed class SiteGraphNodeDefinition
{
    public string NodeId = string.Empty;
    public int Rank;
    public SiteNodeKindValue Kind = SiteNodeKindValue.Unknown;
    public string LaneTag = string.Empty;
    public string EncounterId = string.Empty;
    public string EventId = string.Empty;
    public List<string> NextNodeIds = new();
    public int EchoIncome;
    public string RewardSourceId = string.Empty;
}
