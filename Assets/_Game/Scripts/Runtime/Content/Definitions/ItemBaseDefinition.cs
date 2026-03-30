using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

public enum ItemSlotType { Weapon = 0, Armor = 1, Accessory = 2 }

[CreateAssetMenu(menuName = "SM/Definitions/Item Base Definition", fileName = "itembase_")]
public sealed class ItemBaseDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public ItemSlotType SlotType = ItemSlotType.Weapon;
    public ItemIdentityValue IdentityKind = ItemIdentityValue.Baseline;
    public string ItemFamilyTag = string.Empty;
    public ItemRarityTierValue RarityTier = ItemRarityTierValue.Common;
    public string AffixPoolTag = string.Empty;
    public string CraftCategory = string.Empty;
    public string BudgetBand = string.Empty;
    public List<SkillDefinitionAsset> GrantedSkills = new();
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public List<StableTagDefinition> AllowedClassTags = new();
    public List<StableTagDefinition> AllowedArchetypeTags = new();
    public List<StableTagDefinition> UniqueRuleTags = new();
    public List<SerializableStatModifier> BaseModifiers = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
}
