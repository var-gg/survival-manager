using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using SM.Core.Content;
using SM.Core.Contracts;
using SM.Core.Stats;

namespace SM.Combat.Services;

public static class StatusResolutionService
{
    public static void AdvanceStatuses(BattleState state, List<BattleEvent> stepEvents)
    {
        foreach (var unit in state.AllUnits.Where(unit => unit.IsAlive))
        {
            foreach (var status in unit.Statuses
                         .Where(status => state.StatusRules.AppliesPeriodicDamage(status.StatusId))
                         .ToList())
            {
                ApplyPeriodicDamage(state, unit, status, stepEvents);
            }

            var removedStatuses = unit.AdvanceStatusTimers();
            foreach (var status in removedStatuses)
            {
                var source = ResolveStatusSourceUnit(state, unit, status.SourceActorId);
                stepEvents.Add(BuildStatusEvent(state, source, unit, BattleEventKind.StatusRemoved, status.StatusId));
                BattleTelemetryRecorder.RecordStatus(state, TelemetryEventKind.StatusRemoved, source, unit, status.StatusId, 0f);
                if (state.StatusRules.IsHardControl(status.StatusId))
                {
                    var controlRule = state.StatusRules.ControlDiminishing;
                    unit.ApplyControlResistWindow(controlRule.WindowSeconds, controlRule.ControlResistMultiplier);
                    stepEvents.Add(BuildStatusEvent(state, unit, unit, BattleEventKind.ControlResistApplied, status.StatusId, controlRule.ControlResistMultiplier));
                }
            }

            if (unit.IsStunned)
            {
                unit.ClearTarget(applySwitchDelay: false);
                unit.SetActionState(CombatActionState.SeekTarget);
            }
        }
    }

    public static void ApplySkillStatuses(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec? skill, List<BattleEvent> stepEvents)
    {
        if (skill == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(skill.CleanseProfileId))
        {
            ApplyCleanse(state, actor, target, skill, skill.CleanseProfileId, stepEvents);
        }

        foreach (var status in skill.AppliedStatuses ?? Array.Empty<StatusApplicationSpec>())
        {
            ApplyStatus(state, actor, target, skill, status, stepEvents);
        }
    }

    /// <summary>
    /// 스킬이 아닌 formation consequence가 상태 truth와 StatusApplied 이벤트를 우회하지 않고 쓰는 경로.
    /// sourceSkillId는 의도적으로 비워 authored skill attribution과 섞이지 않는다.
    /// </summary>
    internal static void ApplyFormationStatus(
        BattleState state,
        UnitSnapshot source,
        UnitSnapshot target,
        string applicationId,
        string statusId,
        float durationSeconds,
        float magnitude,
        List<BattleEvent> stepEvents)
    {
        ApplyStatus(
            state,
            source,
            target,
            new StatusApplicationSpec(applicationId, statusId, durationSeconds, magnitude),
            sourceSkillId: string.Empty,
            stepEvents: stepEvents);
    }

    public static bool CanUseActiveSkill(UnitSnapshot actor)
    {
        return actor.IsAlive && !actor.IsStunned && !actor.IsSilenced;
    }

    public static bool CanUseSkillSlot(UnitSnapshot actor, BattleSkillSpec skill)
    {
        if (!actor.IsAlive || actor.IsStunned)
        {
            return false;
        }

        if (skill.EffectiveSlotKind is ActionSlotKind.SignatureActive or ActionSlotKind.FlexActive)
        {
            return !actor.IsSilenced;
        }

        return true;
    }

    public static bool CanUseBasicAttack(UnitSnapshot actor)
    {
        return actor.IsAlive && !actor.IsStunned;
    }

    public static bool CanUseMobility(UnitSnapshot actor)
    {
        return actor.IsAlive && !actor.IsStunned && !actor.IsRooted;
    }

    private static void ApplyStatus(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec skill, StatusApplicationSpec spec, List<BattleEvent> stepEvents)
        => ApplyStatus(state, actor, target, spec, skill.Id, stepEvents);

    private static void ApplyStatus(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        StatusApplicationSpec spec,
        string sourceSkillId,
        List<BattleEvent> stepEvents)
    {
        if (string.IsNullOrWhiteSpace(spec.StatusId))
        {
            return;
        }

        // Move 3 표식 방패: 현재 barrier가 남아 있으면 TacticalMark 그룹 전체(marked/exposed 및
        // 파생 family)가 상태 적용 truth에서 튕긴다. family id 리터럴 대신 저작 group을 소비한다.
        if (target.Barrier > 0f
            && state.StatusRules.TryGetStatusFamily(spec.StatusId, out var statusRule)
            && statusRule.Group == StatusGroupValue.TacticalMark)
        {
            stepEvents.Add(BuildStatusEvent(
                state,
                actor,
                target,
                BattleEventKind.StatusResisted,
                spec.StatusId,
                target.Barrier));
            return;
        }

        if (state.StatusRules.IsHardControl(spec.StatusId) && target.IsUnstoppable)
        {
            return;
        }

        var adjustedDuration = AdjustDurationForTenacity(state, target, spec.StatusId, spec.DurationSeconds);
        if (target.ControlResistWindow is { } resistWindow && state.StatusRules.IsHardControl(spec.StatusId))
        {
            adjustedDuration *= Math.Max(0.1f, 1f - resistWindow.ResistMultiplier);
        }

        var adjusted = spec with { DurationSeconds = adjustedDuration };
        // 즉시 보호막 전환(상태 미잔존)은 콘텐츠 효과 종류 서술자(GrantsBarrierOnApply) — 과거
        // StatusId=="barrier" 문자열 분기의 승격(효과 종류 데이터화 3보 1슬라이스). 바닥 1은 코드
        // 소유 클램프. 나머지 효과 종류(행동차단/침묵/표식 등)의 데이터화는 3b~3g 페이즈 범위.
        if (state.StatusRules.ResolveGrantsBarrierOnApply(spec.StatusId))
        {
            target.AddBarrier(Math.Max(1f, spec.Magnitude));
        }
        else
        {
            target.ApplyStatus(adjusted, actor.Id.Value, sourceSkillId, spec.Id);
        }

        stepEvents.Add(BuildStatusEvent(state, actor, target, BattleEventKind.StatusApplied, spec.StatusId, spec.Magnitude));
        BattleTelemetryRecorder.RecordStatus(state, TelemetryEventKind.StatusApplied, actor, target, spec.StatusId, spec.Magnitude);
    }

