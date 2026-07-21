using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Class Definition", fileName = "class_")]
    public sealed class ClassDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;

        [Header("Combat Baseline")]
        [Range(1, 200)] public int BaselineDamageMultiplierPercent = 100;
        [Range(0f, 1f)] public float BaseCritChance;
        [Min(1f)] public float BaseCritMultiplier = 1f;
        [Range(0f, 1f)] public float CritChanceCap = 1f;
        [Min(1f)] public float CritMultiplierCap = 2f;

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
