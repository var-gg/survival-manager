using UnityEngine;

namespace SM.Content.Definitions
{
    /// <summary>사이트 run 전상 mechanic의 sweep 가능한 저작 balance 값.</summary>
    [CreateAssetMenu(menuName = "SM/Definitions/War Wound Balance", fileName = "war_wound_balance")]
    public sealed class WarWoundBalanceDefinition : ScriptableObject
    {
        public string Id = "war_wound_default";

        [Range(0.25f, 0.35f)]
        public float WoundTriggerHpRatio = 0.30f;

        [Range(0.85f, 0.92f)]
        public float WoundAbilityScalar = 0.88f;

        [Min(1)]
        public int MaxWoundsAppliedPerBattle = 2;

        [Min(1)]
        public int MaxActiveWounds = 2;

        [Min(1)]
        public int WoundStacksPerUnitMax = 1;

        public bool ApplyWoundOnLoss;
    }
}
