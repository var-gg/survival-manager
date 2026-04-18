using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Trait Pool Definition", fileName = "traitpool_")]
    public sealed class TraitPoolDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string ArchetypeId = string.Empty;
        public List<TraitEntry> PositiveTraits = new();
        public List<TraitEntry> NegativeTraits = new();
    }
}
