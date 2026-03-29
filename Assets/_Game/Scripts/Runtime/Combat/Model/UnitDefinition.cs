using System.Collections.Generic;
using SM.Core.Stats;

namespace SM.Combat.Model;

public sealed record UnitDefinition(
    string Id,
    string Name,
    string RaceId,
    string ClassId,
    DeploymentAnchorId PreferredAnchor,
    Dictionary<StatKey, float> BaseStats,
    IReadOnlyList<TacticRule> Tactics,
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<CombatModifierPackage>? Packages = null);
