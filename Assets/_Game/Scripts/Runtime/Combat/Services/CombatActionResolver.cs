using System;
using System.Collections.Generic;
using SM.Combat.Model;
using SM.Core.Contracts;

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
                actor.GainEnergyFromBasicAttackResolved();
                if (attackResult.Value > 0f)
                {
                    state.RegisterDamage(actor, target);
                    target.TakeDamage(attackResult.Value);
                    target.GainEnergyFromDirectHitTaken();
                    BattleTelemetryRecorder.RecordImpact(
                        state,
                        TelemetryEventKind.DamageApplied,
                        actor,
                        target,
                        BattleActionType.BasicAttack,
                        null,
                        attackResult.Value,
                        attackResult.MitigationValue,
                        attackResult.Note);
                }

                actor.StartRecovery();
                BattleTelemetryRecorder.RecordActionResolved(state, actor, target, BattleActionType.BasicAttack, null, attackResult.Value);
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
                    events.AddRange(ResolveKillAndAssist(state, actor, target, BattleActionType.BasicAttack, null));
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
                    BattleTelemetryRecorder.RecordActionResolved(state, actor, target, BattleActionType.ActiveSkill, skill, heal);
                    BattleTelemetryRecorder.RecordImpact(
                        state,
                        TelemetryEventKind.HealingApplied,
                        actor,
                        target,
                        BattleActionType.ActiveSkill,
                        skill,
                        heal);
                    events.Add(BuildEvent(state, actor, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal, target, heal));
                    StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                }
                else if (skill?.Kind == SkillKind.Shield)
                {
                    var barrier = HitResolutionService.ResolveSupportValue(actor, skill);
                    target.AddBarrier(barrier);
                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    BattleTelemetryRecorder.RecordActionResolved(state, actor, target, BattleActionType.ActiveSkill, skill, barrier);
                    BattleTelemetryRecorder.RecordImpact(
                        state,
                        TelemetryEventKind.BarrierApplied,
                        actor,
                        target,
                        BattleActionType.ActiveSkill,
                        skill,
                        barrier);
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
                        state.RegisterDamage(actor, target);
                        target.TakeDamage(skillResult.Value);
                        target.GainEnergyFromDirectHitTaken();
                        BattleTelemetryRecorder.RecordImpact(
                            state,
                            TelemetryEventKind.DamageApplied,
                            actor,
                            target,
                            BattleActionType.ActiveSkill,
                            skill,
                            skillResult.Value,
                            skillResult.MitigationValue,
                            skillResult.Note);
                    }

                    actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                    BattleTelemetryRecorder.RecordActionResolved(state, actor, target, BattleActionType.ActiveSkill, skill, skillResult.Value);
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
                        events.AddRange(ResolveKillAndAssist(state, actor, target, BattleActionType.ActiveSkill, skill));
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

    internal static BattleEvent BuildEvent(
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

    private static IReadOnlyList<BattleEvent> ResolveKillAndAssist(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleActionType actionType, BattleSkillSpec? skill)
    {
        var events = new List<BattleEvent>();
        var killPayload = BuildKillPayload(actor, target);

        actor.GainEnergyFromKill();

        if (killPayload.IsMirroredFromOwnedSummon && killPayload.MirroredOwner != default)
        {
            var owner = state.FindUnit(killPayload.MirroredOwner);
            if (killPayload.GrantsOwnerEnergy)
            {
                owner?.GainEnergyFromKill();
            }
        }

        foreach (var assister in state.ConsumeAssistContributors(target.Id, actor.Id))
        {
            assister.GainEnergyFromAssist();
        }

        events.Add(new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            actionType,
            BattleLogCode.Generic,
            target.Id,
            target.Definition.Name,
            0f,
            BattleEventKind.Kill,
            string.Empty,
            0f,
            killPayload.IsMirroredFromOwnedSummon ? "mirrored_kill" : "kill",
            killPayload));
        BattleTelemetryRecorder.RecordKill(state, actor, target, actionType, skill, killPayload);

        return events;
    }

    private static KillEventPayload BuildKillPayload(UnitSnapshot actor, UnitSnapshot target)
    {
        var payload = new KillEventPayload
        {
            ActualKiller = actor.Id,
            ActualVictim = target.Id,
            MirroredOwner = default,
            IsMirroredFromOwnedSummon = false,
            GrantsOwnerEnergy = false,
            GrantsOwnerOnKillTriggers = false,
        };

        if (actor.EntityKind is CombatEntityKind.OwnedSummon or CombatEntityKind.Deployable
            && actor.Ownership is { } ownership
            && actor.SummonProfile != null
            && actor.SummonProfile.CreditPolicy.HasFlag(CombatCreditFlags.EligibleForMirroredOwnerKill))
        {
            payload.MirroredOwner = ownership.OwnerEntity;
            payload.IsMirroredFromOwnedSummon = true;
            payload.GrantsOwnerEnergy = actor.SummonProfile.CreditPolicy.HasFlag(CombatCreditFlags.EligibleForOwnerEnergyGain);
            payload.GrantsOwnerOnKillTriggers = actor.SummonProfile.CreditPolicy.HasFlag(CombatCreditFlags.EligibleForOwnerOnKillTriggers);
        }

        return payload;
    }
}
