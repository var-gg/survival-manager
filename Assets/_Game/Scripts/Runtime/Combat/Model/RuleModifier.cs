using System.Collections.Generic;
using SM.Core.Stats;

namespace SM.Combat.Model;

public enum RuleModifierKind
{
    BehaviorTag = 0,
    TargetingBias = 1,
    DamageConversion = 2,
    ResourceShift = 3,
    Intercept = 4,
    AdditionalChain = 5,
}

public sealed record RuleModifier(
    RuleModifierKind Kind,
    string Value,
    float Magnitude = 1f);

public sealed record CombatRuleModifierPackage(
    string SourceId,
    ModifierSource Source,
    IReadOnlyList<RuleModifier> Modifiers);
