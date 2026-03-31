using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

public enum EncounterKindValue
{
    Skirmish = 0,
    Elite = 1,
    Boss = 2,
}

public enum ThreatTierValue
{
    Tier1 = 1,
    Tier2 = 2,
    Tier3 = 3,
}

[CreateAssetMenu(menuName = "SM/Definitions/Encounter Definition", fileName = "encounter_")]
public sealed class EncounterDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public EncounterKindValue Kind = EncounterKindValue.Skirmish;
    public string SiteId = string.Empty;
    public string EnemySquadTemplateId = string.Empty;
    public string BossOverlayId = string.Empty;
    public string RewardSourceId = string.Empty;
    public string FactionId = string.Empty;
    public ThreatTierValue ThreatTier = ThreatTierValue.Tier1;
    public int ThreatCost = 1;
    public int ThreatSkulls = 1;
    public string DifficultyBand = string.Empty;
    public List<string> RewardDropTags = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
