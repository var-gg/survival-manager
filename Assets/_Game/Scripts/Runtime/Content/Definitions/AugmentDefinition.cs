using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

public enum AugmentRarity { Silver = 0, Gold = 1, Platinum = 2, Permanent = 3 }

[CreateAssetMenu(menuName = "SM/Definitions/Augment Definition", fileName = "augment_")]
public sealed class AugmentDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public AugmentRarity Rarity = AugmentRarity.Silver;
    public bool IsPermanent;
    [TextArea] public string Description = string.Empty;
    public List<SerializableStatModifier> Modifiers = new();
}
