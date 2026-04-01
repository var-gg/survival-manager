using System.Collections.Generic;
using SM.Core.Contracts;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[System.Flags]
public enum AugmentEligibleModeValue { Expedition = 1, Pvp = 2 }

[CreateAssetMenu(menuName = "SM/Definitions/Augment Definition", fileName = "augment_")]
public sealed class AugmentDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public ContentRarity Rarity = ContentRarity.Common;
    public bool IsPermanent;
    public BudgetCard BudgetCard = new() { Domain = BudgetDomain.Augment, PowerBand = global::SM.Content.Definitions.PowerBand.Major };
    public AugmentCategoryValue Category = AugmentCategoryValue.Combat;
    public string FamilyId = string.Empty;
    public int Tier = 1;
    public float OfferWeight = 1f;
    public AugmentOfferBucketValue OfferBucket = AugmentOfferBucketValue.LegacyDerived;
    public AugmentRiskRewardClassValue RiskRewardClass = AugmentRiskRewardClassValue.LegacyDerived;
    public float BudgetScore = 0f;
    public bool SuppressIfPermanentEquipped;
    public List<StableTagDefinition> Tags = new();
    public List<StableTagDefinition> BuildBiasTags = new();
    public List<StableTagDefinition> ProtectionTags = new();
    public List<StableTagDefinition> MutualExclusionTags = new();
    public List<StableTagDefinition> RequiresTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public AugmentEligibleModeValue EligibleModes = AugmentEligibleModeValue.Expedition;
    public AuthorityLayer AuthorityLayer = AuthorityLayer.Augment;
    public int RosterSlotDelta = 0;
    public List<EffectDescriptor> Effects = new();
    public List<SerializableStatModifier> Modifiers = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
