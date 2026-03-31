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
                    actor.SetActionState(CombatActionState.AcquireTarget);
                    return events;
                }

                var attackResult = HitResolutionService.ResolveBasicAttack(state, actor, target);
                if (attackResult.Value > 0f)
                {
                    target.TakeDamage(attackResult.Value);
                }

                actor.StartRecovery();
                events.Add(BuildEvent(
                    state,
                    actor,
                    BattleActionType.BasicAttack,
                    BattleLogCode.BasicAttackDamage,
                    target,
                    attackResult.Value,
                    attackResult.MitigationValue,
                    attackResult.Note));
                if (!target.IsAlive)
                {
                    actor.ClearTarget(applySwitchDelay: true);
                }
                break;

            case BattleActionType.ActiveSkill:
                if (target == null || !target.IsAlive)
                {
                    actor.ClearTarget(applySwitchDelay: true);
                    actor.SetActionState(CombatActionState.AcquireTarget);
                    return events;
                }

                if (skill?.Kind == SkillKind.Heal)
                {
                    var heal = HitResolutionService.ResolveSupportValue(actor, skill);
                    target.Heal(heal);
                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal, target, heal));
                    StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                }
                else if (skill?.Kind == SkillKind.Shield)
                {
                    var barrier = HitResolutionService.ResolveSupportValue(actor, skill);
                    target.AddBarrier(barrier);
                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal, target, barrier));
                    StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                }
                else
                {
                    var skillResult = skill != null
                        ? HitResolutionService.ResolveSkillDamage(state, actor, target, skill)
                        : HitResolutionService.ResolveBasicAttack(state, actor, target);
                    if (skill?.Kind is not (SkillKind.Buff or SkillKind.Utility) && skillResult.Value > 0f)
                    {
                        target.TakeDamage(skillResult.Value);
                    }

                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(
                        state,
                        actor,
                        BattleActionType.ActiveSkill,
                        BattleLogCode.ActiveSkillDamage,
                        target,
                        skillResult.Value,
                        skillResult.MitigationValue,
                        skillResult.Note));
                    StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                    if (!target.IsAlive)
                    {
                        actor.ClearTarget(applySwitchDelay: true);
                    }
                }
                break;

            case BattleActionType.WaitDefend:
                actor.SetDefending();
                events.Add(BuildEvent(state, actor, BattleActionType.WaitDefend, BattleLogCode.WaitDefend, actor, 0f));
                break;
        }

        return events;
    }

    public static BattleEvent BuildEvent(
        BattleState state,
        UnitSnapshot actor,
        BattleActionType actionType,
        BattleLogCode logCode,
        UnitSnapshot? target,
        float value,
        float secondaryValue = 0f,
        string note = "")
    {
        return new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            actionType,
            logCode,
            target?.Id,
            target?.Definition.Name,
            value,
            BattleEventKind.Action,
            string.Empty,
            secondaryValue,
            note);
    }
}
