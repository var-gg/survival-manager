using UnityEngine;
using SM.Core.Content;

namespace SM.Content.Definitions
{

    [System.Serializable]
    public sealed class TacticPresetEntry
    {
        public int Priority;
        public TacticConditionTypeValue ConditionType;
        public float Threshold;
        public BattleActionTypeValue ActionType;
        public TargetSelectorTypeValue TargetSelector;
        public SkillDefinitionAsset Skill;
    }
}
