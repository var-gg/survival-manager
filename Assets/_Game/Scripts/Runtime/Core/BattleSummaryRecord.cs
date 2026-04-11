namespace SM.Core;

public sealed record BattleSummaryRecord(
    string ChapterId,
    string SiteId,
    string NodeId,
    int NodeIndex,
    bool Victory,
    int StepCount,
    int EventCount,
    string RewardSourceId,
    string RouteLabelKey);
