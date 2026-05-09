using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class AoeClusterSelectorService
{
    public static AoeClusterSelection Select(
        EffectPositionSnapshot snapshot,
        TeamSide targetSide,
        BattleAreaEffectFamily family,
        float radius,
        float skillBias = 0f,
        float friendlyRisk = 0f,
        string? preferredPrimaryTargetId = null)
    {
        var safeRadius = Math.Max(0.1f, radius);
        var targets = snapshot.Units
            .Where(unit => unit.Side == targetSide)
            .OrderBy(unit => unit.UnitId, StringComparer.Ordinal)
            .ToList();
        if (targets.Count == 0)
        {
            var empty = new AoeClusterCandidate("empty", CombatVector2.Zero, 0f, 0f, 0, skillBias, friendlyRisk, skillBias - friendlyRisk);
            return new AoeClusterSelection(family, safeRadius, empty, Array.Empty<AoeDistributionHit>());
        }

        var candidates = BuildCandidates(targets, safeRadius, skillBias, friendlyRisk, preferredPrimaryTargetId).ToList();
        var best = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .First();
        var hits = ResolveDistribution(targets, best.Center, safeRadius, family);
        return new AoeClusterSelection(family, safeRadius, best, hits);
    }

    public static IReadOnlyList<AoeDistributionHit> ResolveDistribution(
        IReadOnlyList<EffectPositionUnit> targets,
        CombatVector2 center,
        float radius,
        BattleAreaEffectFamily family)
    {
        var safeRadius = Math.Max(0.1f, radius);
        var inRadius = targets
            .Select(unit =>
            {
                var distance = unit.Position.DistanceTo(center);
                var falloff = ResolveFalloff(distance, safeRadius);
                return new { Unit = unit, Distance = distance, Falloff = falloff };
            })
            .Where(hit => hit.Distance <= safeRadius)
            .OrderBy(hit => hit.Distance)
            .ThenBy(hit => hit.Unit.UnitId, StringComparer.Ordinal)
            .ToList();

        return family switch
        {
            BattleAreaEffectFamily.GroundAoe => inRadius
                .Select(hit => new AoeDistributionHit(hit.Unit.UnitId, hit.Distance, hit.Falloff, 0.70f + (0.30f * hit.Falloff)))
                .ToList(),
            BattleAreaEffectFamily.PrimarySplash => inRadius
                .Select((hit, index) => new AoeDistributionHit(
                    hit.Unit.UnitId,
                    hit.Distance,
                    hit.Falloff,
                    index == 0 ? 1f : 0.55f + (0.35f * hit.Falloff)))
                .ToList(),
            BattleAreaEffectFamily.CleaveCone => inRadius
                .Select((hit, index) => new AoeDistributionHit(
                    hit.Unit.UnitId,
                    hit.Distance,
                    hit.Falloff,
                    index == 0 ? 1f : 0.65f + (0.15f * hit.Falloff)))
                .ToList(),
            BattleAreaEffectFamily.Chain => inRadius
                .Take(3)
                .Select((hit, index) => new AoeDistributionHit(
                    hit.Unit.UnitId,
                    hit.Distance,
                    hit.Falloff,
                    MathF.Pow(0.75f, index),
                    index))
                .ToList(),
            BattleAreaEffectFamily.KnockbackWave => inRadius
                .Select(hit => new AoeDistributionHit(hit.Unit.UnitId, hit.Distance, hit.Falloff, 0.35f + (0.25f * hit.Falloff)))
                .ToList(),
            _ => inRadius.Take(1).Select(hit => new AoeDistributionHit(hit.Unit.UnitId, hit.Distance, hit.Falloff, 1f)).ToList(),
        };
    }

    private static IEnumerable<AoeClusterCandidate> BuildCandidates(
        IReadOnlyList<EffectPositionUnit> targets,
        float radius,
        float skillBias,
        float friendlyRisk,
        string? preferredPrimaryTargetId)
    {
        foreach (var target in targets)
        {
            yield return Score(
                $"primary:{target.UnitId}",
                target.Position,
                ResolvePrimaryTargetValue(target, preferredPrimaryTargetId),
                targets,
                radius,
                skillBias,
                friendlyRisk);
        }

        for (var i = 0; i < targets.Count; i++)
        {
            for (var j = i + 1; j < targets.Count; j++)
            {
                var midpoint = new CombatVector2(
                    (targets[i].Position.X + targets[j].Position.X) * 0.5f,
                    (targets[i].Position.Y + targets[j].Position.Y) * 0.5f);
                yield return Score($"pair:{targets[i].UnitId}:{targets[j].UnitId}", midpoint, 0.75f, targets, radius, skillBias, friendlyRisk);
            }
        }

        var frontliners = targets.Where(target => target.FormationLine == SM.Core.Contracts.FormationLine.Frontline).ToList();
        if (frontliners.Count > 0)
        {
            yield return Score("guard_cluster", Centroid(frontliners), 0.65f, targets, radius, skillBias, friendlyRisk);
        }

        foreach (var marked in targets.Where(target => target.IsMarked || target.IsVulnerable))
        {
            yield return Score($"marked:{marked.UnitId}", marked.Position, 1.25f, targets, radius, skillBias + 0.1f, friendlyRisk);
        }

        yield return Score("largest_cluster", Centroid(ResolveLargestCluster(targets, radius)), 0.8f, targets, radius, skillBias, friendlyRisk);
    }

    private static AoeClusterCandidate Score(
        string id,
        CombatVector2 center,
        float primaryTargetValue,
        IReadOnlyList<EffectPositionUnit> targets,
        float radius,
        float skillBias,
        float friendlyRisk)
    {
        var clusterMass = 0f;
        var affectedCount = 0;
        foreach (var target in targets)
        {
            var distance = target.Position.DistanceTo(center);
            if (distance <= radius)
            {
                affectedCount++;
            }

            clusterMass += ResolveTargetWeight(target) * ResolveFalloff(distance, radius);
        }

        var score = primaryTargetValue + (0.45f * clusterMass) + (0.20f * Math.Max(0, affectedCount - 1)) + skillBias - friendlyRisk;
        return new AoeClusterCandidate(id, center, primaryTargetValue, clusterMass, affectedCount, skillBias, friendlyRisk, score);
    }

    private static IReadOnlyList<EffectPositionUnit> ResolveLargestCluster(IReadOnlyList<EffectPositionUnit> targets, float radius)
    {
        var best = targets.Take(1).ToList();
        foreach (var target in targets)
        {
            var cluster = targets
                .Where(candidate => candidate.Position.DistanceTo(target.Position) <= radius)
                .OrderBy(candidate => candidate.UnitId, StringComparer.Ordinal)
                .ToList();
            if (cluster.Count > best.Count
                || (cluster.Count == best.Count && string.CompareOrdinal(cluster[0].UnitId, best[0].UnitId) < 0))
            {
                best = cluster;
            }
        }

        return best;
    }

    private static CombatVector2 Centroid(IReadOnlyList<EffectPositionUnit> targets)
    {
        if (targets.Count == 0)
        {
            return CombatVector2.Zero;
        }

        return new CombatVector2(
            targets.Sum(target => target.Position.X) / targets.Count,
            targets.Sum(target => target.Position.Y) / targets.Count);
    }

    private static float ResolvePrimaryTargetValue(EffectPositionUnit target, string? preferredPrimaryTargetId)
    {
        var preferred = string.Equals(target.UnitId, preferredPrimaryTargetId, StringComparison.Ordinal) ? 0.4f : 0f;
        var lowHp = 1f - Math.Clamp(target.HealthRatio, 0f, 1f);
        return 1f + preferred + (lowHp * 0.25f);
    }

    private static float ResolveTargetWeight(EffectPositionUnit target)
    {
        return 0.75f + ((1f - Math.Clamp(target.HealthRatio, 0f, 1f)) * 0.25f) + (target.IsMarked ? 0.2f : 0f);
    }

    private static float ResolveFalloff(float distance, float radius)
    {
        var ratio = radius <= 0.0001f ? 1f : distance / radius;
        return Math.Clamp(1f - (0.35f * ratio * ratio), 0f, 1f);
    }
}
