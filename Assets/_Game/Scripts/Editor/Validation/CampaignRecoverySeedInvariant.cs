using System;
using System.Linq;

namespace SM.Editor.Validation;

/// <summary>
/// Recovery 측정이 attempt 또는 refit을 새 전투 RNG 티켓으로 바꾸지 않는지 검증한다.
/// 같은 campaign cell과 terminal encounter는 두 arm 및 모든 attempt에서 같은 seed여야 한다.
/// </summary>
internal static class CampaignRecoverySeedInvariant
{
    internal static void RequireStable(
        CampaignRecoveryPairObservation pair,
        string targetNodeId)
    {
        var observations = pair.ArmA.Attempts
            .Concat(pair.ArmB.Attempts)
            .GroupBy(attempt => attempt.TerminalNodeId, StringComparer.Ordinal);
        foreach (var encounter in observations)
        {
            var seeds = encounter
                .Select(attempt => attempt.TerminalBattleSeed)
                .Distinct()
                .ToArray();
            if (seeds.Length > 1)
            {
                throw new InvalidOperationException(
                    $"recovery encounter seed drift: target={targetNodeId} "
                    + $"cell={pair.CellId} encounter={encounter.Key} "
                    + $"seeds=[{string.Join(",", seeds)}]");
            }
        }

        var controlOutcomes = pair.ArmB.Attempts
            .Select(attempt => (
                attempt.TargetReached,
                attempt.TargetWon,
                attempt.TerminalNodeId,
                attempt.TerminalBattleSeed,
                attempt.RunEntryPower,
                attempt.TargetPower))
            .Distinct()
            .ToArray();
        if (controlOutcomes.Length > 1)
        {
            throw new InvalidOperationException(
                $"recovery control drifted from captured arrival: target={targetNodeId} "
                + $"cell={pair.CellId} variants={controlOutcomes.Length}");
        }
    }
}
