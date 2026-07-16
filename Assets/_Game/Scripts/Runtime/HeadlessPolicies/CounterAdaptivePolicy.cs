using System;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>현재 공개된 enemy class/anchor preview에만 대응하고 encounter 내부 stat은 보지 않는다.</summary>
public sealed class CounterAdaptivePolicy : IHeadlessPolicy
{
    public string Id => HeadlessPolicyFactory.CounterAdaptiveId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var heroes = HeadlessPolicyScoring.SelectBestCombination(
            observation,
            candidate => (HeadlessPolicyScoring.CounterSelectionScore(observation.EnemyPreview, candidate) * 2d)
                         + candidate.Sum(HeadlessPolicyScoring.ReadinessScore));
        var placements = HeadlessPolicyScoring.PlaceFormation(heroes, observation.Anchors);
        var previewLabel = observation.EnemyPreview.IsAvailable
            ? $"encounter={observation.EnemyPreview.EncounterId} threats={observation.EnemyPreview.ThreatSkulls}"
            : "preview unavailable; formation fallback";
        return new HeadlessDeploymentDecision(
            placements,
            $"adapt to current player-visible enemy preview ({previewLabel}); roster={HeadlessPolicyScoring.HeroSignature(heroes)}",
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
                Score = HeadlessPolicyScoring.RewardScore(observation, value, false, false, true),
            })
            .OrderByDescending(value => value.Score)
            .ThenBy(value => value.Option.Index)
            .First();
        return new HeadlessRewardDecision(
            option.Option.Index,
            $"reward favors visible counter tools; payload={option.Option.PayloadId}",
            option.Score,
            HeadlessPolicyEvidence.ForReward(observation, false, false, false));
    }
}
