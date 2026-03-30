using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Synergy Tier Definition", fileName = "synergytier_")]
public sealed class SynergyTierDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public int Threshold = 2;
    [TextArea] public string Description = string.Empty;
    public List<SerializableStatModifier> Modifiers = new();
}
