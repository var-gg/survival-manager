using System;
using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Combat.Services;

/// <summary>
/// Phase 3 콤보 발현 — "세팅 디버프(primer) → 윈도우 안의 후속 타격(consume)" 연쇄를 결정론적으로
/// 인식해 <see cref="CombatBeatType.ComboPrimerApplied"/>/<see cref="CombatBeatType.ComboConsumed"/>
/// beat(같은 ChainId)을 남기고, 소비 타격에 프라이머 family별 별도 추가타를 적용한다.
/// 이미 일어난 사실(<see cref="BattleEvent"/>의 status 적용·피해)은 step 이벤트 순서 그대로 읽는다.
/// 윈도우(1.2s)/ICD(2.5s)/프라이머 레지스트리는 마스터 플랜 기준값으로 시작하는 V1 authority 노브.
/// </summary>
public static class CombatComboService
{
    /// <summary>프라이머 유효 + 소비 가능 윈도우 — 적용 step 부터 1.2s(12 tick).</summary>
    public const int PrimerWindowTicks = 12;

    /// <summary>연쇄 ICD — 같은 (프라이머 소스, status) 콤보는 소비 후 2.5s(25 tick) 동안 재발동 금지.</summary>
    public const int ChainIcdTicks = 25;

    // 콤보를 여는 "세팅" 디버프 — 제어(stun/root/slow)와 취약 증폭(sunder/marked/exposed).
    // DoT(burn/bleed)는 제외: 지속 피해 + 아무 타격 = 연쇄로 읽으면 콤보 의미가 희석된다.
    private static readonly HashSet<string> PrimerStatusIds = new(StringComparer.Ordinal)
    {
        "stun", "root", "slow", "sunder", "marked", "exposed",
    };

    public static bool IsPrimerStatus(string statusId) => PrimerStatusIds.Contains(statusId);

    /// <summary>
    /// step 말미에 1회 — 이번 step 의 이벤트를 기록 순서대로 읽어 프라이머 개시/소비를 판정한다.
    /// 이벤트 순서가 곧 인과 순서라, 같은 step 안에서도 "프라이머 이후의 타격"만 소비할 수 있다.
    /// </summary>
    public static void ProcessStep(BattleState state, List<BattleEvent> stepEvents)
    {
        state.ComboLedger.PruneExpired(state.StepIndex);

        // payoff/kill 이벤트를 같은 리스트에 append하므로 원본 사실만 스냅샷 길이로 순회한다.
        // 새 ComboPayoffDamage가 다시 콤보를 소비하거나 컬렉션 순회가 깨지는 것을 동시에 막는다.
        var sourceEventCount = stepEvents.Count;
        for (var index = 0; index < sourceEventCount; index++)
        {
            var stepEvent = stepEvents[index];
            if (stepEvent.EventKind == BattleEventKind.StatusApplied)
            {
                TryOpenPrimer(state, stepEvent);
            }
            else if (IsDamageContact(stepEvent))
            {
                TryConsumePrimer(state, stepEvent, stepEvents);
            }
        }
    }

    private static bool IsDamageContact(BattleEvent stepEvent)
    {
        return stepEvent.Value > 0f
               && stepEvent.LogCode is BattleLogCode.BasicAttackDamage or BattleLogCode.ActiveSkillDamage;
    }

    private static void TryOpenPrimer(BattleState state, BattleEvent stepEvent)
    {
        if (!PrimerStatusIds.Contains(stepEvent.PayloadId))
        {
            return;
        }

        var source = state.FindUnit(stepEvent.ActorId);
        var target = state.FindUnit(stepEvent.TargetId);
        if (source == null || target == null || source.Side == target.Side)
        {
            return;
        }

        // 활성 윈도우 중 재적용(DoT 갱신 등)은 기존 연쇄를 유지한다 — 프라이머 beat 스팸 방지.
        if (state.ComboLedger.FindActivePrimer(target.Id, stepEvent.PayloadId, state.StepIndex) != null)
        {
            return;
        }

        var chainId = state.ComboLedger.AllocateChainId();
        state.ComboLedger.AddPrimer(new ComboPrimerWindow(
            chainId,
            source.Id,
            source.Side,
            target.Id,
            stepEvent.PayloadId,
            state.StepIndex,
            state.StepIndex + PrimerWindowTicks));
        state.RecordBeat(
            CombatBeatType.ComboPrimerApplied,
            source.Side,
            source.Id,
            target.Id,
            chainId,
            CombatBeatImportance.ComboPrimerApplied,
            stepEvent.Value,
            target.Position,
            stepEvent.PayloadId);
    }

