namespace SM.Meta;

public sealed record StoryMomentContext
{
    public static StoryMomentContext Empty { get; } = new();

    public string ChapterId { get; init; } = string.Empty;
    public string SiteId { get; init; } = string.Empty;
    public int NodeIndex { get; init; } = -1;
    public object? BattleSummary { get; init; }
    public object? RewardSummary { get; init; }
}
