using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Site Graph Definition", fileName = "site_graph_")]
public sealed class SiteGraphDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string SiteId = string.Empty;
    public List<SiteGraphNodeDefinition> Nodes = new();
}