    private static void ApplyCleanse(BattleState state, UnitSnapshot actor, UnitSnapshot target, BattleSkillSpec skill, string cleanseProfileId, List<BattleEvent> stepEvents)
    {
        if (!state.StatusRules.TryGetCleanseProfile(cleanseProfileId, out var cleanseRule))
        {
            return;
        }

        var removed = target.RemoveStatuses(cleanseRule.RemovesStatusIds);
        if (cleanseRule.RemovesOneHardControl)
        {
            var hardControl = target.Statuses
                .Select(status => status.StatusId)
                .FirstOrDefault(state.StatusRules.IsHardControl);
            if (!string.IsNullOrWhiteSpace(hardControl) && target.RemoveStatus(hardControl))
            {
                removed++;
            }
        }

        if (cleanseRule.GrantsUnstoppable)
        {
            // 부여 상태 id는 프로필 데이터 소유(GrantedStatusId, 기본 "unstoppable") — 과거 리터럴의 승격.
            // 바닥 0.1s는 코드 소유 클램프.
            target.ApplyStatus(new StatusApplicationSpec(
                $"status.{cleanseRule.Id}.{cleanseRule.GrantedStatusId}",
                cleanseRule.GrantedStatusId,
                Math.Max(0.1f, cleanseRule.GrantedUnstoppableDurationSeconds),
                0f),
                actor.Id.Value,
                skill.Id,
                cleanseRule.Id);
        }

        if (removed > 0 || cleanseRule.GrantsUnstoppable)
        {
            stepEvents.Add(BuildStatusEvent(state, actor, target, BattleEventKind.CleanseTriggered, cleanseProfileId, removed));
            if (cleanseRule.GrantsUnstoppable)
            {
                var controlRule = state.StatusRules.ControlDiminishing;
                target.ApplyControlResistWindow(controlRule.WindowSeconds, controlRule.ControlResistMultiplier);
                stepEvents.Add(BuildStatusEvent(state, actor, target, BattleEventKind.ControlResistApplied, cleanseRule.GrantedStatusId, controlRule.ControlResistMultiplier));
                BattleTelemetryRecorder.RecordStatus(state, TelemetryEventKind.StatusApplied, actor, target, cleanseRule.GrantedStatusId, controlRule.ControlResistMultiplier);
            }
        }
    }

    private static void ApplyPeriodicDamage(BattleState state, UnitSnapshot unit, AppliedStatusState status, List<BattleEvent> stepEvents)
    {
        if (!unit.HasStatus(status.StatusId))
        {
            return;
        }

        var source = ResolveStatusSourceUnit(state, unit, status.SourceActorId);
        // 주기 피해(burn/bleed)도 magnitude × 배율(콘텐츠 튜닝값, 기본 1)이 틱 피해량 — 숫자 콘텐츠화 2보.
        // 바닥 1은 코드 소유 클램프(바닥값 리터럴 백로그와 동일 축).
        var damage = Math.Max(1f, status.Magnitude * state.StatusRules.ResolveMagnitudeScale(status.StatusId));
        unit.TakeDamage(damage);
        BattleTelemetryRecorder.RecordStatusTick(state, source, unit, status.StatusId, damage);
        stepEvents.Add(new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            source.Id,
            source.Definition.Name,
            BattleActionType.ActiveSkill,
            BattleLogCode.Generic,
            unit.Id,
            unit.Definition.Name,
            damage,
            BattleEventKind.Action,
            status.StatusId,
            0f,
            "status_tick"));
        if (!unit.IsAlive)
        {
            stepEvents.AddRange(CombatActionResolver.ResolveKillAndAssist(
                state,
                source,
                unit,
                BattleActionType.ActiveSkill,
                skill: null));
        }
    }

    private static UnitSnapshot ResolveStatusSourceUnit(BattleState state, UnitSnapshot fallback, string sourceActorId)
    {
        if (string.IsNullOrWhiteSpace(sourceActorId))
        {
            return fallback;
        }

        return state.AllUnits.FirstOrDefault(unit => string.Equals(unit.Id.Value, sourceActorId, StringComparison.Ordinal)) ?? fallback;
    }

    private static float AdjustDurationForTenacity(BattleState state, UnitSnapshot target, string statusId, float durationSeconds)
    {
        var tenacity = Math.Max(0f, target.Stats.Get(StatKey.Tenacity));
        var tenacityScale = state.StatusRules.ResolveTenacityScale(statusId);
        return tenacityScale <= 0f
            ? durationSeconds
            : Math.Max(0.1f, durationSeconds * Math.Max(0.1f, 1f - (tenacity * tenacityScale)));
    }

    private static BattleEvent BuildStatusEvent(
        BattleState state,
        UnitSnapshot actor,
        UnitSnapshot target,
        BattleEventKind kind,
        string payloadId,
        float value = 0f)
    {
        return new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            BattleActionType.ActiveSkill,
            BattleLogCode.Generic,
            target.Id,
            target.Definition.Name,
            value,
            kind,
            payloadId);
    }
}
