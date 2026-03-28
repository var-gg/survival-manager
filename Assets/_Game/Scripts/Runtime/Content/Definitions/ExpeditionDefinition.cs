using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[System.Serializable]
public sealed class ExpeditionNodeDefinition
{
    public string Id = string.Empty;
    public string Label = string.Empty;
    public RewardTableDefinition RewardTable;
}

[CreateAssetMenu(menuName = "SM/Definitions/Expedition Definition", fileName = "expedition_")]
public sealed class ExpeditionDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public List<ExpeditionNodeDefinition> Nodes = new();
}
