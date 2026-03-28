using SM.Core.Stats;
using UnityEngine;

namespace SM.Content.Definitions;

[System.Serializable]
public sealed class SerializableStatModifier
{
    public string StatId = string.Empty;
    public ModifierOp Op = ModifierOp.Flat;
    public float Value = 0f;
}
