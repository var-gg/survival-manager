using System.Collections.Generic;
using SM.Core.Content;
using SM.Core.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Synergy Tier Definition", fileName = "synergytier_")]
    public sealed class SynergyTierDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public int Threshold = 2;
        public BudgetCard BudgetCard = new() { Domain = BudgetDomain.SynergyBreakpoint, PowerBand = SM.Core.Content.PowerBand.Standard };
        public List<EffectDescriptor> Effects = new();
        public List<SerializableStatModifier> Modifiers = new();

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDescription => legacyDescription;
    }
}
