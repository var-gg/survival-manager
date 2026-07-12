using System.Collections.Generic;
using SM.Core.Contracts;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [System.Serializable]
    public sealed class StatusApplicationRule
    {
        public string Id = string.Empty;
        public string StatusId = string.Empty;
        public float DurationSeconds = 1f;
        public float Magnitude = 0f;
        public int MaxStacks = 1;
        public bool RefreshDurationOnReapply = true;
        public int StackCap = 0;
        public StatusStackPolicyValue StackPolicy = StatusStackPolicyValue.LegacyDerived;
        public StatusRefreshPolicyValue RefreshPolicy = StatusRefreshPolicyValue.LegacyDerived;
        public StatusProcAttributionPolicyValue ProcAttributionPolicy = StatusProcAttributionPolicyValue.LegacyDerived;
        public StatusOwnershipPolicyValue OwnershipPolicy = StatusOwnershipPolicyValue.LegacyDerived;
        public List<EffectDescriptor> Effects = new();
    }

    [CreateAssetMenu(menuName = "SM/Definitions/Status Family Definition", fileName = "status_family_")]
    public sealed class StatusFamilyDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public StatusGroupValue Group = StatusGroupValue.Control;
        public bool IsHardControl;
        public bool UsesControlDiminishing;
        public bool AffectedByTenacity = true;
        public float TenacityScale = 1f;
        public bool AppliesPeriodicDamage;

        [Tooltip("이 상태가 유닛의 받는 피해 배수에 주는 delta (guarded=-0.1 → 받는 피해 10% 감소). 0=영향 없음. 과거 sim에 -0.1 리터럴로 박혀 있던 값의 콘텐츠 승격.")]
        public float IncomingDamageDelta = 0f;

        [Tooltip("적용 magnitude가 이 상태의 숫자 채널에 실리는 배율 — sunder=방어/저항 차감량, marked/exposed=받는 피해 가산, wound=치유 감소율, slow=공속/이속 감쇠율. 1=magnitude 그대로(현행 공식). 과거 sim에 magnitude 직소비로 박혀 있던 식의 콘텐츠 승격(숫자 콘텐츠화 2보).")]
        public float MagnitudeScale = 1f;

        [Tooltip("적용 시 상태로 잔존하는 대신 즉시 보호막(barrier)으로 전환한다 (barrier=true). 과거 sim에 StatusId==\"barrier\" 문자열 분기로 박혀 있던 효과 종류의 콘텐츠 승격 — 효과 종류 데이터화 3보 1슬라이스. 바닥 1은 코드 소유 클램프.")]
        public bool GrantsBarrierOnApply;

        [Tooltip("이 상태를 보유한 유닛이 저지불가가 된다 — 하드 컨트롤 적용 면역 + 저작 강제이동(넉백/끌기) 면역 (unstoppable=true). 과거 sim에 HasStatus(\"unstoppable\") 문자열 조회로 박혀 있던 효과 종류의 콘텐츠 승격 — 효과 종류 데이터화 3보 3b. 정화 프로필이 부여하는 상태 id는 별개 축(CleanseProfileDefinition.GrantsUnstoppable).")]
        public bool GrantsUnstoppable;

        [Tooltip("이 상태를 보유한 유닛의 액티브 시전을 차단한다 — ActiveSkill 전술 규칙 게이트 + signature/flex 슬롯 차단 (silence=true). 과거 sim에 HasStatus(\"silence\") 문자열 조회로 박혀 있던 효과 종류의 콘텐츠 승격 — 효과 종류 데이터화 3보 3c.")]
        public bool BlocksActiveSkills;

        public string VfxCueId = string.Empty;
        public string SfxHookId = string.Empty;
        public BudgetCard BudgetCard = new() { Domain = BudgetDomain.Status, PowerBand = PowerBand.Minor };
        public bool IsRuleModifierOnly;
        public AuthorityLayer AuthorityLayer = AuthorityLayer.Status;
        public int DefaultStackCap = 1;
        public StatusStackPolicyValue DefaultStackPolicy = StatusStackPolicyValue.LegacyDerived;
        public StatusRefreshPolicyValue DefaultRefreshPolicy = StatusRefreshPolicyValue.LegacyDerived;
        public StatusProcAttributionPolicyValue DefaultProcAttributionPolicy = StatusProcAttributionPolicyValue.LegacyDerived;
        public StatusOwnershipPolicyValue DefaultOwnershipPolicy = StatusOwnershipPolicyValue.LegacyDerived;
        public bool IsAiRelevant = true;
        public int VisualPriority = 0;
        public List<EffectDescriptor> Effects = new();
        public List<string> CompileTags = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
