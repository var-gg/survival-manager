using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

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
        return selector switch
        {
            TargetSelectorType.Self => actor,
            TargetSelectorType.LowestHpAlly => SelectLowestHpAlly(state, actor),
            TargetSelectorType.FirstEnemyInRange => SelectEnemy(state, actor, selector, actor.ResolveActionRange(skillId), requireRange: true),
            TargetSelectorType.LowestHpEnemy => SelectEnemy(state, actor, selector, actor.ResolveActionRange(skillId), requireRange: false),
            TargetSelectorType.NearestEnemy => SelectEnemy(state, actor, selector, actor.ResolveActionRange(skillId), requireRange: false),
            TargetSelectorType.MostExposedEnemy => SelectEnemy(state, actor, selector, actor.ResolveActionRange(skillId), requireRange: false),
            _ => null,
        };
    }

    public static float ComputeExposureScore(BattleState state, UnitSnapshot target)
    {
        var allies = state.GetTeam(target.Side)
            .Where(unit => unit.IsAlive && unit.Id != target.Id)
            .ToList();
        var nearestAllyDistance = allies.Count == 0
            ? 4f
            : allies.Min(unit => unit.Position.DistanceTo(target.Position));
        var anchorPenalty = target.Anchor.IsBackRow() ? 1.5f : 0f;
        return nearestAllyDistance + anchorPenalty;
    }

    private static UnitSnapshot? SelectLowestHpAlly(BattleState state, UnitSnapshot actor)
    {
        return state.GetTeam(actor.Side)
            .Where(unit => unit.IsAlive)
            .OrderBy(unit => unit.HealthRatio)
            .ThenBy(unit => unit.Position.DistanceTo(actor.Position))
            .ThenBy(unit => unit.Id.Value)
            .FirstOrDefault();
    }

    private static UnitSnapshot? SelectEnemy(
        BattleState state,
        UnitSnapshot actor,
        TargetSelectorType selector,
        float actionRange,
        bool requireRange)
    {
        var candidates = state.GetOpponents(actor.Side)
            .Where(unit => unit.IsAlive)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var scored = new List<(UnitSnapshot Target, float Score)>();
        foreach (var target in candidates)
        {
            var distance = actor.Position.DistanceTo(target.Position);
            if (requireRange && distance > actionRange + 0.05f)
            {
                continue;
            }

            var score = ComputeSelectorScore(state, actor, target, selector, distance);
            score += ComputePostureBias(state, actor, target, distance);
            if (actor.CurrentTargetId == target.Id && actor.TargetSwitchLockRemaining > 0f)
            {
                score += 25f;
            }

            score += ComputeSeedTieBreaker(state.Seed, actor, target);
            scored.Add((target, score));
        }

        return scored
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Target.Id.Value)
            .Select(item => item.Target)
            .FirstOrDefault();
    }

    private static float ComputeSelectorScore(BattleState state, UnitSnapshot actor, UnitSnapshot target, TargetSelectorType selector, float distance)
    {
        return selector switch
        {
            TargetSelectorType.FirstEnemyInRange => 100f - distance,
            TargetSelectorType.LowestHpEnemy => 100f - (target.HealthRatio * 100f) - (distance * 0.4f),
            TargetSelectorType.NearestEnemy => 100f - (distance * 4f),
            TargetSelectorType.MostExposedEnemy => 50f + (ComputeExposureScore(state, target) * 8f) - distance,
            _ => 20f - distance,
        };
    }

    private static float ComputePostureBias(BattleState state, UnitSnapshot actor, UnitSnapshot target, float distance)
    {
        var posture = state.GetPosture(actor.Side);
        var carryThreat = IsThreateningCarry(state, actor.Side, target) ? 8f : 0f;
        var weakLane = ResolveWeakLane(state, actor.Side);
        var laneBias = target.Anchor.LaneIndex() == weakLane ? 4f : 0f;

        return posture switch
        {
            TeamPostureType.HoldLine => (target.Anchor.IsFrontRow() ? 4f : -2f) - (target.Position.DistanceTo(actor.AnchorPosition) * 0.4f),
            TeamPostureType.StandardAdvance => 2f - (distance * 0.2f),
            TeamPostureType.ProtectCarry => carryThreat + (target.Anchor.IsFrontRow() ? 3f : -1f),
            TeamPostureType.CollapseWeakSide => laneBias + (target.Anchor.IsBackRow() ? 1f : 0f),
            TeamPostureType.AllInBackline => target.Anchor.IsBackRow() ? 10f : -3f,
            _ => 0f,
        };
    }

    private static bool IsThreateningCarry(BattleState state, TeamSide defendingSide, UnitSnapshot target)
    {
        var carryIds = state.GetTeam(defendingSide)
            .Where(unit => unit.IsAlive && unit.Anchor.IsBackRow())
            .Select(unit => unit.Id)
            .ToHashSet();
        if (carryIds.Count == 0)
        {
            return false;
        }

        return target.CurrentTargetId is { } currentTarget && carryIds.Contains(currentTarget);
    }

    private static int ResolveWeakLane(BattleState state, TeamSide attackingSide)
    {
        var enemies = state.GetOpponents(attackingSide).Where(unit => unit.IsAlive).ToList();
        var topHealth = enemies.Where(unit => unit.Anchor.LaneIndex() > 0).Sum(unit => unit.CurrentHealth);
        var bottomHealth = enemies.Where(unit => unit.Anchor.LaneIndex() < 0).Sum(unit => unit.CurrentHealth);
        if (Math.Abs(topHealth - bottomHealth) < 0.01f)
        {
            return 0;
        }

        return topHealth < bottomHealth ? 1 : -1;
    }

    private static float ComputeSeedTieBreaker(int seed, UnitSnapshot actor, UnitSnapshot target)
    {
        unchecked
        {
            var hash = seed;
            hash = (hash * 397) ^ actor.Id.Value.GetHashCode();
            hash = (hash * 397) ^ target.Id.Value.GetHashCode();
            var remainder = Math.Abs(hash % 1000);
            return remainder / 100000f;
        }
    }
}
