using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [System.Serializable]
    public sealed class LootBundleEntryDefinition
    {
        public string Id = string.Empty;
        public RewardType RewardType = RewardType.Gold;
        public int Amount = 1;
        public RarityBracketValue RarityBracket = RarityBracketValue.Common;
        public int Weight = 1;
        public bool IsGuaranteed;
        public List<string> RequiredContextTags = new();
    }

    [CreateAssetMenu(menuName = "SM/Definitions/Drop Table Definition", fileName = "drop_table_")]
    public sealed class DropTableDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public string RewardSourceId = string.Empty;
        public List<LootBundleEntryDefinition> Entries = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
