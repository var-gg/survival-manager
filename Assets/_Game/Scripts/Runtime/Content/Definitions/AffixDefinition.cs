using System.Collections.Generic;
using SM.Core.Content;
using SM.Core.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    public enum AffixTierValue
    {
        Implicit = 0,
        Prefix = 1,
        Suffix = 2,
    }

    [CreateAssetMenu(menuName = "SM/Definitions/Affix Definition", fileName = "affix_")]
    public sealed class AffixDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public string DescriptionKey = string.Empty;
        public AffixCategoryValue Category = AffixCategoryValue.Utility;
        public AffixTierValue Tier = AffixTierValue.Prefix;
        public AffixFamilyValue AffixFamily = AffixFamilyValue.LegacyDerived;
        public AffixEffectTypeValue EffectType = AffixEffectTypeValue.LegacyDerived;
        public float ValueMin = 0f;
        public float ValueMax = 0f;
        public List<ItemSlotType> AllowedSlotTypes = new();
        public List<StableTagDefinition> CompileTags = new();
        public List<StableTagDefinition> RuleModifierTags = new();
        public List<StableTagDefinition> RequiredTags = new();
        public List<StableTagDefinition> ExcludedTags = new();
        public int ItemLevelMin = 0;
        public float SpawnWeight = 1f;
        public string ExclusiveGroupId = string.Empty;
        public float BudgetScore = 0f;
        public BudgetCard BudgetCard = new() { Domain = BudgetDomain.Affix };
        public string TextTemplateKey = string.Empty;
        public AuthorityLayer AuthorityLayer = AuthorityLayer.Affix;
        public List<EffectDescriptor> Effects = new();
        public List<SerializableStatModifier> Modifiers = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
