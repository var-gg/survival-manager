using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>BattleHighlightLedger와 BattleMetricRecord가 공유하는 진형 전과 5채널의 안정 id.</summary>
public static class FormationChannelIds
{
    public const string Flank = "flank";
    public const string Rear = "rear";
    public const string ScreenBlock = "screen_block";
    public const string Save = "save";
    public const string BacklineDiveKill = "backline_dive_kill";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        Flank,
        Rear,
        ScreenBlock,
        Save,
        BacklineDiveKill,
    };

    public static void RequireKnown(string channelId)
    {
        if (!All.Contains(channelId, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(channelId), channelId, "Unknown formation channel.");
        }
    }
}
