using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

public enum AugmentRarity { Silver = 0, Gold = 1, Platinum = 2, Permanent = 3 }
[System.Flags]
public enum AugmentEligibleModeValue { Expedition = 1, Pvp = 2 }

[CreateAssetMenu(menuName = "SM/Definitions/Augment Definition", fileName = "augment_")]
public sealed class AugmentDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public AugmentRarity Rarity = AugmentRarity.Silver;
    public bool IsPermanent;
    public AugmentCategoryValue Category = AugmentCategoryValue.Combat;
    public string FamilyId = string.Empty;
    public int Tier = 1;
    public float OfferWeight = 1f;
    public bool SuppressIfPermanentEquipped;
    [TextArea] public string Description = string.Empty;
    public List<StableTagDefinition> Tags = new();
    public List<StableTagDefinition> MutualExclusionTags = new();
    public List<StableTagDefinition> RequiresTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public AugmentEligibleModeValue EligibleModes = AugmentEligibleModeValue.Expedition;
    public List<SerializableStatModifier> Modifiers = new();
}
