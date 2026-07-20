using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions
{
    /// <summary>
    /// Node list order is the deterministic node-index coordinate; seed updates must preserve existing coordinates.
    /// </summary>
    [CreateAssetMenu(menuName = "SM/Definitions/Site Graph Definition", fileName = "site_graph_")]
    public sealed class SiteGraphDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string SiteId = string.Empty;
        public List<SiteGraphNodeDefinition> Nodes = new();
    }
}
