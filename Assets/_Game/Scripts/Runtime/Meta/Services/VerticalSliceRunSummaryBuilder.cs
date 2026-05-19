using System;
using System.Collections.Generic;
using System.Linq;
using SM.Meta.Model;

namespace SM.Meta.Services;

/// <summary>
/// VerticalSliceRunSummary를 ActiveRun + Overlay + reward ledger snapshot에서 빌드하는 pure builder.
/// SM.Unity가 GameSessionState에서 input record를 모아 호출. SM.Meta는 Unity 의존을 가지지 않는다.
/// task-slice-observability-summary-v1 acceptance #1/#2/#3/#5.
/// </summary>
public sealed record VerticalSliceRunSummaryInput(
    string RunId,
    string ChapterId,
    string SiteId,
    int SiteNodeIndex,
    string EncounterId,
    string FactionId,
    int BattleSeed,
    bool IsBoss,
    bool LastSettlementWasVictory,
    string BattleContextHash,
    string NodeOverlayHash,
    string SelectedRouteHash,
    int RewardBiasPercent,
    int ThreatPressurePercent,
    int AffinityBoostPercent,
    IReadOnlyList<string>? ResolvedModifierIds,
    string RewardSourceId,
    string RewardCommitId,
    int RewardChoiceLedgerCount,
    bool HasPendingRewardSettlement);

public static class VerticalSliceRunSummaryBuilder
{
    /// <summary>
    /// input → summary. null/empty input은 Empty sentinel로 운반. caller는 IsPopulated로 분기.
    /// </summary>
    public static VerticalSliceRunSummary Build(VerticalSliceRunSummaryInput? input)
    {
        if (input == null)
        {
            return VerticalSliceRunSummary.Empty;
        }

        var modifierIds = input.ResolvedModifierIds == null
            ? Array.Empty<string>()
            : (IReadOnlyList<string>)input.ResolvedModifierIds
                .Where(static id => !string.IsNullOrWhiteSpace(id))
                .ToArray();

        var atlas = string.IsNullOrWhiteSpace(input.RunId)
            ? VerticalSliceAtlasSection.Empty
            : new VerticalSliceAtlasSection(
                RunId: input.RunId,
                ChapterId: input.ChapterId ?? string.Empty,
                SiteId: input.SiteId ?? string.Empty,
                SiteNodeIndex: Math.Max(-1, input.SiteNodeIndex),
                SelectedRouteHash: input.SelectedRouteHash ?? string.Empty,
                NodeOverlayHash: input.NodeOverlayHash ?? string.Empty,
                RewardBiasPercent: input.RewardBiasPercent,
                ThreatPressurePercent: input.ThreatPressurePercent,
                AffinityBoostPercent: input.AffinityBoostPercent,
                ResolvedModifierIds: modifierIds);

        var battle = string.IsNullOrWhiteSpace(input.EncounterId)
            ? VerticalSliceBattleSection.Empty
            : new VerticalSliceBattleSection(
                EncounterId: input.EncounterId,
                FactionId: input.FactionId ?? string.Empty,
                BattleContextHash: input.BattleContextHash ?? string.Empty,
                BattleSeed: input.BattleSeed,
                IsBoss: input.IsBoss,
                LastSettlementWasVictory: input.LastSettlementWasVictory);

        var reward = string.IsNullOrWhiteSpace(input.RewardCommitId)
            ? VerticalSliceRewardSection.Empty
            : new VerticalSliceRewardSection(
                RewardSourceId: input.RewardSourceId ?? string.Empty,
                RewardCommitId: input.RewardCommitId,
                RewardChoiceLedgerCount: Math.Max(0, input.RewardChoiceLedgerCount),
                HasPendingRewardSettlement: input.HasPendingRewardSettlement);

        return new VerticalSliceRunSummary(atlas, battle, reward);
    }

    /// <summary>
    /// Markdown report로 직렬화. PlayMode smoke 종료 시 evidence packet의 일부.
    /// task-vertical-slice-smoke-evidence-v1 acceptance #5/#7.
    /// </summary>
    public static string ToMarkdown(VerticalSliceRunSummary summary)
    {
        if (summary == null || !summary.IsPopulated)
        {
            return "# Vertical Slice Run Summary\n\n_no populated section yet._\n";
        }

        var lines = new List<string>
        {
            "# Vertical Slice Run Summary",
            string.Empty,
        };

        if (summary.Atlas.IsPopulated)
        {
            lines.Add("## Atlas");
            lines.Add($"- RunId: `{summary.Atlas.RunId}`");
            lines.Add($"- Chapter / Site / Node: `{summary.Atlas.ChapterId}` / `{summary.Atlas.SiteId}` / {summary.Atlas.SiteNodeIndex}");
            lines.Add($"- NodeOverlayHash: `{summary.Atlas.NodeOverlayHash}`");
            lines.Add($"- Modifier (Reward/Threat/Affinity %): {summary.Atlas.RewardBiasPercent} / {summary.Atlas.ThreatPressurePercent} / {summary.Atlas.AffinityBoostPercent}");
            lines.Add($"- ResolvedModifierIds ({summary.Atlas.ResolvedModifierIds.Count}): " +
                (summary.Atlas.ResolvedModifierIds.Count == 0 ? "_none_" : string.Join(", ", summary.Atlas.ResolvedModifierIds.Select(id => $"`{id}`"))));
            lines.Add(string.Empty);
        }

        if (summary.Battle.IsPopulated)
        {
            lines.Add("## Battle");
            lines.Add($"- EncounterId: `{summary.Battle.EncounterId}`");
            lines.Add($"- FactionId: `{summary.Battle.FactionId}` / IsBoss: {summary.Battle.IsBoss}");
            lines.Add($"- BattleContextHash: `{summary.Battle.BattleContextHash}`");
            lines.Add($"- BattleSeed: {summary.Battle.BattleSeed}");
            lines.Add($"- LastSettlementWasVictory: {summary.Battle.LastSettlementWasVictory}");
            lines.Add(string.Empty);
        }

        if (summary.Reward.IsPopulated)
        {
            lines.Add("## Reward");
            lines.Add($"- RewardSourceId: `{summary.Reward.RewardSourceId}`");
            lines.Add($"- RewardCommitId: `{summary.Reward.RewardCommitId}`");
            lines.Add($"- RewardChoiceLedgerCount: {summary.Reward.RewardChoiceLedgerCount}");
            lines.Add($"- HasPendingRewardSettlement: {summary.Reward.HasPendingRewardSettlement}");
            lines.Add(string.Empty);
        }

        // Cinematic detector 10종은 본 cycle scope 밖 — outcome에 명시.
        lines.Add("> Cinematic detector 10종 (focus·reposition·tempo 등)은 본 cycle scope 밖 — next cycle telemetry 작업에서 도입.");

        return string.Join("\n", lines);
    }
}
