using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>
/// v1 best-effort causal 판정. 같은 seed의 기본 배치와 다른 배치 전체 재실행을 비교해 채널 발동 유무가
/// 달라지고 승패 또는 정규화 전력차가 10% 이상 달라지면 causal로 표시한다. tagged RNG ablation은 아니다.
/// </summary>
public static class FormationCausalEvaluator
{
    public const float MaterialPowerDifference = 0.10f;
    public const string CausalMethodId = "same-seed-full-rerun-placement-ablation-v1";

    public sealed record Result(
        IReadOnlyList<FormationEventLogRecord> EventLogs,
        IReadOnlyList<FormationPolicySummary> PolicySummaries);

    public static Result Evaluate(IReadOnlyList<FormationBattleRecord> records)
    {
        if (records == null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        var valid = records.Where(record => string.IsNullOrWhiteSpace(record.FailureCode)).ToArray();
        var causalByBattleChannel = ResolveCausalEvidence(valid);
        var logs = valid
            .OrderBy(record => record.PolicyId, StringComparer.Ordinal)
            .ThenBy(record => record.PairingId, StringComparer.Ordinal)
            .ThenBy(record => record.PlacementVariantId, StringComparer.Ordinal)
            .ThenBy(record => record.BattleId, StringComparer.Ordinal)
            .SelectMany(record => record.Channels.OrderBy(channel => channel.ChannelId, StringComparer.Ordinal)
                .Select(channel => BuildLog(record, channel, causalByBattleChannel)))
            .ToArray();
        var summaries = BuildPolicySummaries(valid, logs);
        return new Result(logs, summaries);
    }

    private static Dictionary<string, CausalEvidence> ResolveCausalEvidence(
        IReadOnlyList<FormationBattleRecord> records)
    {
        var result = new Dictionary<string, CausalEvidence>(StringComparer.Ordinal);
        foreach (var group in records
                     .Where(record => !string.IsNullOrWhiteSpace(record.PairingId))
                     .GroupBy(record => record.PairingId, StringComparer.Ordinal))
        {
            var baseline = group.Where(record => record.IsDefaultPlacement)
                .OrderBy(record => record.BattleId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (baseline == null)
            {
                continue;
            }

            foreach (var candidate in group.Where(record => !ReferenceEquals(record, baseline)))
            {
                foreach (var channelId in FormationChannelIds.All)
                {
                    var baselineChannel = Channel(baseline, channelId);
                    var candidateChannel = Channel(candidate, channelId);
                    if (baselineChannel.Fired == candidateChannel.Fired)
                    {
                        continue;
                    }

                    var powerDelta = candidate.NormalizedFinalPowerDifference - baseline.NormalizedFinalPowerDifference;
                    var winnerChanged = !string.Equals(candidate.WinnerSide, baseline.WinnerSide, StringComparison.Ordinal);
                    if (!winnerChanged && Math.Abs(powerDelta) < MaterialPowerDifference)
                    {
                        continue;
                    }

                    var eventRecord = candidateChannel.Fired ? candidate : baseline;
                    var eventDelta = candidateChannel.Fired ? powerDelta : -powerDelta;
                    var key = Key(eventRecord.BattleId, channelId);
                    if (!result.TryGetValue(key, out var existing)
                        || Math.Abs(eventDelta) > Math.Abs(existing.OutcomeDelta))
                    {
                        result[key] = new CausalEvidence(
                            eventDelta,
                            winnerChanged
                                ? $"same-seed placement ablation changed winner against {baseline.BattleId}"
                                : $"same-seed placement ablation changed normalized power by {eventDelta:F3}");
                    }
                }
            }
        }

        return result;
    }

    private static FormationEventLogRecord BuildLog(
        FormationBattleRecord battle,
        FormationBattleRecord.ChannelEvidence channel,
        IReadOnlyDictionary<string, CausalEvidence> causal)
    {
        causal.TryGetValue(Key(battle.BattleId, channel.ChannelId), out var evidence);
        var explanation = channel.Explanation;
        if (evidence != null)
        {
            explanation = string.IsNullOrWhiteSpace(explanation)
                ? evidence.Explanation
                : $"{explanation}; {evidence.Explanation}";
        }

        if (string.Equals(battle.CoverageProbeChannelId, channel.ChannelId, StringComparison.Ordinal))
        {
            explanation = string.IsNullOrWhiteSpace(explanation)
                ? "QA controlled opening condition sampled through production combat resolver"
                : $"QA controlled opening condition sampled through production combat resolver; {explanation}";
        }

        return new FormationEventLogRecord
        {
            RunId = battle.RunId,
            BattleId = battle.BattleId,
            PairingId = battle.PairingId,
            PolicyId = battle.PolicyId,
            PlacementVariantId = battle.PlacementVariantId,
            CoverageProbeChannelId = battle.CoverageProbeChannelId,
            Seed = battle.Seed,
            ChannelId = channel.ChannelId,
            Eligible = channel.Eligible,
            Fired = channel.Fired,
            Causal = evidence != null,
            Legible = channel.Legible,
            EventCount = channel.EventCount,
            OutcomeDelta = evidence?.OutcomeDelta ?? 0f,
            Explanation = explanation,
            CausalMethod = evidence == null ? string.Empty : CausalMethodId,
        };
    }

    private static IReadOnlyList<FormationPolicySummary> BuildPolicySummaries(
        IReadOnlyList<FormationBattleRecord> records,
        IReadOnlyList<FormationEventLogRecord> logs)
    {
        var policyChoices = records.Where(record => record.IsPolicyChoice).ToArray();
        return policyChoices.GroupBy(record => record.PolicyId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var battles = group.ToArray();
                var battleIds = battles.Select(record => record.BattleId).ToHashSet(StringComparer.Ordinal);
                var policyLogs = logs.Where(log => battleIds.Contains(log.BattleId)).ToArray();
                var nontrivial = battles.Where(record => !record.Stomp && !record.Timeout).ToArray();
                var nontrivialIds = nontrivial.Select(record => record.BattleId).ToHashSet(StringComparer.Ordinal);
                var anyFormation = nontrivial.Count(record => record.Channels.Any(channel => channel.Fired));
                var causalBattles = policyLogs.Where(log => log.Causal && nontrivialIds.Contains(log.BattleId))
                    .Select(log => log.BattleId)
                    .Distinct(StringComparer.Ordinal)
                    .Count();
                var firedLogs = policyLogs.Where(log => log.Fired).ToArray();
                var channels = FormationChannelIds.All.Select(channelId =>
                {
                    var rows = policyLogs.Where(log => string.Equals(log.ChannelId, channelId, StringComparison.Ordinal)).ToArray();
                    var eligible = rows.Count(row => row.Eligible);
                    var fired = rows.Count(row => row.Fired);
                    var causal = rows.Count(row => row.Causal);
                    var legible = rows.Count(row => row.Legible);
                    return new FormationPolicySummary.ChannelSummary(
                        channelId,
                        eligible,
                        fired,
                        causal,
                        legible,
                        eligible == 0 ? 0d : fired / (double)eligible,
                        eligible == 0 ? 0d : causal / (double)eligible,
                        fired == 0 ? 0d : legible / (double)fired);
                }).ToArray();
                return new FormationPolicySummary(
                    group.Key,
                    battles.Length,
                    nontrivial.Length,
                    anyFormation,
                    causalBattles,
                    nontrivial.Length == 0 ? 0d : anyFormation / (double)nontrivial.Length,
                    nontrivial.Length == 0 ? 0d : causalBattles / (double)nontrivial.Length,
                    firedLogs.Length == 0 ? 0d : firedLogs.Count(log => log.Legible) / (double)firedLogs.Length,
                    channels);
            }).ToArray();
    }

    private static FormationBattleRecord.ChannelEvidence Channel(FormationBattleRecord record, string channelId)
        => record.Channels.Single(channel => string.Equals(channel.ChannelId, channelId, StringComparison.Ordinal));

    private static string Key(string battleId, string channelId) => $"{battleId}|{channelId}";

    private sealed record CausalEvidence(float OutcomeDelta, string Explanation);
}
