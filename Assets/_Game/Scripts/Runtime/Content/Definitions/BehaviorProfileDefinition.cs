using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Combat/Behavior Profile", fileName = "behavior_")]
public sealed class BehaviorProfileDefinition : ScriptableObject
{
    public float ReevaluationInterval = 0.35f;
    public float RangeHysteresis = 0.25f;
    public float RetreatBias = 0.15f;
    public float MaintainRangeBias = 0.15f;
    public float Opportunism = 0.5f;
    public float Discipline = 0.5f;
    public float DodgeChance = 0f;
    public float BlockChance = 0f;
    public float BlockMitigation = 0.25f;
    public float Stability = 0.5f;
    public float BlockCooldownSeconds = 1.2f;
}
