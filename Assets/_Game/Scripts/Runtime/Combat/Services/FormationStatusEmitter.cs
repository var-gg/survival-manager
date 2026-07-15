using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.Combat.Services;

/// <summary>
/// HitResolutionService가 이미 확정한 진형 consequence를 exposed 상태 경제로 방출한다.
/// 위치 판정을 다시 계산하지 않으며, 상태 적용과 StatusApplied 이벤트 생성은 StatusResolutionService에 맡긴다.
/// </summary>
internal static class FormationStatusEmitter
{
    internal const float RiposteExposedDurationSeconds = 1.2f;
    internal const float PhalanxRiposteDurationBonusSeconds = 0.6f;
    internal const float FlankExposedDurationSeconds = 0.8f;
    internal const int InternalCooldownTicks = 25; // 2.5s @ 0.1s fixed tick

    // V1 placeholder: exposed의 AmplifiesIncomingDamage 채널에 +12%를 공급한다. duration/ICD와 함께 sweep 재비준 대상.
    internal const float ExposedMagnitude = 0.12f;

    private const string ExposedStatusId = "exposed";
    private const string RiposteApplicationId = "formation.riposte.exposed";
    private const string FlankApplicationId = "formation.flank.exposed";

    internal static void EmitForResolvedHit(
        BattleState state,
        UnitSnapshot attacker,
        UnitSnapshot victim,
        HitResolutionResult hit,
        List<BattleEvent> stepEvents)
    {
        if (hit.WasDodged || hit.Value <= 0f)
        {
            return;
        }

        // 고정 방출 순서: riposte -> flank. 둘 다 성립하는 hit도 byte-identical event order를 갖는다.
        // 수령자 생존 체크는 ICD claim보다 먼저 — 시체에 exposed(+허위 StatusApplied/콤보 프라이머)를
        // 얹지 않고, 죽은 수령자가 ICD를 헛소모하지도 않게 한다(치명 후방타 흔한 경로).
        if (!string.IsNullOrEmpty(hit.ScreeningUnitId)
            && attacker.IsAlive
            && state.FindUnitById(hit.ScreeningUnitId) is { IsAlive: true } screener
            && state.FormationStatusLedger.TryClaimRiposte(hit.ScreeningUnitId, state.StepIndex, InternalCooldownTicks))
        {
            StatusResolutionService.ApplyFormationStatus(
                state,
                screener,
                attacker,
                RiposteApplicationId,
                ExposedStatusId,
                RiposteExposedDurationSeconds
                + (state.TeamRuleSet.Has(screener.Side, TeamRuleSet.PhalanxRuleId)
                    ? PhalanxRiposteDurationBonusSeconds
                    : 0f),
                ExposedMagnitude,
                stepEvents);
        }

        if (hit.WasRearFlank
            && victim.IsAlive
            && state.FormationStatusLedger.TryClaimFlank(victim.Id.Value, state.StepIndex, InternalCooldownTicks))
        {
            StatusResolutionService.ApplyFormationStatus(
                state,
                attacker,
                victim,
                FlankApplicationId,
                ExposedStatusId,
                FlankExposedDurationSeconds,
                ExposedMagnitude,
                stepEvents);
        }
    }
}
