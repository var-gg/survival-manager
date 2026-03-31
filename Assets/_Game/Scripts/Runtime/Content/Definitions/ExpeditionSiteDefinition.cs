using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Expedition Site Definition", fileName = "expedition_site_")]
public sealed class ExpeditionSiteDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string ChapterId = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public int SiteOrder = 0;
    public string FactionId = string.Empty;
    public List<string> EncounterIds = new();
    public string ExtractRewardSourceId = string.Empty;
    public ThreatTierValue ThreatTier = ThreatTierValue.Tier1;

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
