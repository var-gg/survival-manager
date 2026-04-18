using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Team Tactic Definition", fileName = "teamtactic_")]
    public sealed class TeamTacticDefinition : ScriptableObject
    {
        public string Id = string.Empty;
        public string NameKey = string.Empty;
        public TeamPostureTypeValue Posture = TeamPostureTypeValue.StandardAdvance;
        public float CombatPace = 1f;
        public float FocusModeBias = 0f;
        public float FrontSpacingBias = 0f;
        public float BackSpacingBias = 0f;
        public float ProtectCarryBias = 0f;
        public float TargetSwitchPenalty = 0f;
        public List<StableTagDefinition> CompileTags = new();

        [FormerlySerializedAs("DisplayName")]
        [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

        public string LegacyDisplayName => legacyDisplayName;
    }
}
