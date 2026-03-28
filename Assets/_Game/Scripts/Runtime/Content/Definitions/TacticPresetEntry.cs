using SM.Combat.Model;
using UnityEngine;

namespace SM.Content.Definitions;

[System.Serializable]
public sealed class TacticPresetEntry
{
    public int Priority;
    public TacticConditionType ConditionType;
    public float Threshold;
    public BattleActionType ActionType;
    public TargetSelectorType TargetSelector;
    public SkillDefinitionAsset Skill;
}
