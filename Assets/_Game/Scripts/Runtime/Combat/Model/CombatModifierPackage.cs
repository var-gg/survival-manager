using System.Collections.Generic;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed record CombatModifierPackage(
    string SourceId,
    ModifierSource Source,
    IReadOnlyList<StatModifier> Modifiers);
