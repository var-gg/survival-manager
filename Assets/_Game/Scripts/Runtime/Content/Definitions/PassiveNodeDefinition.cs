using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Passive Node Definition", fileName = "passivenode_")]
public sealed class PassiveNodeDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string BoardId = string.Empty;
    public string DisplayName = string.Empty;
    public bool IsKeystone;
    [TextArea] public string Description = string.Empty;
    public List<StableTagDefinition> CompileTags = new();
    public List<StableTagDefinition> RuleModifierTags = new();
    public List<SerializableStatModifier> Modifiers = new();
}
