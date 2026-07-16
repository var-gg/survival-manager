using System;
using System.Collections.Generic;

namespace SM.HeadlessPolicies;

/// <summary>
/// observation에 조립된 fact id index에서 정책이 실제로 사용한 player-visible 신호만 선택한다.
/// fact schema나 ledger 구현은 알지 않고 안정적인 문자열 key/id만 소비한다.
/// </summary>
public static class HeadlessPolicyEvidence
{
    public const string DecisionSeedSignal = "decision.seed";
    public const string CampaignContextSignal = "campaign.context";
    public const string DeploymentSurfaceSignal = "deployment.surface";
    public const string RosterSurfaceSignal = "roster.surface";
    public const string EnemyPreviewSignal = "enemy.preview";
    public const string RewardSurfaceSignal = "reward.surface";

    internal static IReadOnlyList<string> ForDeployment(
        HeadlessPolicyObservation observation,
        bool usesDecisionSeed,
        bool usesCampaignContext)
    {
        var keys = new List<string>
        {
            DeploymentSurfaceSignal,
            RosterSurfaceSignal,
            EnemyPreviewSignal,
        };
        if (usesDecisionSeed)
        {
            keys.Add(DecisionSeedSignal);
        }

        if (usesCampaignContext)
        {
            keys.Add(CampaignContextSignal);
        }

        return Resolve(observation, keys);
    }

    internal static IReadOnlyList<string> ForReward(
        HeadlessPolicyObservation observation,
        bool usesDecisionSeed,
        bool usesCampaignContext,
        bool usesRoster)
    {
        var keys = new List<string> { RewardSurfaceSignal };
        if (usesDecisionSeed)
        {
            keys.Add(DecisionSeedSignal);
        }

        if (usesCampaignContext)
        {
            keys.Add(CampaignContextSignal);
        }

        if (usesRoster)
        {
            keys.Add(RosterSurfaceSignal);
        }

        return Resolve(observation, keys);
    }

    private static IReadOnlyList<string> Resolve(
        HeadlessPolicyObservation observation,
        IReadOnlyList<string> signalKeys)
    {
        var result = new string[signalKeys.Count];
        for (var index = 0; index < signalKeys.Count; index++)
        {
            var key = signalKeys[index];
            if (!observation.EvidenceFactIdsBySignal.TryGetValue(key, out var factId)
                || string.IsNullOrWhiteSpace(factId))
            {
                throw new HeadlessPolicyEvidenceException(
                    $"Player-visible evidence signal '{key}' was not projected before policy execution.");
            }

            result[index] = factId;
        }

        return result;
    }
}

/// <summary>정책 결정이 player-visible evidence 계약을 만족하지 못했을 때 사용하는 fail-closed 예외.</summary>
public sealed class HeadlessPolicyEvidenceException : InvalidOperationException
{
    public HeadlessPolicyEvidenceException(string message)
        : base(message)
    {
    }
}
