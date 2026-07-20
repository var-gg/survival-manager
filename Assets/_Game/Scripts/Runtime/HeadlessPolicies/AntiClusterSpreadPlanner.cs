using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.HeadlessPolicies;

/// <summary>
/// 가시 AoE 경고에만 반응해 현재 사수의 anchor 겹침을 줄인다.
/// 좌표 비교는 정수 격자를 사용하며 site/encounter identity나 전투 결과를 읽지 않는다.
/// </summary>
internal static class AntiClusterSpreadPlanner
{
    public const string RuleId = "anticluster_spread";

    private const int FrontRowX = 280;
    private const int BackRowX = 490;
    private const int TopLaneY = 180;
    private const int CenterLaneY = 0;
    private const int BottomLaneY = -180;
    private const int TelegraphRadius = 185;
    private const int TelegraphRadiusSquared = TelegraphRadius * TelegraphRadius;

    public static bool TryPlan(
        HeadlessPolicyObservation observation,
        EnemyThreatProfile profile,
        IReadOnlyList<HeadlessPlacement> current,
        IReadOnlyList<HeadlessHeroObservation> deployedHeroes,
        out IReadOnlyList<HeadlessPlacement> placements,
        out int formationEdits)
    {
        placements = current;
        formationEdits = 0;
        if (!profile.Tags.Contains(EnemyThreatTag.AntiClusterAoe, StringComparer.Ordinal))
        {
            return false;
        }

        var shooterIds = deployedHeroes
            .Where(IsShooter)
            .Select(hero => hero.HeroId)
            .ToHashSet(StringComparer.Ordinal);
        if (shooterIds.Count < 2)
        {
            return false;
        }

        var currentByHero = current.ToDictionary(value => value.HeroId, value => value.Anchor, StringComparer.Ordinal);
        var currentCatch = MaximumShooterCatch(current, shooterIds);
        Candidate? best = null;
        foreach (var candidatePlacements in HeadlessPolicyScoring.EnumeratePlacements(deployedHeroes, observation.Anchors))
        {
            var edits = candidatePlacements.Count(value => currentByHero[value.HeroId] != value.Anchor);
            if (edits > HeadlessPrepPolicyGuard.MaximumFormationEdits)
            {
                continue;
            }

            var candidate = new Candidate(
                candidatePlacements.OrderBy(value => value.Anchor).ToArray(),
                edits,
                MaximumShooterCatch(candidatePlacements, shooterIds),
                candidatePlacements.Count(value => shooterIds.Contains(value.HeroId) && value.Anchor.IsBackRow()),
                MinimumShooterDistanceSquared(candidatePlacements, shooterIds),
                HeadlessPolicyScoring.EvaluateDeployment(observation, deployedHeroes, candidatePlacements));
            if (best == null || candidate.IsBetterThan(best))
            {
                best = candidate;
            }
        }

        if (best == null || best.MaximumCatch >= currentCatch)
        {
            return false;
        }

        placements = best.Placements;
        formationEdits = best.FormationEdits;
        return true;
    }

    internal static int MaximumShooterCatch(
        IReadOnlyList<HeadlessPlacement> placements,
        ISet<string> shooterIds)
    {
        var shooterAnchors = placements
            .Where(value => shooterIds.Contains(value.HeroId))
            .Select(value => value.Anchor)
            .ToArray();
        return shooterAnchors
            .Select(center => shooterAnchors.Count(anchor => DistanceSquared(center, anchor) <= TelegraphRadiusSquared))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static int MinimumShooterDistanceSquared(
        IReadOnlyList<HeadlessPlacement> placements,
        ISet<string> shooterIds)
    {
        var anchors = placements
            .Where(value => shooterIds.Contains(value.HeroId))
            .Select(value => value.Anchor)
            .ToArray();
        var minimum = int.MaxValue;
        for (var left = 0; left < anchors.Length; left++)
        for (var right = left + 1; right < anchors.Length; right++)
        {
            minimum = Math.Min(minimum, DistanceSquared(anchors[left], anchors[right]));
        }

        return minimum == int.MaxValue ? 0 : minimum;
    }

    private static bool IsShooter(HeadlessHeroObservation hero)
        => string.Equals(hero.ClassId, "ranger", StringComparison.Ordinal)
           && hero.SkillCards.Any(skill =>
               skill.Kind == SkillKind.Strike
               && (skill.Range >= 2.5f
                   || skill.Delivery is SkillDelivery.Ranged or SkillDelivery.Projectile));

    private static int DistanceSquared(DeploymentAnchorId left, DeploymentAnchorId right)
    {
        var (leftX, leftY) = Position(left);
        var (rightX, rightY) = Position(right);
        var deltaX = leftX - rightX;
        var deltaY = leftY - rightY;
        return (deltaX * deltaX) + (deltaY * deltaY);
    }

    private static (int X, int Y) Position(DeploymentAnchorId anchor)
        => (
            anchor.IsFrontRow() ? FrontRowX : BackRowX,
            anchor switch
            {
                DeploymentAnchorId.FrontTop or DeploymentAnchorId.BackTop => TopLaneY,
                DeploymentAnchorId.FrontCenter or DeploymentAnchorId.BackCenter => CenterLaneY,
                _ => BottomLaneY,
            });

    private sealed class Candidate
    {
        public Candidate(
            IReadOnlyList<HeadlessPlacement> placements,
            int formationEdits,
            int maximumCatch,
            int backlineShooterCount,
            int minimumShooterDistanceSquared,
            double visibleDeploymentValue)
        {
            Placements = placements;
            FormationEdits = formationEdits;
            MaximumCatch = maximumCatch;
            BacklineShooterCount = backlineShooterCount;
            MinimumShooterDistanceSquared = minimumShooterDistanceSquared;
            VisibleDeploymentValue = visibleDeploymentValue;
        }

        public IReadOnlyList<HeadlessPlacement> Placements { get; }
        public int FormationEdits { get; }
        public int MaximumCatch { get; }
        public int BacklineShooterCount { get; }
        public int MinimumShooterDistanceSquared { get; }
        public double VisibleDeploymentValue { get; }

        public bool IsBetterThan(Candidate other)
            => MaximumCatch < other.MaximumCatch
               || MaximumCatch == other.MaximumCatch && BacklineShooterCount > other.BacklineShooterCount
               || MaximumCatch == other.MaximumCatch && BacklineShooterCount == other.BacklineShooterCount
               && FormationEdits < other.FormationEdits
               || MaximumCatch == other.MaximumCatch && BacklineShooterCount == other.BacklineShooterCount
               && FormationEdits == other.FormationEdits
               && MinimumShooterDistanceSquared > other.MinimumShooterDistanceSquared
               || MaximumCatch == other.MaximumCatch && BacklineShooterCount == other.BacklineShooterCount
               && FormationEdits == other.FormationEdits
               && MinimumShooterDistanceSquared == other.MinimumShooterDistanceSquared
               && (VisibleDeploymentValue > other.VisibleDeploymentValue + 0.000001d
                   || Math.Abs(VisibleDeploymentValue - other.VisibleDeploymentValue) <= 0.000001d
                   && string.CompareOrdinal(
                       HeadlessPolicyScoring.PlacementSignature(Placements),
                       HeadlessPolicyScoring.PlacementSignature(other.Placements)) < 0);
    }
}
