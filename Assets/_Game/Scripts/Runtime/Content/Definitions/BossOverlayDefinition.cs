using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

public enum BossPhaseTriggerValue
{
    None = 0,
    HealthBelowHalf = 1,
    HealthBelowQuarter = 2,
    EscortDefeated = 3,
}

[CreateAssetMenu(menuName = "SM/Definitions/Boss Overlay Definition", fileName = "boss_overlay_")]
public sealed class BossOverlayDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public string DescriptionKey = string.Empty;
    public BossPhaseTriggerValue PhaseTrigger = BossPhaseTriggerValue.None;
    public int ThreatCost = 1;
    public string SignatureAuraTag = string.Empty;
    public string SignatureUtilityTag = string.Empty;
    public List<string> RewardDropTags = new();
    public List<StatusApplicationRule> AppliedStatuses = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    [FormerlySerializedAs("Description")]
    [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
    public string LegacyDescription => legacyDescription;
}
