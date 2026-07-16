using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>관측 seed에서 자체 xorshift stream을 재구성해 호출 순서와 무관하게 합법 행동을 고른다.</summary>
public sealed class RandomLegalPolicy : IHeadlessPolicy
{
    public string Id => HeadlessPolicyFactory.RandomLegalId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var rng = CreateRng(observation, "deployment");
        var heroes = observation.Roster.ToList();
        var anchors = observation.Anchors.ToList();
        Shuffle(heroes, ref rng);
        Shuffle(anchors, ref rng);
        var selected = heroes.Take(observation.DeployCapacity).ToArray();
        var placements = selected
            .Select((hero, index) => new HeadlessPlacement(anchors[index], hero.HeroId))
            .ToArray();
        return new HeadlessDeploymentDecision(
            placements,
            $"seeded legal shuffle seed={observation.DecisionSeed}; selected={HeadlessPolicyScoring.HeroSignature(selected)}",
            HeadlessPolicyScoring.EvaluateDeployment(observation, selected, placements),
            HeadlessPolicyEvidence.ForDeployment(observation, usesDecisionSeed: true, usesCampaignContext: true));
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
                HeadlessPolicyEvidence.ForReward(observation, true, true, false));
        }

        var rng = CreateRng(observation, "reward");
        var option = observation.RewardOptions[rng.Next(observation.RewardOptions.Count)];
        return new HeadlessRewardDecision(
            option.Index,
            $"seeded legal reward draw seed={observation.DecisionSeed}",
            HeadlessPolicyScoring.RewardScore(observation, option, false, false, false),
            HeadlessPolicyEvidence.ForReward(observation, true, true, false));
    }

    private static PolicyRng CreateRng(HeadlessPolicyObservation observation, string decisionKind)
        => new(StableHash($"{observation.DecisionSeed}|{observation.ChapterId}|{observation.SiteId}|{decisionKind}"));

    private static void Shuffle<T>(IList<T> values, ref PolicyRng rng)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var other = rng.Next(index + 1);
            (values[index], values[other]) = (values[other], values[index]);
        }
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return hash == 0 ? 0x9e3779b9u : hash;
        }
    }

    private struct PolicyRng
    {
        private uint _state;

        public PolicyRng(uint state)
        {
            _state = state == 0 ? 0x9e3779b9u : state;
        }

        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return (int)(value % (uint)exclusiveMax);
        }
    }
}
