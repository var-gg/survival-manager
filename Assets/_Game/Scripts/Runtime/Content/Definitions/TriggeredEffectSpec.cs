using System;
using SM.Core.Contracts;
using UnityEngine;

namespace SM.Content.Definitions
{
    /// <summary>
    /// authored 트리거 효과 — 증강·패시브 .asset 에 직접 작성하는 조건부 발동 효과.
    /// 런타임 <c>CombatTriggeredEffect</c>(SM.Combat) 로 변환된다.
    /// 상시 적용 <see cref="SerializableStatModifier"/>(평면 스탯)와 달리 "전투 시작/처치/체력 임계" 시점에 발동한다.
    /// </summary>
    [Serializable]
    public sealed class TriggeredEffectSpec
    {
        [Tooltip("발동 시점 (전투 시작 / 처치 시 / 체력 임계 이하).")]
        public CombatTriggerKind Trigger = CombatTriggerKind.BattleStart;

        [Tooltip("실행할 효과 연산 (상태 부여 / 회복 / 보호막 / 에너지).")]
        public TriggeredEffectOp Op = TriggeredEffectOp.ApplyStatus;

        [Tooltip("효과 대상 범위 (Self / AlliedCombatants / EnemyCombatants / CurrentTarget).")]
        public EffectScope Scope = EffectScope.Self;

        [Tooltip("효과 세기 — 상태 magnitude / 회복량 / 보호막량 / 에너지량.")]
        public float Magnitude = 0f;

        [Tooltip("OnHpBelow 트리거용 체력 비율 임계(0~1). 다른 트리거에선 무시.")]
        [Range(0f, 1f)]
        public float ThresholdRatio = 0f;

        [Tooltip("ApplyStatus 용 상태 ID (예: rally, guarded, marked).")]
        public string StatusId = string.Empty;

        [Tooltip("ApplyStatus 용 지속 시간(초).")]
        public float DurationSeconds = 0f;

        [Tooltip("ApplyStatus 용 최대 중첩.")]
        public int MaxStacks = 1;
    }
}
