using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

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
        [Min(0f)] public float PressureClockFirstPulseSeconds;
        [Min(0f)] public float PressureClockIntervalSeconds;
        [Range(0f, 1f)] public float PressureClockMaxHealthDamageRatio;
        [Min(0)] public int PressureClockMaxPulses;
        public List<string> RewardDropTags = new();
        public List<StatusApplicationRule> AppliedStatuses = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        [FormerlySerializedAs("Description")]
        [SerializeField, HideInInspector, TextArea] private string legacyDescription = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
        public string LegacyDescription => legacyDescription;
    }
}
