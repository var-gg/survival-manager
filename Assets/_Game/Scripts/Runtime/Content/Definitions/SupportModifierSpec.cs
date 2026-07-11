using System;
using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions
{

    /// <summary>
    /// 서포트 젬(SlotKind=Support 계열 스킬)의 페어-변조 계약 — 같은 유닛의 액티브 스킬 중
    /// SupportAllowedTags/SupportBlockedTags 매칭을 통과한 스킬을 컴파일 타임에 변조한다.
    /// 전투 코어는 변조 결과(BattleSkillSpec)만 소비하므로 sim 무변경·결정성 안전.
    /// 기본값 = identity(무효과) — 일반 스킬은 이 블록을 저작하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class SupportModifierSpec
    {
        [Tooltip("매칭된 액티브의 Power/PowerFlat 배수 (1 = 무변).")]
        public float PowerMultiplier = 1f;

        [Tooltip("매칭된 액티브의 기본 쿨다운 배수 (0.8 = 20% 감소).")]
        public float CooldownMultiplier = 1f;

        [Tooltip("매칭된 액티브의 시전 준비시간 배수.")]
        public float CastWindupMultiplier = 1f;

        [Tooltip("매칭된 액티브의 사거리 가산.")]
        public float RangeBonus = 0f;

        [Tooltip("매칭된 액티브가 부여하는 상태들의 지속시간 배수.")]
        public float StatusDurationMultiplier = 1f;

        [Tooltip("매칭된 액티브에 치명타 허용을 강제.")]
        public bool ForceCanCrit = false;

        [Tooltip("매칭된 액티브가 명중 시 추가로 부여할 상태.")]
        public List<StatusApplicationRule> AddedStatuses = new();

        [Tooltip("매칭된 액티브에 부여할 cleanse profile id (액티브에 비어있을 때만).")]
        public string GrantCleanseProfileId = string.Empty;

        [Tooltip("젬 장착 유닛 자체에 상시 적용할 스탯 (예: 흡혈/강인함).")]
        public List<SerializableStatModifier> OwnerModifiers = new();
    }
}
