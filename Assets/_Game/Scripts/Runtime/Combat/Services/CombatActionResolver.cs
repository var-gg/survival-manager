using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Contracts;

namespace SM.Combat.Services;

public static class CombatActionResolver
{
    private const float ImpactRangeTolerance = 0.12f;
    private const string RangeMissNote = "miss_range";

    /// <summary>SaveMoment detector: 회복 직전 HP 비율이 이 값 미만이면 "구출"로 센다.</summary>
    private const float SaveMomentHealthRatio = 0.25f;

    /// <summary>BacklineDive detector: 다이브 커밋 만료 후에도 이 step 수 안의 후열 킬은 다이브에 귀속.
    /// 커밋 12 step + grace 38 = 다이브 시작 후 ~5초.</summary>
    private const int DiveKillAttributionGraceSteps = 38;

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

                // A basic attack is a COMMITTED swing: once the windup begins (only inside attack range) it
                // runs to completion and always connects on the live target. There is no zero-damage range-miss
                // for basics (that produced the "winds up but whiffs" dead-zone). Skills keep their range-miss.
                var preImpactStep = MovementResolver.TryApplyBasicAttackPreImpactStep(state, actor, target);
                var attackResult = HitResolutionService.ResolveBasicAttack(state, actor, target);
                var attackNote = ComposeNote(attackResult.Note, preImpactStep.NoteToken);
                var postAttackReposition = PostAttackRepositionResult.None;
                actor.GainEnergyFromBasicAttackResolved();
                if (attackResult.Value > 0f)
                {
                    state.RegisterDamage(actor, target);
                    target.TakeDamage(attackResult.Value);
                    ApplyDamageDrain(state, actor, BattleActionType.BasicAttack, null, attackResult.Value);
                    target.GainEnergyFromDirectHitTaken();
                    if (target.IsAlive)
                    {
                        MovementResolver.ApplyKnockback(state, actor, target, attackResult.WasCritical);
                    }
                    BattleTelemetryRecorder.RecordImpact(
                        state,
                        TelemetryEventKind.DamageApplied,
                        actor,
                        target,
                        BattleActionType.BasicAttack,
                        null,
                        attackResult.Value,
                        attackResult.MitigationValue,
                        attackNote);
                    if (target.IsAlive)
                    {
                        postAttackReposition = MovementResolver.TryApplyPostAttackReposition(state, actor, target);
                        if (postAttackReposition.Moved)
                        {
                            attackNote = ComposeNote(attackNote, postAttackReposition.NoteToken);
                        }
                    }
                }

                actor.StartRecovery();
                if (postAttackReposition.Moved)
                {
                    BattleTelemetryRecorder.RecordPostAttackReposition(state, actor, target, postAttackReposition.MovedDistance, postAttackReposition.NoteToken);
                }
                BattleTelemetryRecorder.RecordActionResolved(state, actor, target, BattleActionType.BasicAttack, null, attackResult.Value);
                events.Add(BuildEvent(
                    state,
                    actor,
                    BattleActionType.BasicAttack,
                    BattleLogCode.BasicAttackDamage,
                    target,
                    attackResult.Value,
                    attackResult.MitigationValue,
                    attackNote));
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
                    // cinematic detector(SaveMoment): 빈사 아군을 회복으로 구출한 순간 — 회복 전 HP로 판정.
                    // episode 단위(빈사 1회당 1회) — note/accent/카운터가 같은 판정을 공유한다.
                    var isSaveMoment = target.Side == actor.Side
                                       && target.HealthRatio < SaveMomentHealthRatio
                                       && state.ActivityTelemetry.TryBeginSaveMomentEpisode(target.Id.Value);
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
                    events.Add(BuildEvent(
                        state,
                        actor,
                        BattleActionType.ActiveSkill,
                        BattleLogCode.ActiveSkillHeal,
                        target,
                        heal,
                        note: isSaveMoment ? "save_moment" : ""));
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
                    var shouldRevalidateImpactRange = skill == null || skill.Kind is SkillKind.Strike or SkillKind.Debuff;
                    if (shouldRevalidateImpactRange && IsOutOfImpactRange(actor, target, skill))
                    {
                        actor.StartRecovery(actor.ResolveActionCooldown(skill?.Id));
                        BattleTelemetryRecorder.RecordActionResolved(state, actor, target, BattleActionType.ActiveSkill, skill, 0f);
                        events.Add(BuildEvent(
                            state,
                            actor,
                            BattleActionType.ActiveSkill,
                            BattleLogCode.ActiveSkillDamage,
                            target,
                            0f,
                            note: RangeMissNote));
                        return events;
                    }

