using System;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>
/// v1 bounded 1-ply planner. 공개 roster 조합과 legal anchor permutation을 최대 4096개 평가한다.
/// 미래 RNG rollout이나 미공개 node는 평가 함수 입력 자체에 없다.
/// </summary>
public sealed class SearchPlannerPolicy : IHeadlessPolicy
{
    private const int CandidateBudget = 4096;
    private const int CombinationBudget = 12;

    public string Id => HeadlessPolicyFactory.SearchPlannerId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);

        var greedyHeroes = HeadlessPolicyScoring.GreedyHeroes(observation);
        var bestPlacements = HeadlessPolicyScoring.PlaceGreedy(greedyHeroes, observation.Anchors);
        var bestHeroes = greedyHeroes;
        var bestValue = HeadlessPolicyScoring.EvaluateDeployment(observation, bestHeroes, bestPlacements);
        var bestSignature = HeadlessPolicyScoring.PlacementSignature(bestPlacements);
        var evaluated = 1;

        var combinations = HeadlessPolicyScoring.EnumerateCombinations(observation.Roster, observation.DeployCapacity)
            .Select(heroes => new
            {
                Heroes = heroes,
                Prior = HeadlessPolicyScoring.DoctrineScore(heroes)
                        + HeadlessPolicyScoring.FormationSelectionScore(heroes)
                        + HeadlessPolicyScoring.CounterSelectionScore(observation.EnemyPreview, heroes),
                Signature = HeadlessPolicyScoring.HeroSignature(heroes),
            })
            .OrderByDescending(value => value.Prior)
            .ThenBy(value => value.Signature, StringComparer.Ordinal)
            .Take(CombinationBudget)
            .ToArray();

        foreach (var combination in combinations)
        {
            foreach (var placements in HeadlessPolicyScoring.EnumeratePlacements(combination.Heroes, observation.Anchors))
            {
                if (evaluated >= CandidateBudget)
                {
                    break;
                }

                evaluated++;
                var value = HeadlessPolicyScoring.EvaluateDeployment(observation, combination.Heroes, placements);
                var signature = HeadlessPolicyScoring.PlacementSignature(placements);
                if (value > bestValue || (Math.Abs(value - bestValue) < 0.000001d
                                          && string.CompareOrdinal(signature, bestSignature) < 0))
                {
                    bestHeroes = combination.Heroes;
                    bestPlacements = placements;
                    bestValue = value;
                    bestSignature = signature;
                }
            }

            if (evaluated >= CandidateBudget)
            {
                break;
            }
        }

        return new HeadlessDeploymentDecision(
            bestPlacements,
            $"bounded visible-state search depth=1 candidates={evaluated}; roster={HeadlessPolicyScoring.HeroSignature(bestHeroes)}",
            bestValue);
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
                Score = HeadlessPolicyScoring.RewardScore(observation, value, true, true, true),
            })
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Option.Index)
            .First();
        return new HeadlessRewardDecision(
            option.Option.Index,
            $"bounded visible reward evaluation options={observation.RewardOptions.Count}; payload={option.Option.PayloadId}",
            option.Score);
    }
}
