using SM.Core;

namespace SM.Meta;

public sealed record StoryMomentContext
{
    public static StoryMomentContext Empty { get; } = new();

    public string ChapterId { get; init; } = string.Empty;
    public string SiteId { get; init; } = string.Empty;
    public int NodeIndex { get; init; } = -1;
    public BattleSummaryRecord? BattleSummary { get; init; }
    public RewardSummaryRecord? RewardSummary { get; init; }
}