                    if (skill != null
                        && skill.AreaEffectFamily != BattleAreaEffectFamily.SingleTarget
                        && skill.AreaRadius > 0f)
                    {
                        ResolveAreaSkillDamage(state, actor, target, skill, events);
                        actor.StartRecovery(actor.ResolveActionCooldown(skill.Id));
                        BattleTelemetryRecorder.RecordActionResolved(
                            state,
                            actor,
                            target,
                            BattleActionType.ActiveSkill,
                            skill,
                            events.Where(evt => evt.ActorId == actor.Id && evt.ActionType == BattleActionType.ActiveSkill).Sum(evt => evt.Value));
                        StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);
                        break;
                    }

                    var skillResult = skill != null
                        ? HitResolutionService.ResolveSkillDamage(state, actor, target, skill)
                        : HitResolutionService.ResolveBasicAttack(state, actor, target);
                    if (skill?.Kind is not (SkillKind.Buff or SkillKind.Utility) && skillResult.Value > 0f)
                    {
                        state.RegisterDamage(actor, target);
                        target.TakeDamage(skillResult.Value);
                        ApplyDamageDrain(state, actor, BattleActionType.ActiveSkill, skill, skillResult.Value);
                        target.GainEnergyFromDirectHitTaken();
                        if (target.IsAlive)
                        {
                            // P3: 저작 강제이동(넉백/끌기)이 있으면 micro-knockback을 대체한다 — 이중 이동 방지.
                            var displaced = skill != null && MovementResolver.TryApplySkillTargetDisplacement(state, actor, target, skill);
                            if (!displaced)
                            {
                                MovementResolver.ApplyKnockback(state, actor, target, skillResult.WasCritical);
                            }
                        }
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

                    // P3 돌진 — contact 시점 follow-through(사거리 의미 불변, 위치 이득은 다음 행동부터).
                    // dodge로 피해가 0이어도 몸은 들어간다.
                    if (skill != null)
                    {
                        MovementResolver.TryApplySkillSelfDash(state, actor, target, skill);
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

    private static void ResolveAreaSkillDamage(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot primaryTarget,
        BattleSkillSpec skill,
        List<BattleEvent> events)
    {
        var selection = EffectMembershipSampler.ResolveAoeSkill(state, actor, primaryTarget, skill);
        var affectedCount = selection.Hits.Count;
        var caughtTargets = new List<UnitSnapshot>();
        foreach (var hit in selection.Hits)
        {
            var target = state.FindUnitById(hit.TargetUnitId);
            if (target == null || !target.IsAlive)
            {
                continue;
            }

            var result = HitResolutionService.ResolveSkillDamage(state, actor, target, skill);
            var punishCluster = skill.PunishCluster && affectedCount >= 3
                ? 1f + Math.Min(0.15f, 0.05f * Math.Max(0, affectedCount - 2))
                : 1f;
            var resolvedValue = result.Value * hit.DamageMultiplier * punishCluster;
            if (resolvedValue > 0f)
            {
                state.RegisterDamage(actor, target);
                target.TakeDamage(resolvedValue);
                ApplyDamageDrain(state, actor, BattleActionType.ActiveSkill, skill, resolvedValue);
                target.GainEnergyFromDirectHitTaken();
                caughtTargets.Add(target);
                if (target.IsAlive)
                {
                    // P3: AoE 강제이동 — 대상별 시전자 기준 축으로 적용(현 AoE 경로는 micro-knockback 없음).
                    MovementResolver.TryApplySkillTargetDisplacement(state, actor, target, skill);
                }
                BattleTelemetryRecorder.RecordImpact(
                    state,
                    TelemetryEventKind.DamageApplied,
                    actor,
                    target,
                    BattleActionType.ActiveSkill,
                    skill,
                    resolvedValue,
                    result.MitigationValue,
                    result.Note,
                    $"{skill.AreaEffectFamily}:{selection.Candidate.CandidateId}:x{hit.DamageMultiplier:0.##}");
            }

            events.Add(BuildEvent(
                state,
                actor,
                BattleActionType.ActiveSkill,
                BattleLogCode.ActiveSkillDamage,
                target,
                resolvedValue,
                result.MitigationValue,
                ComposeNote(result.Note, $"{skill.AreaEffectFamily}:{hit.ChainIndex}")));
        }

        if (caughtTargets.Count >= 3)
        {
            var severity = Math.Clamp((caughtTargets.Count - 2) / 3f, 0f, 1f);
            foreach (var target in caughtTargets)
            {
                if (state.ApplyGroupDispersalLock(target, selection.Candidate.Center, severity, caughtTargets.Count))
                {
                    state.ActivityTelemetry.RecordKnockbackDispersalEvent();
                }
            }
        }

        foreach (var dead in caughtTargets.Where(target => !target.IsAlive).OrderBy(target => target.Id.Value, StringComparer.Ordinal))
        {
            events.AddRange(ResolveKillAndAssist(state, actor, dead, BattleActionType.ActiveSkill, skill));
        }
    }

    private static bool IsOutOfImpactRange(UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec? skill)
    {
        var range = actor.ResolveActionRange(skill?.Id);
        return !MovementResolver.IsInActionRange(actor, target, range + ImpactRangeTolerance);
    }

    private static string ComposeNote(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left}+{right}";
    }

    private static void ApplyDamageDrain(
        BattleState state,
        UnitSnapshot actor,
        BattleActionType actionType,
        BattleSkillSpec? skill,
        float damageValue)
    {
        if (damageValue <= 0f || !actor.IsAlive)
        {
            return;
        }

        var ratio = ResolveDamageDrainRatio(actor, actionType);
        if (ratio <= 0f)
        {
            return;
        }

        var heal = damageValue * ratio;
        actor.Heal(heal);
        BattleTelemetryRecorder.RecordImpact(
            state,
            TelemetryEventKind.HealingApplied,
            actor,
            actor,
            actionType,
            skill,
            heal,
            0f,
            ResolveDamageDrainNote(actor, actionType));
    }

    private static float ResolveDamageDrainRatio(UnitSnapshot actor, BattleActionType actionType)
    {
        var ratio = actor.Omnivamp;
        if (actionType == BattleActionType.BasicAttack)
        {
            ratio += actor.Lifesteal;
        }

        return Math.Clamp(ratio, 0f, 1f);
    }

    private static string ResolveDamageDrainNote(UnitSnapshot actor, BattleActionType actionType)
    {
        var hasLifesteal = actionType == BattleActionType.BasicAttack && actor.Lifesteal > 0f;
        var hasOmnivamp = actor.Omnivamp > 0f;
        return hasLifesteal && hasOmnivamp
            ? "lifesteal+omnivamp"
            : hasLifesteal ? "lifesteal" : "omnivamp";
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

        // cinematic detector(BacklineDive): Dive 의도로 들어간 유닛이 후열을 처치 — "내가 그린 그림"의 1급 순간.
        // 귀속 창: 다이브 커밋(1.2s)은 킬보다 먼저 만료되는 게 보통이라(T1 sweep — 의도 발동 24/24인데
        // 킬 귀속 0), 마지막 다이브 커밋 만료 후 grace 안의 후열 킬은 그 다이브의 결과로 센다.
        var isBacklineDiveKill = target.Behavior.FormationLine == FormationLine.Backline
                                 && (actor.CurrentCombatIntent.Type == CombatIntentType.Dive
                                     || state.StepIndex <= actor.LastDiveCommitUntilStep + DiveKillAttributionGraceSteps);
        if (isBacklineDiveKill)
        {
            state.ActivityTelemetry.RecordBacklineDiveKill();
            killPayload.IsBacklineDiveKill = true;
        }

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
            ComposeNote(
                killPayload.IsMirroredFromOwnedSummon ? "mirrored_kill" : "kill",
                isBacklineDiveKill ? "backline_dive" : ""),
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
