using System;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>front/back 균형, support 보호, class coverage를 공개 role/anchor 정보로 최적화한다.</summary>
public sealed class FormationPolicy : IHeadlessPolicy
{
    public string Id => HeadlessPolicyFactory.FormationId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var heroes = HeadlessPolicyScoring.SelectBestCombination(
            observation,
            candidate => (HeadlessPolicyScoring.FormationSelectionScore(candidate) * 2d)
                         + candidate.Sum(HeadlessPolicyScoring.ReadinessScore));
        var placements = HeadlessPolicyScoring.PlaceFormation(heroes, observation.Anchors);
        return new HeadlessDeploymentDecision(
            placements,
            $"balance front/back and protect visible support; roster={HeadlessPolicyScoring.HeroSignature(heroes)}",
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

        var option = observation.RewardOptions
            .Select(value => new
            {
                Option = value,
                Score = HeadlessPolicyScoring.RewardScore(observation, value, false, true, false),
            })
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Option.Index)
            .First();
        return new HeadlessRewardDecision(
            option.Option.Index,
            $"reward favors protection/healing formation value; payload={option.Option.PayloadId}",
            option.Score,
            HeadlessPolicyEvidence.ForReward(observation, false, false, false));
    }
}
