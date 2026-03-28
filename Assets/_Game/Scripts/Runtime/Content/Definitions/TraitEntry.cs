using System.Collections.Generic;
using SM.Core.Stats;
using UnityEngine;

namespace SM.Content.Definitions;

[System.Serializable]
public sealed class TraitEntry
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    [TextArea] public string Description = string.Empty;
    public List<SerializableStatModifier> Modifiers = new();
}
