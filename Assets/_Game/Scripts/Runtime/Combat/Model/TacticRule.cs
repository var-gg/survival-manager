namespace SM.Combat.Model;

public sealed record TacticRule(
    int Priority,
    TacticConditionType ConditionType,
    float Threshold,
    BattleActionType ActionType,
    TargetSelectorType TargetSelector,
    string? SkillId = null);
