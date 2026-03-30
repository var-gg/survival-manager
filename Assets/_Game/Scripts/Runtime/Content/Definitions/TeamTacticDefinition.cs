using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Team Tactic Definition", fileName = "teamtactic_")]
public sealed class TeamTacticDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public TeamPostureTypeValue Posture = TeamPostureTypeValue.StandardAdvance;
    public float CombatPace = 1f;
    public float FocusModeBias = 0f;
    public float FrontSpacingBias = 0f;
    public float BackSpacingBias = 0f;
    public float ProtectCarryBias = 0f;
    public float TargetSwitchPenalty = 0f;
    public List<StableTagDefinition> CompileTags = new();
}
