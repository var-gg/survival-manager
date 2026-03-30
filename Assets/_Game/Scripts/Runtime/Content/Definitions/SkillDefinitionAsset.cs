using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Skill Definition", fileName = "skill_")]
public sealed class SkillDefinitionAsset : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public SkillKindValue Kind = SkillKindValue.Strike;
    public SkillSlotKindValue SlotKind = SkillSlotKindValue.CoreActive;
    public DamageTypeValue DamageType = DamageTypeValue.Physical;
    public SkillDeliveryValue Delivery = SkillDeliveryValue.Melee;
    public SkillTargetRuleValue TargetRule = SkillTargetRuleValue.NearestEnemy;
    public float Power = 0f;
    public float Range = 1f;
    public float PowerFlat = 0f;
    public float PhysCoeff = 1f;
    public float MagCoeff = 0f;
    public float HealCoeff = 0f;
    public float HealthCoeff = 0f;
    public bool CanCrit;
    public float ManaCost = 0f;
    public float BaseCooldownSeconds = 0f;
    public float CastWindupSeconds = 0f;
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public List<StableTagDefinition> SupportAllowedTags = new();
    public List<StableTagDefinition> RequiredWeaponTags = new();
    public List<StableTagDefinition> RequiredClassTags = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
}
