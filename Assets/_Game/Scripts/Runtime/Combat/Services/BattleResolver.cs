using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class BattleResolver
{
    public static BattleResult Run(BattleState state, int maxTicks = 50)
    {
        var events = new List<BattleEvent>();

        while (state.LivingAllies.Any() && state.LivingEnemies.Any() && state.Tick < maxTicks)
        {
            var actingOrder = state.AllUnits.Where(x => x.IsAlive).OrderByDescending(x => x.Speed).ThenBy(x => x.Side).ToList();

            foreach (var actor in actingOrder)
            {
                if (!actor.IsAlive)
                {
                    continue;
                }

                actor.ResetTurnState();
                var evaluated = TacticEvaluator.Evaluate(state, actor);
                ResolveAction(state, actor, evaluated, events);

                if (!state.LivingAllies.Any() || !state.LivingEnemies.Any())
                {
                    break;
                }
            }

            state.AdvanceTick();
        }

        var winner = state.LivingAllies.Any() ? TeamSide.Ally : TeamSide.Enemy;
        return new BattleResult(winner, state.Tick, events);
    }

    private static void ResolveAction(BattleState state, UnitSnapshot actor, EvaluatedAction evaluated, List<BattleEvent> events)
    {
        switch (evaluated.ActionType)
        {
            case BattleActionType.BasicAttack:
                if (evaluated.Target is null) return;
                var damage = Math.Max(1f, actor.Attack - evaluated.Target.Defense - (evaluated.Target.IsDefending ? 1f : 0f));
                evaluated.Target.TakeDamage(damage);
                events.Add(new BattleEvent(state.Tick, actor.Id, BattleActionType.BasicAttack, evaluated.Target.Id, damage, "basic_attack"));
                break;
            case BattleActionType.ActiveSkill:
                if (evaluated.Target is null || evaluated.Skill is null) return;
                if (evaluated.Skill.Kind == SkillKind.Heal)
                {
                    var heal = Math.Max(1f, actor.HealPower + evaluated.Skill.Power);
                    evaluated.Target.Heal(heal);
                    events.Add(new BattleEvent(state.Tick, actor.Id, BattleActionType.ActiveSkill, evaluated.Target.Id, heal, "heal_skill"));
                }
                else
                {
                    var skillDamage = Math.Max(1f, actor.Attack + evaluated.Skill.Power - evaluated.Target.Defense - (evaluated.Target.IsDefending ? 1f : 0f));
                    evaluated.Target.TakeDamage(skillDamage);
                    events.Add(new BattleEvent(state.Tick, actor.Id, BattleActionType.ActiveSkill, evaluated.Target.Id, skillDamage, "strike_skill"));
                }
                break;
            case BattleActionType.WaitDefend:
                actor.SetDefending();
                events.Add(new BattleEvent(state.Tick, actor.Id, BattleActionType.WaitDefend, actor.Id, 0f, "wait_defend"));
                break;
        }
    }
}
