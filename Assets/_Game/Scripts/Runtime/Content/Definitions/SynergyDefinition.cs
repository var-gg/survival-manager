using System.Collections.Generic;
using SM.Core.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Synergy Definition", fileName = "synergy_")]
    public sealed class SynergyDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public string CountedTagId = string.Empty;
        public AuthorityLayer AuthorityLayer = AuthorityLayer.Synergy;
        public List<SynergyTierDefinition> Tiers = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
    }
}
