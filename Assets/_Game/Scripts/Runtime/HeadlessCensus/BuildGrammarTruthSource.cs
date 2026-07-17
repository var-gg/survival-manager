using System.Collections.Generic;
using SM.Combat.Model;

namespace SM.HeadlessCensus;

/// <summary>Editor adapter가 authored snapshot을 evaluator-only 문법 입력으로 낮춘 순수 DTO.</summary>
public sealed record BuildGrammarTruthSource(
    string SubjectKind,
    string SubjectId,
    bool Actionable,
    string SlotId = "",
    string RoleId = "",
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? RequiredTags = null,
    IReadOnlyList<string>? PrerequisiteIds = null,
    IReadOnlyList<string>? ExcludedTags = null,
    IReadOnlyList<string>? ConflictIds = null,
    IReadOnlyList<string>? AcquisitionPaths = null,
    IReadOnlyList<string>? GrantedSkillIds = null,
    BattleSkillSpec? Skill = null,
    CombatModifierPackage? ModifierPackage = null,
    CombatRuleModifierPackage? RulePackage = null,
    IReadOnlyList<CombatTriggeredEffect>? TriggeredEffects = null,
    TeamSynergyTierRule? SynergyRule = null,
    string ComparatorGroupId = "",
    string BudgetBand = "",
    bool HasVisibleTradeoff = false);

public static class BuildGrammarSubjectKind
{
    public const string Archetype = "archetype";
    public const string Skill = "skill";
    public const string Item = "item";
    public const string Affix = "affix";
    public const string Augment = "augment";
    public const string Passive = "passive";
    public const string Synergy = "synergy";
}
