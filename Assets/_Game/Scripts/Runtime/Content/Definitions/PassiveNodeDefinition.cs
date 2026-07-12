using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Passive Node Definition", fileName = "passivenode_")]
    public sealed class PassiveNodeDefinition : ScriptableObject, ISerializationCallbackReceiver
    {
        public string Id = string.Empty;
        public string BoardId = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public PassiveNodeKindValue NodeKind = PassiveNodeKindValue.Small;
        public List<string> PrerequisiteNodeIds = new();
        public List<StableTagDefinition> MutualExclusionTags = new();
        public int BoardDepth;
        public List<StableTagDefinition> CompileTags = new();
        public List<StableTagDefinition> RuleModifierTags = new();
        public List<SerializableStatModifier> Modifiers = new();

        [Tooltip("PoE식 노드 도달 보상 스킬 id — 이 노드를 선택하면 해당 스킬의 발동형 효과(TriggeredEffects)와 서포트 변조(SupportModifier)가 슬롯 계약 밖 효과 캐리어로 유닛에 합류한다. 빈 값이면 스탯 전용 노드.")]
        public string GrantedSkillId = string.Empty;

        [FormerlySerializedAs("IsKeystone")]
        [SerializeField, HideInInspector] private bool legacyIsKeystone;

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (legacyIsKeystone && NodeKind == PassiveNodeKindValue.Small)
            {
                NodeKind = PassiveNodeKindValue.Keystone;
            }
        }
    }
}
