using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Stable Tag Definition", fileName = "tag_")]
    public sealed class StableTagDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
    }
}
