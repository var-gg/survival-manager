using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

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
    public List<ItemSlotType> AllowedSlotTypes = new();
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public List<SerializableStatModifier> Modifiers = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