    private static void TryConsumePrimer(BattleState state, BattleEvent stepEvent, List<BattleEvent> stepEvents)
    {
        var attacker = state.FindUnit(stepEvent.ActorId);
        var victim = state.FindUnit(stepEvent.TargetId);
        if (attacker == null || victim == null || attacker.Side == victim.Side)
        {
            return;
        }

        var primer = state.ComboLedger.FindOldestActivePrimer(victim.Id, attacker.Side, state.StepIndex);
        if (primer == null)
        {
            return;
        }

        // 프라이머를 건 그 액션의 자기 타격이 같은 step 에 즉시 소비하는 것은 연쇄가 아니다 —
        // 콤보는 "세팅 + 후속"이어야 한다. (같은 step 이라도 다른 유닛의 타격은 정당한 소비.)
        if (primer.AppliedStep == state.StepIndex && primer.SourceId == attacker.Id)
        {
            return;
        }

        var chainKey = $"{primer.SourceId.Value}:{primer.StatusId}";
        if (state.ComboLedger.IsChainOnCooldown(chainKey, state.StepIndex, ChainIcdTicks))
        {
            return;
        }

        state.ComboLedger.RemovePrimer(primer);
        state.ComboLedger.MarkChainConsumed(chainKey, state.StepIndex);

        var payoffDamage = 0f;
        if (victim.IsAlive)
        {
            payoffDamage = stepEvent.Value * state.StatusRules.ResolveComboPayoffBonus(primer.StatusId);
            if (state.TeamRuleSet.Has(attacker.Side, TeamRuleSet.ExecuteRuleId))
            {
                // class@3 execute는 기존 family payoff 공식을 바꾸지 않고, 해당 팀의 최종 payoff에만
                // 조건부 배율을 얹는다. 규칙이 없으면 이 분기를 타지 않아 기존 float 연산도 그대로다.
                payoffDamage *= 1f + TeamRuleSet.ExecuteComboPayoffMultiplierBonus;
            }

            if (payoffDamage > 0f)
            {
                state.RegisterDamage(attacker, victim);
                victim.TakeDamage(payoffDamage);
                BattleTelemetryRecorder.RecordImpact(
                    state,
                    TelemetryEventKind.DamageApplied,
                    attacker,
                    victim,
                    stepEvent.ActionType,
                    null,
                    payoffDamage,
                    stringValueA: $"combo_payoff:{primer.StatusId}");
                stepEvents.Add(CombatActionResolver.BuildEvent(
                    state,
                    attacker,
                    stepEvent.ActionType,
                    BattleLogCode.ComboPayoffDamage,
                    victim,
                    payoffDamage,
                    note: $"combo_payoff:{primer.StatusId}"));

                if (!victim.IsAlive)
                {
                    stepEvents.AddRange(CombatActionResolver.ResolveKillAndAssist(
                        state,
                        attacker,
                        victim,
                        stepEvent.ActionType,
                        null));
                    attacker.ClearTarget(applySwitchDelay: true);
                }
            }
        }

        state.RecordBeat(
            CombatBeatType.ComboConsumed,
            attacker.Side,
            attacker.Id,
            victim.Id,
            primer.ChainId,
            CombatBeatImportance.ComboConsumed,
            payoffDamage,
            victim.Position,
            primer.StatusId);
    }
}
