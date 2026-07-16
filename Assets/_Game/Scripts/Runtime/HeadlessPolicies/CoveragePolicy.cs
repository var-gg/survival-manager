using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

/// <summary>
/// 진형 5채널의 발동 가능성을 의도적으로 표본화하는 QA 정책이다. 유능 플레이나 밸런스 가치를
/// 주장하지 않으며, player-visible observation과 legal action만 사용한다.
/// </summary>
public sealed class CoveragePolicy : IHeadlessPolicy
{
    private static readonly string[] ChannelIds =
    {
        "flank",
        "rear",
        "screen_block",
        "save",
        "backline_dive_kill",
    };

    private static readonly DeploymentAnchorId[][] AnchorTemplates =
    {
        new[] { DeploymentAnchorId.FrontCenter, DeploymentAnchorId.FrontTop, DeploymentAnchorId.BackCenter, DeploymentAnchorId.BackBottom },
        new[] { DeploymentAnchorId.FrontCenter, DeploymentAnchorId.FrontBottom, DeploymentAnchorId.BackTop, DeploymentAnchorId.BackCenter },
        new[] { DeploymentAnchorId.FrontCenter, DeploymentAnchorId.FrontBottom, DeploymentAnchorId.BackTop, DeploymentAnchorId.BackCenter },
        new[] { DeploymentAnchorId.FrontTop, DeploymentAnchorId.FrontBottom, DeploymentAnchorId.BackBottom, DeploymentAnchorId.BackCenter },
        new[] { DeploymentAnchorId.FrontCenter, DeploymentAnchorId.FrontTop, DeploymentAnchorId.BackBottom, DeploymentAnchorId.BackTop },
    };

    public string Id => HeadlessPolicyFactory.CoverageId;

    public HeadlessDeploymentDecision DecideDeployment(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        var heroes = HeadlessPolicyScoring.SelectBestCombination(observation, CoverageSelectionScore);
        var channelIndex = (observation.DecisionSeed & int.MaxValue) % ChannelIds.Length;
        var placements = PlaceForChannel(heroes, observation.Anchors, channelIndex);
        return new HeadlessDeploymentDecision(
            placements,
            $"QA coverage only (not competent play); sample={ChannelIds[channelIndex]} healer={heroes.Any(IsHealer)} "
            + $"doctrine={HeadlessPolicyScoring.DoctrineScore(heroes):F1} roster={HeadlessPolicyScoring.HeroSignature(heroes)}",
            HeadlessPolicyScoring.EvaluateDeployment(observation, heroes, placements),
            HeadlessPolicyEvidence.ForDeployment(observation, usesDecisionSeed: true, usesCampaignContext: false));
    }

    public HeadlessRewardDecision DecideReward(HeadlessPolicyObservation observation)
    {
        HeadlessPolicyGuard.ValidateObservation(observation);
        if (observation.RewardOptions.Count == 0)
        {
            return new HeadlessRewardDecision(
                -1,
                "QA coverage only; no visible reward options",
                0d,
                HeadlessPolicyEvidence.ForReward(observation, false, false, true));
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
            $"QA coverage only; favor visible doctrine/protection/counter hook payload={option.Option.PayloadId}",
            option.Score,
            HeadlessPolicyEvidence.ForReward(observation, false, false, true));
    }

    private static double CoverageSelectionScore(IReadOnlyList<HeadlessHeroObservation> heroes)
    {
        var score = heroes.Sum(HeadlessPolicyScoring.ReadinessScore)
                    + (HeadlessPolicyScoring.DoctrineScore(heroes) * 3d)
                    + (heroes.Select(hero => hero.ClassId).Distinct(StringComparer.Ordinal).Count() * 18d);
        score += heroes.Any(IsHealer) ? 120d : 0d;
        score += heroes.Any(hero => string.Equals(hero.ClassId, "vanguard", StringComparison.Ordinal)) ? 40d : 0d;
        score += heroes.Any(hero => string.Equals(hero.ClassId, "duelist", StringComparison.Ordinal)) ? 40d : 0d;
        score += heroes.Any(hero => string.Equals(hero.ClassId, "ranger", StringComparison.Ordinal)) ? 30d : 0d;
        return score;
    }

    private static IReadOnlyList<HeadlessPlacement> PlaceForChannel(
        IReadOnlyList<HeadlessHeroObservation> heroes,
        IReadOnlyList<DeploymentAnchorId> legalAnchors,
        int channelIndex)
    {
        var template = AnchorTemplates[channelIndex];
        if (template.Any(anchor => !legalAnchors.Contains(anchor)))
        {
            return HeadlessPolicyScoring.PlaceFormation(heroes, legalAnchors);
        }

        var ordered = heroes
            .OrderBy(RoleRank)
            .ThenBy(hero => hero.HeroId, StringComparer.Ordinal)
            .ToArray();
        return ordered.Select((hero, index) => new HeadlessPlacement(template[index], hero.HeroId)).ToArray();
    }

    private static int RoleRank(HeadlessHeroObservation hero)
        => hero.ClassId switch
        {
            "vanguard" => 0,
            "duelist" => 1,
            "ranger" => 2,
            "mystic" => 3,
            _ => 4,
        };

    private static bool IsHealer(HeadlessHeroObservation hero)
        => string.Equals(hero.ClassId, "mystic", StringComparison.Ordinal)
           || hero.RoleTag.Contains("heal", StringComparison.OrdinalIgnoreCase)
           || hero.RoleTag.Contains("support", StringComparison.OrdinalIgnoreCase);
}
