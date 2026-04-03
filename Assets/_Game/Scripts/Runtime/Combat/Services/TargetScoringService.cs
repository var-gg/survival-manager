using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public static class TargetScoringService
{
    public static UnitSnapshot? SelectTarget(
        BattleState state,
        UnitSnapshot actor,
        TargetSelectorType selector,
        BattleActionType actionType,
        string? skillId)
    {
        var bridgeRule = selector switch
        {
            TargetSelectorType.Self => new TargetRule { Domain = TargetDomain.Self, PrimarySelector = TargetSelector.Self, FallbackPolicy = TargetFallbackPolicy.Self, Filters = TargetFilterFlags.None },
            TargetSelectorType.LowestHpAlly => new TargetRule { Domain = TargetDomain.AlliedUnit, PrimarySelector = TargetSelector.LowestHpPercentAlly, FallbackPolicy = TargetFallbackPolicy.Self, Filters = TargetFilterFlags.ExcludeFullHealthAllies | TargetFilterFlags.ExcludeUntargetable },
            TargetSelectorType.FirstEnemyInRange => new TargetRule { Domain = TargetDomain.EnemyUnit, PrimarySelector = TargetSelector.NearestReachableEnemy, FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy, Filters = TargetFilterFlags.InRange },
            TargetSelectorType.LowestHpEnemy => new TargetRule { Domain = TargetDomain.EnemyUnit, PrimarySelector = TargetSelector.LowestHpPercentEnemy, FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy, Filters = TargetFilterFlags.ExcludeUntargetable },
            TargetSelectorType.MostExposedEnemy => new TargetRule { Domain = TargetDomain.EnemyUnit, PrimarySelector = TargetSelector.BacklineExposedEnemy, FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy, Filters = TargetFilterFlags.ExcludeUntargetable },
            _ => new TargetRule { Domain = TargetDomain.EnemyUnit, PrimarySelector = TargetSelector.NearestReachableEnemy, FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy, Filters = TargetFilterFlags.ExcludeUntargetable },
        };
        bridgeRule.MaxAcquireRange = actionType == BattleActionType.BasicAttack
            ? actor.AttackRange
            : actor.ResolveActionRange(skillId);
        return SelectTarget(state, actor, bridgeRule);
    }

    public static UnitSnapshot? SelectTarget(BattleState state, UnitSnapshot actor, TargetRule? authoredRule)
    {
        var rule = authoredRule ?? new TargetRule();
        actor.SetTargetDebugState(rule.PrimarySelector, rule.FallbackPolicy);

        if (rule.Domain == TargetDomain.Self || rule.PrimarySelector == TargetSelector.Self)
        {
            return actor;
        }

        var currentTarget = state.FindUnit(actor.CurrentTargetId);
        if (rule.PrimarySelector == TargetSelector.CurrentTarget && IsValidCandidate(state, actor, currentTarget, rule))
        {
            return currentTarget;
        }

        var candidates = GetCandidates(state, actor, rule).ToList();
        if (candidates.Count == 0)
        {
            if (rule.Filters.HasFlag(TargetFilterFlags.InRange)
                && rule.FallbackPolicy is TargetFallbackPolicy.NearestReachableEnemy or TargetFallbackPolicy.LowestCurrentHpEnemy)
            {
                var relaxedCandidates = GetCandidates(state, actor, rule, skipRangeFilter: true).ToList();
                if (relaxedCandidates.Count > 0)
                {
                    return ResolveFallback(state, actor, rule, relaxedCandidates);
                }
            }

            return ResolveFallback(state, actor, rule, Array.Empty<UnitSnapshot>());
        }

        var selected = rule.PrimarySelector switch
        {
            TargetSelector.NearestReachableEnemy => candidates.OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.NearestFrontlineEnemy => candidates
                .Where(target => target.Behavior.FormationLine == FormationLine.Frontline)
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target))
                .ThenBy(target => target.Id.Value)
                .FirstOrDefault() ?? ResolveFallback(state, actor, CloneRuleWithFallback(rule, TargetFallbackPolicy.NearestReachableEnemy), candidates),
            TargetSelector.LowestCurrentHpEnemy => candidates.OrderBy(target => target.CurrentHealth).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.LowestHpPercentEnemy => candidates.OrderBy(target => target.HealthRatio).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.LowestEhpEnemy => candidates.OrderBy(target => EstimateEhpAgainst(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.MarkedEnemy => candidates.FirstOrDefault(target => target.HasStatus("marked")) ?? ResolveFallback(state, actor, rule, candidates),
            TargetSelector.LargestEnemyCluster => candidates
                .OrderByDescending(target => CountClusterTargets(candidates, target.Position, Math.Max(0.1f, rule.ClusterRadius)))
                .ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target))
                .ThenBy(target => target.Id.Value)
                .FirstOrDefault(),
            TargetSelector.BacklineExposedEnemy => candidates
                .Where(target => IsBacklineExposedEnemy(state, target))
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target))
                .ThenBy(target => target.Id.Value)
                .FirstOrDefault() ?? ResolveFallback(state, actor, rule, candidates),
            TargetSelector.LowestCurrentHpAlly => candidates.OrderBy(target => target.CurrentHealth).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.LowestHpPercentAlly => candidates.OrderBy(target => target.HealthRatio).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.LowestEhpAlly => candidates.OrderBy(EstimateEhpAgainstAverageThreat).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault(),
            TargetSelector.NearestInjuredAlly => candidates.Where(target => target.CurrentHealth < target.MaxHealth).OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value).FirstOrDefault() ?? ResolveFallback(state, actor, rule, candidates),
            TargetSelector.EmptyPointNearSelf => actor,
            TargetSelector.EmptyPointNearTarget => currentTarget,
            _ => ResolveFallback(state, actor, rule, candidates),
        };

        return IsValidCandidate(state, actor, selected, rule)
            ? selected
            : ResolveFallback(state, actor, rule, candidates);
    }

    public static float ComputeExposureScore(BattleState state, UnitSnapshot target)
    {
        var allies = state.GetTeam(target.Side)
            .Where(unit => unit.IsAlive && unit.Id != target.Id)
            .ToList();
        var nearestAllyDistance = allies.Count == 0
            ? 4f
            : allies.Min(unit => unit.Position.DistanceTo(target.Position));
        var frontlineGuard = state.GetTeam(target.Side)
            .Where(unit => unit.IsAlive && unit.Behavior.FormationLine == FormationLine.Frontline && unit.Id != target.Id)
            .DefaultIfEmpty()
            .Max(unit => unit == null ? 0f : Math.Max(0f, unit.Behavior.FrontlineGuardRadius - unit.Position.DistanceTo(target.Position)));
        var anchorPenalty = target.Behavior.FormationLine == FormationLine.Backline ? 1.5f : 0f;
        return nearestAllyDistance + anchorPenalty - frontlineGuard;
    }

    public static float EstimateEhpAgainst(UnitSnapshot source, UnitSnapshot target)
    {
        var mitigation = Math.Max(target.Armor, target.Resist);
        var incomingScalar = Math.Max(0.25f, target.GetIncomingDamageMultiplier());
        var sourcePressure = Math.Max(1f, source.PhysPower + source.MagPower);
        return (target.CurrentHealth + target.Barrier + mitigation) * incomingScalar / sourcePressure;
    }

    public static float EstimateEhpAgainstAverageThreat(UnitSnapshot target)
    {
        return (target.CurrentHealth + target.Barrier + Math.Max(target.Armor, target.Resist)) * Math.Max(0.25f, target.GetIncomingDamageMultiplier());
    }

    private static IEnumerable<UnitSnapshot> GetCandidates(BattleState state, UnitSnapshot actor, TargetRule rule, bool skipRangeFilter = false)
    {
        IEnumerable<UnitSnapshot> baseCandidates = rule.Domain switch
        {
            TargetDomain.AlliedUnit => state.GetTeam(actor.Side),
            TargetDomain.EnemyUnit => state.GetOpponents(actor.Side),
            TargetDomain.GroundPoint => state.GetOpponents(actor.Side),
            _ => Array.Empty<UnitSnapshot>(),
        };

        return baseCandidates.Where(target => IsValidCandidate(state, actor, target, rule, skipRangeFilter));
    }

    private static bool IsValidCandidate(BattleState state, UnitSnapshot actor, UnitSnapshot? target, TargetRule rule, bool skipRangeFilter = false)
    {
        if (target == null || !target.IsAlive)
        {
            return false;
        }

        if (rule.Domain == TargetDomain.EnemyUnit && target.Side == actor.Side)
        {
            return false;
        }

        if (rule.Domain == TargetDomain.AlliedUnit && target.Side != actor.Side)
        {
            return false;
        }

        if (rule.Domain == TargetDomain.AlliedUnit && target.Id == actor.Id && rule.PrimarySelector != TargetSelector.Self)
        {
            return false;
        }

        if (rule.Filters.HasFlag(TargetFilterFlags.ExcludeSummons) && target.EntityKind != CombatEntityKind.RosterUnit)
        {
            return false;
        }

        if (rule.Filters.HasFlag(TargetFilterFlags.ExcludeFullHealthAllies) && target.CurrentHealth >= target.MaxHealth)
        {
            return false;
        }

        if (rule.Filters.HasFlag(TargetFilterFlags.RequireMarked) && !target.HasStatus("marked"))
        {
            return false;
        }

        if (rule.Filters.HasFlag(TargetFilterFlags.RequireBacklineExposed) && !IsBacklineExposedEnemy(state, target))
        {
            return false;
        }

        if (!skipRangeFilter && rule.Filters.HasFlag(TargetFilterFlags.InRange))
        {
            var acquireRange = ResolveAcquireRange(actor, rule);
            if (MovementResolver.ComputeEdgeDistance(actor, target) > acquireRange)
            {
                return false;
            }
        }

        return true;
    }

    private static UnitSnapshot? ResolveFallback(
        BattleState state,
        UnitSnapshot actor,
        TargetRule rule,
        IEnumerable<UnitSnapshot> candidates)
    {
        return rule.FallbackPolicy switch
        {
            TargetFallbackPolicy.Abort => null,
            TargetFallbackPolicy.KeepCurrentIfStillValid => IsValidCandidate(state, actor, state.FindUnit(actor.CurrentTargetId), rule)
                ? state.FindUnit(actor.CurrentTargetId)
                : null,
            TargetFallbackPolicy.NearestReachableEnemy => candidates
                .Where(target => target.Side != actor.Side)
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target))
                .ThenBy(target => target.Id.Value)
                .FirstOrDefault(),
            TargetFallbackPolicy.LowestCurrentHpEnemy => candidates
                .Where(target => target.Side != actor.Side)
                .OrderBy(target => target.CurrentHealth)
                .ThenBy(target => target.Id.Value)
                .FirstOrDefault(),
            TargetFallbackPolicy.Self => actor,
            _ => null,
        };
    }

    private static float ResolveAcquireRange(UnitSnapshot actor, TargetRule rule)
    {
        var acquireRange = rule.MaxAcquireRange > 0f
            ? rule.MaxAcquireRange
            : actor.AttackRange;
        return acquireRange + 0.05f;
    }

    private static TargetRule CloneRuleWithFallback(TargetRule source, TargetFallbackPolicy fallbackPolicy)
    {
        return new TargetRule
        {
            Domain = source.Domain,
            PrimarySelector = source.PrimarySelector,
            FallbackPolicy = fallbackPolicy,
            Filters = source.Filters,
            ReevaluateIntervalSeconds = source.ReevaluateIntervalSeconds,
            MinimumCommitSeconds = source.MinimumCommitSeconds,
            MaxAcquireRange = source.MaxAcquireRange,
            PreferredMinTargets = source.PreferredMinTargets,
            ClusterRadius = source.ClusterRadius,
            LockTargetAtCastStart = source.LockTargetAtCastStart,
            RetargetLockMode = source.RetargetLockMode,
        };
    }

    private static int CountClusterTargets(IEnumerable<UnitSnapshot> candidates, CombatVector2 center, float radius)
    {
        return candidates.Count(target => target.Position.DistanceTo(center) <= radius);
    }

    private static bool IsBacklineExposedEnemy(BattleState state, UnitSnapshot target)
    {
        if (target.Behavior.FormationLine != FormationLine.Backline)
        {
            return false;
        }

        return !state.GetTeam(target.Side)
            .Where(unit => unit.IsAlive && unit.Id != target.Id && unit.Behavior.FormationLine == FormationLine.Frontline)
            .Any(frontliner => frontliner.Position.DistanceTo(target.Position) <= frontliner.Behavior.FrontlineGuardRadius);
    }
}
