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

                var damage = Math.Max(1f, (actor.PhysPower - target.Armor - (target.IsGuarded ? 1f : 0f)) * target.GetIncomingDamageMultiplier());
                target.TakeDamage(damage);
                actor.StartRecovery();
                events.Add(BuildEvent(state, actor, BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, target, damage));
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
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal, target, heal));
                    StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                }
                else if (skill?.Kind == SkillKind.Shield)
                {
                    var barrier = Math.Max(1f, actor.HealPower + (skill?.ResolvedPowerFlat ?? 0f));
                    target.AddBarrier(barrier);
                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal, target, barrier));
                    StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                }
                else
                {
                    var power = skill?.ResolvedPowerFlat ?? 0f;
                    var basePower = skill?.DamageType == DamageType.Magical
                        ? actor.MagPower + power
                        : actor.PhysPower + power;
                    var mitigation = skill?.DamageType == DamageType.Magical ? target.Resist : target.Armor;
                    var skillDamage = Math.Max(1f, (basePower - mitigation - (target.IsGuarded ? 1f : 0f)) * target.GetIncomingDamageMultiplier());
                    if (skill?.Kind is not (SkillKind.Buff or SkillKind.Utility))
                    {
                        target.TakeDamage(skillDamage);
                    }

                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillDamage, target, skillDamage));
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
        float value)
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
            value);
    }
}
