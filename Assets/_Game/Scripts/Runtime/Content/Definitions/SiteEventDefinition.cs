using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions
{
    [CreateAssetMenu(menuName = "SM/Definitions/Site Event Definition", fileName = "site_event_")]
    public sealed class SiteEventDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string SiteId = string.Empty;
        public string SetupKey = string.Empty;
        public List<SiteEventChoiceDefinition> Choices = new();
    }
}
