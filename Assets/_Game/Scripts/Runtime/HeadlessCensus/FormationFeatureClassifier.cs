using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessCensus;

/// <summary>
/// BattleFormationConsequence의 front/back screen 및 lane geometry를 전투 전 정적 eligibility proxy로 투영한다.
/// 실제 flank/save/dive 발동은 battle smoke가 검증하며 census는 runtime predicate를 흉내 내지 않는다.
/// </summary>
public static class FormationFeatureClassifier
{
    /// <summary>동일 feature truth에서 concept catalog가 사용하는 canonical profile id를 반환한다.</summary>
    public static string ClassifyProfile(FormationFeatures features)
    {
        if (features == null)
        {
            throw new ArgumentNullException(nameof(features));
        }

        return ConceptFormationProfile.Classify(features);
    }

    public static FormationFeatures Classify(IEnumerable<DeploymentAnchorId> anchors)
    {
        var anchorsByRole = anchors.ToArray();
        if (anchorsByRole.Length != BuildSpaceEnumerator.SquadSize
            || anchorsByRole.Distinct().Count() != anchorsByRole.Length)
        {
            throw new ArgumentException($"Formation requires {BuildSpaceEnumerator.SquadSize} distinct anchors.", nameof(anchors));
        }

        var occupied = anchorsByRole.OrderBy(anchor => anchor).ToArray();
        var occupiedSet = occupied.ToHashSet();
        var frontlineCount = occupied.Count(anchor => anchor.IsFrontRow());
        var backAnchors = occupied.Where(anchor => anchor.IsBackRow()).ToArray();
        var protectedSlots = backAnchors.Count(anchor => occupiedSet.Contains(ToFrontAnchor(anchor)));
        var rearExposure = backAnchors.Length - protectedSlots;
        var sideExposure = ResolveSideExposure(occupiedSet);
        var exposureScore = ResolveRoleWeightedExposure(anchorsByRole, occupiedSet);
        var supportDistance = ResolveSupportDistance(anchorsByRole);
        var backlineAccessibility = ResolveBacklineAccessibility(anchorsByRole, occupiedSet);

        return new FormationFeatures(
            frontlineCount,
            protectedSlots,
            sideExposure,
            rearExposure,
            Round(exposureScore),
            Round(supportDistance),
            Round(backlineAccessibility));
    }

    private static int ResolveSideExposure(HashSet<DeploymentAnchorId> occupied)
    {
        var exposure = 0;
        foreach (var anchor in occupied)
        {
            foreach (var adjacent in AdjacentSameRow(anchor))
            {
                if (!occupied.Contains(adjacent))
                {
                    exposure++;
                }
            }
        }

        return exposure;
    }

    private static double ResolveRoleWeightedExposure(
        IReadOnlyList<DeploymentAnchorId> anchorsByRole,
        HashSet<DeploymentAnchorId> occupied)
    {
        var score = 0d;
        for (var roleIndex = 0; roleIndex < anchorsByRole.Count; roleIndex++)
        {
            var anchor = anchorsByRole[roleIndex];
            var roleWeight = RoleExposureWeight((BuildRole)roleIndex);
            score += AdjacentSameRow(anchor).Count(adjacent => !occupied.Contains(adjacent)) * roleWeight;
            if (anchor.IsBackRow() && !occupied.Contains(ToFrontAnchor(anchor)))
            {
                score += roleWeight * 2d;
            }
        }

        return score;
    }

    private static double ResolveSupportDistance(IReadOnlyList<DeploymentAnchorId> anchorsByRole)
    {
        var layout = BattlefieldLayout.Default;
        var supportAnchor = anchorsByRole[(int)BuildRole.Healer];
        var supportPosition = layout.ResolveAnchorPosition(TeamSide.Ally, supportAnchor);
        var total = 0d;
        var targets = 0;
        for (var roleIndex = 0; roleIndex < anchorsByRole.Count; roleIndex++)
        {
            if (roleIndex == (int)BuildRole.Healer)
            {
                continue;
            }

            var targetPosition = layout.ResolveAnchorPosition(TeamSide.Ally, anchorsByRole[roleIndex]);
            var dx = (double)supportPosition.X - targetPosition.X;
            var dy = (double)supportPosition.Y - targetPosition.Y;
            total += Math.Sqrt((dx * dx) + (dy * dy));
            targets++;
        }

        return targets == 0 ? 0d : total / targets;
    }

    private static double ResolveBacklineAccessibility(
        IReadOnlyList<DeploymentAnchorId> anchorsByRole,
        HashSet<DeploymentAnchorId> occupied)
    {
        var score = 0d;
        foreach (var role in new[] { BuildRole.Ranged, BuildRole.Healer })
        {
            var anchor = anchorsByRole[(int)role];
            if (anchor.IsFrontRow())
            {
                score += 1d;
                continue;
            }

            var front = ToFrontAnchor(anchor);
            if (!occupied.Contains(front))
            {
                score += 1d;
                continue;
            }

            if (AdjacentSameRow(front).Any(adjacent => adjacent.IsFrontRow() && !occupied.Contains(adjacent)))
            {
                score += 0.5d;
            }
        }

        return score / 2d;
    }

    private static double RoleExposureWeight(BuildRole role)
        => role switch
        {
            BuildRole.Tank => 0.5d,
            BuildRole.Damage => 0.75d,
            BuildRole.Ranged => 1d,
            BuildRole.Healer => 1.25d,
            _ => 1d,
        };

    private static IEnumerable<DeploymentAnchorId> AdjacentSameRow(DeploymentAnchorId anchor)
    {
        var rowOffset = anchor.IsFrontRow() ? 0 : 3;
        var lane = (int)anchor - rowOffset;
        if (lane > 0)
        {
            yield return (DeploymentAnchorId)(rowOffset + lane - 1);
        }

        if (lane < 2)
        {
            yield return (DeploymentAnchorId)(rowOffset + lane + 1);
        }
    }

    private static DeploymentAnchorId ToFrontAnchor(DeploymentAnchorId backAnchor)
        => backAnchor switch
        {
            DeploymentAnchorId.BackTop => DeploymentAnchorId.FrontTop,
            DeploymentAnchorId.BackCenter => DeploymentAnchorId.FrontCenter,
            DeploymentAnchorId.BackBottom => DeploymentAnchorId.FrontBottom,
            _ => backAnchor,
        };

    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}
