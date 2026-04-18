using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [System.Serializable]
    public sealed class ExpeditionNodeDefinition
    {
        public string Id = string.Empty;
        public string LabelKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public string RewardSummaryKey = string.Empty;
        public RewardTableDefinition RewardTable;

        [FormerlySerializedAs("Label")]
        [SerializeField, HideInInspector] private string legacyLabel = string.Empty;

        public string LegacyLabel => legacyLabel;
    }

    [CreateAssetMenu(menuName = "SM/Definitions/Expedition Definition", fileName = "expedition_")]
    public sealed class ExpeditionDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public List<ExpeditionNodeDefinition> Nodes = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
    }
}
