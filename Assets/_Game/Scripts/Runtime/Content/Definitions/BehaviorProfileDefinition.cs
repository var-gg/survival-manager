using UnityEngine;
using SM.Core.Contracts;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Combat/Behavior Profile", fileName = "behavior_")]
public sealed class BehaviorProfileDefinition : ScriptableObject
{
    public FormationLine FormationLine = FormationLine.Frontline;
    public RangeDiscipline RangeDiscipline = RangeDiscipline.HoldBand;
    public float ReevaluationInterval = 0.25f;
    public float RangeHysteresis = 0.25f;
    public float PreferredRangeMin = 0f;
    public float PreferredRangeMax = 0f;
    public float ApproachBuffer = 0.4f;
    public float RetreatBuffer = 0.25f;
    public float ChaseLeashMeters = 5f;
    public float RetreatAtHpPercent = 0f;
    public float RetreatBias = 0.15f;
    public float MaintainRangeBias = 0.15f;
    public float Opportunism = 0.5f;
    public float Discipline = 0.5f;
    public float DodgeChance = 0f;
    public float BlockChance = 0f;
    public float BlockMitigation = 0.25f;
    public float Stability = 0.5f;
    public float BlockCooldownSeconds = 1.2f;
    public float FrontlineGuardRadius = 2.5f;
}
