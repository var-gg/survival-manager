using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Skill Definition", fileName = "skill_")]
public sealed class SkillDefinitionAsset : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public SkillKindValue Kind = SkillKindValue.Strike;
    public SkillSlotKindValue SlotKind = SkillSlotKindValue.CoreActive;
    public DamageTypeValue DamageType = DamageTypeValue.Physical;
    public float Power = 0f;
    public float Range = 1f;
    public float PowerFlat = 0f;
    public float PhysCoeff = 1f;
    public float MagCoeff = 0f;
    public float HealCoeff = 0f;
    public float ManaCost = 0f;
    public float BaseCooldownSeconds = 0f;
    public float CastWindupSeconds = 0f;
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
}
