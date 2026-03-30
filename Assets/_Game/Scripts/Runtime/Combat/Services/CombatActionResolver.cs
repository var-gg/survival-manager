using System;
using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class CombatActionResolver
{
    public static IReadOnlyList<BattleEvent> Resolve(BattleState state, UnitSnapshot actor)
    {
        var events = new List<BattleEvent>();
        if (!actor.IsAlive || actor.PendingActionType is null)
        {
            return events;
        }

        var target = state.FindUnit(actor.PendingTargetId);
        var skill = actor.ResolveSkill(actor.PendingSkillId);
        var actionType = actor.PendingActionType.Value;

        actor.FinishWindup();

        switch (actionType)
        {
            case BattleActionType.BasicAttack:
                if (target == null || !target.IsAlive)
                {
                    actor.ClearTarget(applySwitchDelay: true);
                    actor.SetActionState(CombatActionState.SeekTarget);
                    return events;
                }

                var damage = Math.Max(1f, actor.PhysPower - target.Armor - (target.IsDefending ? 1f : 0f));
                target.TakeDamage(damage);
                actor.StartRecovery();
                events.Add(BuildEvent(state, actor, BattleActionType.BasicAttack, target, damage, "basic_attack"));
                if (!target.IsAlive)
                {
                    actor.ClearTarget(applySwitchDelay: true);
                }
                break;

            case BattleActionType.ActiveSkill:
                if (target == null || !target.IsAlive)
                {
                    actor.ClearTarget(applySwitchDelay: true);
                    actor.SetActionState(CombatActionState.SeekTarget);
                    return events;
                }

                if (skill?.Kind == SkillKind.Heal)
                {
                    var heal = Math.Max(1f, actor.HealPower + (skill?.ResolvedPowerFlat ?? 0f));
                    target.Heal(heal);
                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, target, heal, "heal_skill"));
                }
                else
                {
                    var power = skill?.ResolvedPowerFlat ?? 0f;
                    var skillDamage = Math.Max(1f, actor.PhysPower + power - target.Armor - (target.IsDefending ? 1f : 0f));
                    target.TakeDamage(skillDamage);
                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, target, skillDamage, "strike_skill"));
                    if (!target.IsAlive)
                    {
                        actor.ClearTarget(applySwitchDelay: true);
                    }
                }
                break;

            case BattleActionType.WaitDefend:
                actor.SetDefending();
                events.Add(BuildEvent(state, actor, BattleActionType.WaitDefend, actor, 0f, "wait_defend"));
                break;
        }

        return events;
    }

    public static BattleEvent BuildEvent(
        BattleState state,
        UnitSnapshot actor,
        BattleActionType actionType,
        UnitSnapshot? target,
        float value,
        string note)
    {
        return new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            actionType,
            target?.Id,
            target?.Definition.Name,
            value,
            note);
    }
}
