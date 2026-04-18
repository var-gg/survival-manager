using System.Collections.Generic;
using SM.Core.Stats;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [System.Serializable]
    public sealed class TraitEntry
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public List<SerializableStatModifier> Modifiers = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
