using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Cleanse Profile Definition", fileName = "cleanse_profile_")]
    public sealed class CleanseProfileDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public List<string> RemovesStatusIds = new();
        public bool RemovesOneHardControl;
        public bool GrantsUnstoppable;
        public float GrantedUnstoppableDurationSeconds = 0f;

        [Tooltip("GrantsUnstoppable=true일 때 부여할 상태 id. 과거 sim에 \"unstoppable\" 리터럴로 박혀 있던 부여 대상의 콘텐츠 승격 — 저지불가 kind를 가진 파생 상태(시전 슈퍼아머 등)로 교체 저작 가능. 기본값이 기존 동작과 동일.")]
        public string GrantedStatusId = "unstoppable";

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
