using UnityEngine;

namespace SM.Content.Definitions
{
    /// <summary>Power-quality Refit의 schedule/cost owner-ratifiable authored knobs.</summary>
    [CreateAssetMenu(menuName = "SM/Definitions/Refit Balance", fileName = "refit_balance")]
    public sealed class RefitBalanceDefinition : ScriptableObject
    {
        public string Id = "refit_balance_v1";

        [Min(1)]
        public int RulesVersion = 1;

        public string AffixCatalogVersion = "shipped-affix-catalog-v1";

        [Header("Floor schedule: u_inf * (1 - eta^m)")]
        [Min(1)]
        public int MaximumFloorNumerator = 70;

        [Min(2)]
        public int MaximumFloorDenominator = 100;

        [Min(1)]
        public int FloorDecayNumerator = 55;

        [Min(2)]
        public int FloorDecayDenominator = 100;

        [Header("Echo cost: base * E1 * growth^(m-1) * gradeRatio^(g-gbar)")]
        [Min(0.01f)]
        public float CostBaseFirstFarmEchoMultiplier = 0.60f;

        [Min(1.01f)]
        public float CostGrowthPerLevel = 1.70f;

        [Min(1.01f)]
        public float GradeCostRatio = 1.25f;
    }
}
