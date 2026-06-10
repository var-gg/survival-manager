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
        var context = state.GetTacticContext(actor.Side);
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
            TargetSelector.NearestReachableEnemy => candidates
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target) + ResolveTacticTargetBias(state, actor, target, context))
                .ThenBy(target => target.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault(),
            TargetSelector.NearestFrontlineEnemy => candidates
                .Where(target => target.Behavior.FormationLine == FormationLine.Frontline)
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target) + ResolveTacticTargetBias(state, actor, target, context))
                .ThenBy(target => target.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault() ?? ResolveFallback(state, actor, CloneRuleWithFallback(rule, TargetFallbackPolicy.NearestReachableEnemy), candidates),
            TargetSelector.LowestCurrentHpEnemy => candidates.OrderBy(target => target.CurrentHealth).ThenBy(target => ResolveTacticTargetBias(state, actor, target, context)).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault(),
            TargetSelector.LowestHpPercentEnemy => candidates.OrderBy(target => target.HealthRatio).ThenBy(target => ResolveTacticTargetBias(state, actor, target, context)).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault(),
            TargetSelector.LowestEhpEnemy => candidates.OrderBy(target => EstimateEhpAgainst(actor, target)).ThenBy(target => ResolveTacticTargetBias(state, actor, target, context)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault(),
            TargetSelector.MarkedEnemy => candidates.FirstOrDefault(target => target.HasStatus("marked")) ?? ResolveFallback(state, actor, rule, candidates),
            TargetSelector.LargestEnemyCluster => candidates
                .OrderByDescending(target => CountClusterTargets(candidates, target.Position, Math.Max(0.1f, rule.ClusterRadius)))
                .ThenBy(target => ResolveTacticTargetBias(state, actor, target, context))
                .ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target))
                .ThenBy(target => target.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault(),
            TargetSelector.BacklineExposedEnemy => candidates
                .Where(target => IsBacklineExposedEnemy(state, target))
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target) + ResolveTacticTargetBias(state, actor, target, context))
                .ThenBy(target => target.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault() ?? ResolveFallback(state, actor, rule, candidates),
            TargetSelector.LowestCurrentHpAlly => candidates.OrderBy(target => target.CurrentHealth).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault(),
            TargetSelector.LowestHpPercentAlly => candidates.OrderBy(target => target.HealthRatio).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault(),
            TargetSelector.LowestEhpAlly => candidates.OrderBy(EstimateEhpAgainstAverageThreat).ThenBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault(),
            TargetSelector.NearestInjuredAlly => candidates.Where(target => target.CurrentHealth < target.MaxHealth).OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target)).ThenBy(target => target.Id.Value, StringComparer.Ordinal).FirstOrDefault() ?? ResolveFallback(state, actor, rule, candidates),
            TargetSelector.EmptyPointNearSelf => actor,
            TargetSelector.EmptyPointNearTarget => currentTarget,
            _ => ResolveFallback(state, actor, rule, candidates, context),
        };

        var result = IsValidCandidate(state, actor, selected, rule)
            ? selected
            : ResolveFallback(state, actor, rule, candidates, context);
        return ApplyMeleeNearestEngagementOverride(state, actor, result, rule);
    }

    // Q5 (GPT Pro): "근접은 nearest-reachable 교전, focus-fire는 soft preference." Melee engagers pick the
    // NEAREST reachable enemy rather than converging on one far focus target — the focus-fire pile-up is the
    // dominant residual treadmill once the movement layer is clean. Focus-fire is preserved only LOCALLY: the
    // authored pick is kept when it is within a small band of the nearest (so among near-equidistant enemies a
    // low-HP target is still finished off), but a focus target that is meaningfully farther than the nearest is
    // abandoned. Ranged units and forced/marked/locked targeting keep strict focus-fire. Pure function of truth
    // positions (deterministic, reproduced on re-sim, no serialization); the focus band + the upstream retarget
    // lock provide hysteresis against per-tick thrash.
    private const float MeleeFocusBand = 0.5f; // keep the authored focus if it is within this much of the nearest

    private static UnitSnapshot? ApplyMeleeNearestEngagementOverride(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot? selected,
        TargetRule rule)
    {
        if (selected == null
            || rule.Domain != TargetDomain.EnemyUnit
            || selected.Side == actor.Side
            || !IsMeleeEngager(actor)
            || IsForcedTargetRule(rule))
        {
            return selected;
        }

        UnitSnapshot? nearest = null;
        var nearestEdge = float.MaxValue;
        foreach (var enemy in state.GetOpponents(actor.Side))
        {
            if (!enemy.IsAlive || !IsValidCandidate(state, actor, enemy, rule, skipRangeFilter: true))
            {
                continue;
            }

            var edge = MovementResolver.ComputeEdgeDistance(actor, enemy);
            if (edge < nearestEdge
                || (Math.Abs(edge - nearestEdge) <= 1e-4f && nearest != null && string.CompareOrdinal(enemy.Id.Value, nearest.Id.Value) < 0))
            {
                nearest = enemy;
                nearestEdge = edge;
            }
        }

        if (nearest == null || nearest.Id == selected.Id)
        {
            return selected;
        }

        // Keep the authored focus only when it is roughly as close as the nearest (local focus-fire); otherwise
        // engage the nearest reachable enemy instead of walking past it.
        var selectedEdge = MovementResolver.ComputeEdgeDistance(actor, selected);
        return selectedEdge <= nearestEdge + MeleeFocusBand ? selected : nearest;
    }

    private static bool IsMeleeEngager(UnitSnapshot actor)
    {
        // Short-range engagers only. Rangers/casters keep strict focus-fire (their long range never forces them
        // to walk past a nearer enemy, so the pile-up this fixes doesn't apply to them).
        return actor.AttackRange <= 1.8f;
    }

    private static bool IsForcedTargetRule(TargetRule rule)
    {
        // Only genuine "this exact target" intents are exempt (taunt / marked / explicit current-target).
        // NOT LockTargetAtCastStart — that merely forbids switching mid-cast and is handled upstream by the
        // stable-target lock; it defaults true on every rule, so gating on it would disable the override entirely.
        return rule.PrimarySelector is TargetSelector.MarkedEnemy or TargetSelector.CurrentTarget
               || rule.Filters.HasFlag(TargetFilterFlags.RequireMarked);
    }

    internal static float ComputeExposureScore(BattleState state, UnitSnapshot target)
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
        IEnumerable<UnitSnapshot> candidates,
        TacticContext? context = null)
    {
        context ??= state.GetTacticContext(actor.Side);
        return rule.FallbackPolicy switch
        {
            TargetFallbackPolicy.Abort => null,
            TargetFallbackPolicy.KeepCurrentIfStillValid => IsValidCandidate(state, actor, state.FindUnit(actor.CurrentTargetId), rule)
                ? state.FindUnit(actor.CurrentTargetId)
                : null,
            TargetFallbackPolicy.NearestReachableEnemy => candidates
                .Where(target => target.Side != actor.Side)
                .OrderBy(target => MovementResolver.ComputeEdgeDistance(actor, target) + ResolveTacticTargetBias(state, actor, target, context))
                .ThenBy(target => target.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault(),
            TargetFallbackPolicy.LowestCurrentHpEnemy => candidates
                .Where(target => target.Side != actor.Side)
                .OrderBy(target => target.CurrentHealth)
                .ThenBy(target => ResolveTacticTargetBias(state, actor, target, context))
                .ThenBy(target => target.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault(),
            TargetFallbackPolicy.Self => actor,
            _ => null,
        };
    }

    private static float ResolveTacticTargetBias(BattleState state, UnitSnapshot actor, UnitSnapshot target, TacticContext context)
    {
        if (target.Side == actor.Side)
        {
            return 0f;
        }

        var currentFocus = state.GetOpponents(target.Side)
            .Count(unit => unit.IsAlive
                           && unit.Id != actor.Id
                           && (unit.CurrentTargetId == target.Id || unit.PendingTargetId == target.Id));
        var switchPenalty = actor.CurrentTargetId != null && actor.CurrentTargetId != target.Id
            ? context.TargetSwitchPenalty * 0.15f
            : 0f;
        var focusBias = context.FocusModeBias >= 0f
            ? -context.FocusModeBias * currentFocus * 0.35f
            : MathF.Abs(context.FocusModeBias) * currentFocus * 0.55f;
        var flankBias = MathF.Abs(context.FlankBias) * (target.Behavior.FormationLine == FormationLine.Backline ? -0.08f : 0f);
        // P0 차단: 스크린이 멀쩡한 후열은 일반 타게팅에서 덜 매력적이다(거리 미터 등가 페널티).
        // Dive 의도는 RoleBrain 경로로 이 페널티를 거치지 않는다 — 스크린을 뚫는 건 다이버의 일.
        var screenPenalty = BattleFormationConsequence.IsScreenedBackline(state, target)
            ? BattleFormationConsequence.ScreenedTargetScorePenalty
            : 0f;
        return switchPenalty + focusBias + flankBias + screenPenalty;
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
        // 노출 술어의 단일 진실은 BattleFormationConsequence — 타게팅과 피해 경감이 같은 판정을 공유한다.
        return BattleFormationConsequence.IsBacklineExposed(state, target);
    }
}
