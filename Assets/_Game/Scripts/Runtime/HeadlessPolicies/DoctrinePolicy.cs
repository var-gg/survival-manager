using System;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>공개 race/class tag로 class@3 또는 race@4 임계와 하위 시너지 밀도를 우선한다.</summary>
public sealed class DoctrinePolicy : IHeadlessPolicy
{
    public string Id => HeadlessPolicyFactory.DoctrineId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var heroes = HeadlessPolicyScoring.SelectBestCombination(
            observation,
            candidate => (HeadlessPolicyScoring.DoctrineScore(candidate) * 3d)
                         + candidate.Sum(HeadlessPolicyScoring.ReadinessScore));
        var placements = HeadlessPolicyScoring.PlaceFormation(heroes, observation.Anchors);
        return new HeadlessDeploymentDecision(
            placements,
            $"maximize visible race/class thresholds; roster={HeadlessPolicyScoring.HeroSignature(heroes)} doctrine={HeadlessPolicyScoring.DoctrineScore(heroes):F1}",
            HeadlessPolicyScoring.EvaluateDeployment(observation, heroes, placements));
    }

    public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        if (observation.RewardOptions.Count == 0)
        {
            return new HeadlessRewardDecision(-1, "no visible reward options", 0d);
        }

        var option = observation.RewardOptions
            .Select(value => new
            {
                Option = value,
                Score = HeadlessPolicyScoring.RewardScore(observation, value, true, false, false),
            })
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Option.Index)
            .First();
        return new HeadlessRewardDecision(
            option.Option.Index,
            $"reward reinforces visible race/class thesis; payload={option.Option.PayloadId}",
            option.Score);
    }
}
