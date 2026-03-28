using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public sealed record EvaluatedAction(BattleActionType ActionType, UnitSnapshot? Target, SkillDefinition? Skill, TacticRule Rule);

public static class TacticEvaluator
{
    public static EvaluatedAction Evaluate(BattleState state, UnitSnapshot actor)
    {
        var ordered = actor.Definition.Tactics.OrderBy(x => x.Priority);
        foreach (var rule in ordered)
        {
            if (!ConditionMatches(state, actor, rule, out var target))
            {
                continue;
            }

            var skill = rule.SkillId is null ? null : actor.Definition.Skills.FirstOrDefault(x => x.Id == rule.SkillId);
            return new EvaluatedAction(rule.ActionType, target, skill, rule);
        }

        var fallbackRule = new TacticRule(999, TacticConditionType.Fallback, 0f, BattleActionType.WaitDefend, TargetSelectorType.Self, null);
        return new EvaluatedAction(BattleActionType.WaitDefend, actor, null, fallbackRule);
    }

    private static bool ConditionMatches(BattleState state, UnitSnapshot actor, TacticRule rule, out UnitSnapshot? target)
    {
        target = SelectTarget(state, actor, rule.TargetSelector, rule.ActionType == BattleActionType.ActiveSkill ? rule.SkillId : null);

        switch (rule.ConditionType)
        {
            case TacticConditionType.SelfHpBelow:
                return actor.HealthRatio <= rule.Threshold && target is not null;
            case TacticConditionType.AllyHpBelow:
                return state.GetTeam(actor.Side).Where(x => x.IsAlive).Any(x => x.HealthRatio <= rule.Threshold) && target is not null;
            case TacticConditionType.EnemyInRange:
                return SelectEnemyInRange(state, actor, rule.ActionType == BattleActionType.ActiveSkill ? rule.SkillId : null) is not null && target is not null;
            case TacticConditionType.LowestHpEnemy:
                return SelectLowestHpEnemy(state, actor, rule.ActionType == BattleActionType.ActiveSkill ? rule.SkillId : null) is not null && target is not null;
            case TacticConditionType.Fallback:
                return target is not null;
            default:
                return false;
        }
    }

    private static UnitSnapshot? SelectTarget(BattleState state, UnitSnapshot actor, TargetSelectorType selector, string? skillId)
    {
        return selector switch
        {
            TargetSelectorType.Self => actor,
            TargetSelectorType.LowestHpAlly => state.GetTeam(actor.Side).Where(x => x.IsAlive).OrderBy(x => x.HealthRatio).FirstOrDefault(),
            TargetSelectorType.FirstEnemyInRange => SelectEnemyInRange(state, actor, skillId),
            TargetSelectorType.LowestHpEnemy => SelectLowestHpEnemy(state, actor, skillId),
            _ => null,
        };
    }

    private static UnitSnapshot? SelectEnemyInRange(BattleState state, UnitSnapshot actor, string? skillId)
    {
        var range = ResolveRange(actor, skillId);
        var front = state.GetOpponents(actor.Side).Where(x => x.IsAlive && x.Definition.Row == RowPosition.Front).ToList();
        var back = state.GetOpponents(actor.Side).Where(x => x.IsAlive && x.Definition.Row == RowPosition.Back).ToList();

        if (range <= 1)
        {
            return front.FirstOrDefault();
        }

        return front.FirstOrDefault() ?? back.FirstOrDefault();
    }

    private static UnitSnapshot? SelectLowestHpEnemy(BattleState state, UnitSnapshot actor, string? skillId)
    {
        var range = ResolveRange(actor, skillId);
        var candidates = state.GetOpponents(actor.Side).Where(x => x.IsAlive).ToList();
        if (range <= 1)
        {
            candidates = candidates.Where(x => x.Definition.Row == RowPosition.Front).ToList();
        }

        return candidates.OrderBy(x => x.HealthRatio).FirstOrDefault();
    }

    private static int ResolveRange(UnitSnapshot actor, string? skillId)
    {
        if (skillId is null)
        {
            return actor.Definition.Row == RowPosition.Back ? 2 : 1;
        }

        var skill = actor.Definition.Skills.FirstOrDefault(x => x.Id == skillId);
        return skill?.Range ?? 1;
    }
}
