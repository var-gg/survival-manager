namespace SM.Core;

public sealed record StoryPresentationContextRecord(
    BattleSummaryRecord? BattleSummary,
    RewardSummaryRecord? RewardSummary)
{
    public static readonly StoryPresentationContextRecord Empty = new(null, null);
}
