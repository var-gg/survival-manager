using System.Collections.Generic;

namespace SM.Combat.Model;

public record BattleSkillSpec(
    string Id,
    string Name,
    SkillKind Kind,
    float Power,
    float Range,
    string SlotKind = "active_core",
    IReadOnlyList<string>? CompileTags = null);

[System.Obsolete("Use BattleSkillSpec for compiled battle inputs.")]
public sealed record SkillDefinition(
    string Id,
    string Name,
    SkillKind Kind,
    float Power,
    float Range)
    : BattleSkillSpec(Id, Name, Kind, Power, Range);
