using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

public enum ItemSlotType { Weapon = 0, Armor = 1, Accessory = 2 }

[CreateAssetMenu(menuName = "SM/Definitions/Item Base Definition", fileName = "itembase_")]
public sealed class ItemBaseDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public ItemSlotType SlotType = ItemSlotType.Weapon;
    public List<SerializableStatModifier> BaseModifiers = new();
}
