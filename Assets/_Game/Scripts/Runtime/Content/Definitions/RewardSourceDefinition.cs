using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Reward Source Definition", fileName = "reward_source_")]
    public sealed class RewardSourceDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public RewardSourceKindValue Kind = RewardSourceKindValue.Skirmish;
        public string DropTableId = string.Empty;
        public bool UsesRewardCards = true;
        public List<RarityBracketValue> AllowedRarityBrackets = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
