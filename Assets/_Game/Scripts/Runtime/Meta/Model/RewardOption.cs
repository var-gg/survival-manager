using SM.Core.Content;

namespace SM.Meta.Model;

public sealed record RewardOption(
    string Id,
    RewardType Type,
    int Amount,
    string SummaryKey);
