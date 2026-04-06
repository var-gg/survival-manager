using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Character Definition", fileName = "character_")]
public sealed class CharacterDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public RaceDefinition Race;
    public ClassDefinition Class;
    public UnitArchetypeDefinition DefaultArchetype;
    public RoleInstructionDefinition DefaultRoleInstruction;

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
