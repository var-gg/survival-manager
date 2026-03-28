using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Affix Definition", fileName = "affix_")]
public sealed class AffixDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    [TextArea] public string Description = string.Empty;
    public List<SerializableStatModifier> Modifiers = new();
}
