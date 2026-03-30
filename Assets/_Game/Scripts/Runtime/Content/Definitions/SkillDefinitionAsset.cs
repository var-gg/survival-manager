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
    public float Power = 0f;
    public float Range = 1f;
    public List<StableTagDefinition> CompileTags = new();
}
