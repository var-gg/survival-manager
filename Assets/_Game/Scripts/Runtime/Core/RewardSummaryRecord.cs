namespace SM.Core;

public sealed record RewardSummaryRecord(
    string ChapterId,
    string SiteId,
    string RewardSourceId,
    int ChoiceIndex,
    string ChoiceKind,
    string PayloadId,
    int GoldDelta,
    int EchoDelta,
    bool WasRecoveredSettlement);
