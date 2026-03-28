namespace SM.Combat.Model;

public sealed record SkillDefinition(
    string Id,
    string Name,
    SkillKind Kind,
    float Power,
    int Range);
