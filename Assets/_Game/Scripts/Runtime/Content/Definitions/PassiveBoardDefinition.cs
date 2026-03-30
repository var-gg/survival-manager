using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Passive Board Definition", fileName = "passiveboard_")]
public sealed class PassiveBoardDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public string ArchetypeId = string.Empty;
    public List<PassiveNodeDefinition> Nodes = new();
}
