using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Synergy Definition", fileName = "synergy_")]
public sealed class SynergyDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public string CountedTagId = string.Empty;
    public List<SynergyTierDefinition> Tiers = new();
}
