using UnityEngine;
using SM.Combat.Model;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Skill Definition", fileName = "skill_")]
public sealed class SkillDefinitionAsset : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public SkillKind Kind = SkillKind.Strike;
    public float Power = 0f;
    public int Range = 1;

    public SkillDefinition ToRuntime()
        => new(Id, DisplayName, Kind, Power, Range);
}
