using System;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>Stage 1의 roster-order + front/back 배치와 reward index 0을 그대로 보존하는 기준선.</summary>
public sealed class GreedyPolicy : IHeadlessPolicy
{
    public string Id => HeadlessPolicyFactory.GreedyId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var heroes = HeadlessPolicyScoring.GreedyHeroes(observation);
        var placements = HeadlessPolicyScoring.PlaceGreedy(heroes, observation.Anchors);
        return new HeadlessDeploymentDecision(
            placements,
            $"roster-order first {heroes.Count}; melee front and ranged/support back (Stage 1 equivalent)",
            HeadlessPolicyScoring.EvaluateDeployment(observation, heroes, placements),
            HeadlessPolicyEvidence.ForDeployment(observation, usesDecisionSeed: false, usesCampaignContext: false));
    }

    public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        if (observation.RewardOptions.Count == 0)
        {
            return new HeadlessRewardDecision(
                -1,
                "no visible reward options",
                0d,
                HeadlessPolicyEvidence.ForReward(observation, false, false, false));
        }

        var option = observation.RewardOptions.OrderBy(value => value.Index).First();
        return new HeadlessRewardDecision(
            option.Index,
            "choose first visible reward option (Stage 1 equivalent)",
            HeadlessPolicyScoring.RewardScore(observation, option, false, false, false),
            HeadlessPolicyEvidence.ForReward(observation, false, false, false));
    }
}
