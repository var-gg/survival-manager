using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessMetrics;

/// <summary>BattleMetricRecord의 typed counter와 live predicate tracker를 진형 전과 레코드로 투영한다.</summary>
public static class FormationMetricProjector
{
    public static FormationBattleRecord Project(
        BattleMetricRecord battle,
        FormationEligibilityTracker eligibility,
        string pairingId,
        string placementSetId,
        string placementVariantId,
        bool isDefaultPlacement,
        bool isPolicyChoice,
        bool isHealerComparison,
        string healerComparisonId,
        bool containsHealer,
        bool competentSelectedHealer,
        string coverageProbeChannelId = "")
    {
        if (battle == null)
        {
            throw new ArgumentNullException(nameof(battle));
        }

        if (eligibility == null)
        {
            throw new ArgumentNullException(nameof(eligibility));
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [FormationChannelIds.Flank] = Math.Max(0, battle.FlankStrikeCount - battle.RearStrikeCount),
            [FormationChannelIds.Rear] = Math.Max(0, battle.RearStrikeCount),
            [FormationChannelIds.ScreenBlock] = Math.Max(0, battle.ScreenBlockCount),
            [FormationChannelIds.Save] = Math.Max(0, battle.SaveMomentCount),
            [FormationChannelIds.BacklineDiveKill] = Math.Max(0, battle.BacklineDiveKillCount),
        };
        var channels = FormationChannelIds.All.Select(channelId =>
        {
            var count = counts[channelId];
            var fired = count > 0;
            var explanation = fired ? Explain(channelId, count, battle) : string.Empty;
            return new FormationBattleRecord.ChannelEvidence(
                channelId,
                eligibility.IsEligible(channelId) || fired,
                fired,
                fired && !string.IsNullOrWhiteSpace(explanation),
                count,
                explanation);
        }).ToArray();

        return new FormationBattleRecord
        {
            RunId = battle.RunId,
            BattleId = battle.BattleId,
            PairingId = pairingId ?? string.Empty,
            PlacementSetId = placementSetId ?? string.Empty,
            PlacementVariantId = placementVariantId ?? string.Empty,
            ScenarioId = battle.ScenarioId,
            PolicyId = battle.PolicyId,
            Seed = battle.Seed,
            IsDefaultPlacement = isDefaultPlacement,
            IsPolicyChoice = isPolicyChoice,
            CoverageProbeChannelId = coverageProbeChannelId ?? string.Empty,
            IsHealerComparison = isHealerComparison,
            HealerComparisonId = healerComparisonId ?? string.Empty,
            ContainsHealer = containsHealer,
            CompetentSelectedHealer = competentSelectedHealer,
            AllyFormationId = battle.AllyFormationId,
            WinnerSide = battle.WinnerSide,
            NormalizedFinalPowerDifference = battle.NormalizedFinalPowerDifference,
            Timeout = battle.Timeout,
            Stomp = battle.Stomp,
            FailureCode = battle.FailureCode,
            Channels = channels,
        };
    }

    private static string Explain(string channelId, int count, BattleMetricRecord battle)
        => channelId switch
        {
            FormationChannelIds.Flank => $"typed flank contacts={count}",
            FormationChannelIds.Rear => $"typed rear contacts={count}",
            FormationChannelIds.ScreenBlock => $"typed screen blocks={count} (absorb={battle.ScreenAbsorbCount}, deterrence={battle.ScreenDeterrenceCount})",
            FormationChannelIds.Save => $"typed low-health heal saves={count}",
            FormationChannelIds.BacklineDiveKill => $"typed backline dive kills={count}",
            _ => string.Empty,
        };
}
