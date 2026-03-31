using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Combat/Mobility Profile", fileName = "mobility_")]
public sealed class MobilityProfileDefinition : ScriptableObject
{
    public MobilityStyleValue Style = MobilityStyleValue.None;
    public MobilityPurposeValue Purpose = MobilityPurposeValue.None;
    public float Distance = 0f;
    public float Cooldown = 0f;
    public float CastTime = 0f;
    public float Recovery = 0f;
    public float TriggerMinDistance = 0f;
    public float TriggerMaxDistance = 0f;
    public float LateralBias = 0f;
}
