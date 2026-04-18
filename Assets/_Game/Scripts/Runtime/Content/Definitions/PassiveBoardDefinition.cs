using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Passive Board Definition", fileName = "passiveboard_")]
    public sealed class PassiveBoardDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        [FormerlySerializedAs("ArchetypeId")]
        public string ClassId = string.Empty;
        public List<PassiveNodeDefinition> Nodes = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
    }
}
