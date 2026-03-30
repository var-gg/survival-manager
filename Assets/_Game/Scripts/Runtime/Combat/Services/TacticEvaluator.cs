using System;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public sealed record EvaluatedAction(BattleActionType ActionType, UnitSnapshot? Target, BattleSkillSpec? Skill, TacticRule Rule, float DesiredRange);

public static class TacticEvaluator
{
    public static EvaluatedAction Evaluate(BattleState state, UnitSnapshot actor)
    {
        var ordered = actor.Definition.Tactics.OrderBy(x => x.Priority);
        foreach (var rule in ordered)
        {
            var skill = rule.ActionType == BattleActionType.ActiveSkill
                ? actor.ResolveSkill(rule.SkillId)
                : null;
            var target = TargetScoringService.SelectTarget(state, actor, rule.TargetSelector, rule.ActionType, rule.SkillId);
            if (!ConditionMatches(state, actor, rule, skill, target))
            {
                continue;
            }

            var desiredRange = skill?.Range ?? actor.AttackRange;
            return new EvaluatedAction(rule.ActionType, target, skill, rule, desiredRange);
        }

        var fallbackRule = new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self, null);
        return new EvaluatedAction(BattleActionType.WaitDefend, actor, null, fallbackRule, 0f);
    }

    private static bool ConditionMatches(BattleState state, UnitSnapshot actor, TacticRule rule, BattleSkillSpec? skill, UnitSnapshot? target)
    {
        switch (rule.ConditionType)
        {
            case TacticConditionType.SelfHpBelow:
                return actor.HealthRatio <= rule.Threshold && target is not null;
            case TacticConditionType.AllyHpBelow:
                return state.GetTeam(actor.Side).Where(x => x.IsAlive).Any(x => x.HealthRatio <= rule.Threshold) && target is not null;
            case TacticConditionType.EnemyInRange:
                return target is not null
                       && target.Side != actor.Side
                       && MovementResolver.IsInActionRange(actor, target, skill?.Range ?? actor.AttackRange);
            case TacticConditionType.LowestHpEnemy:
                return target is not null;
            case TacticConditionType.EnemyExposed:
                return target is not null
                       && target.Side != actor.Side
                       && TargetScoringService.ComputeExposureScore(state, target) >= MathF.Max(1f, rule.Threshold);
            case TacticConditionType.Fallback:
                return target is not null;
            default:
                return false;
        }
    }
}
