using UnityEngine;
using SM.Core.Content;

namespace SM.Content.Definitions
{

    [CreateAssetMenu(menuName = "SM/Definitions/Combat/Footprint Profile", fileName = "footprint_")]
    public sealed class FootprintProfileDefinition : ScriptableObject
    {
        public float NavigationRadius = 0.42f;
        public float SeparationRadius = 0.65f;
        public float CombatReach = 0.65f;
        public float PreferredRangeMin = 0.6f;
        public float PreferredRangeMax = 1.15f;
        public int EngagementSlotCount = 4;
        public float EngagementSlotRadius = 0.95f;
        public BodySizeCategoryValue BodySizeCategory = BodySizeCategoryValue.Medium;
        public float HeadAnchorHeight = 2.05f;
    }
}
