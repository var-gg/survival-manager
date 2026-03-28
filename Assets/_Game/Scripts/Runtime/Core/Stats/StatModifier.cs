using SM.Core.Tags;

namespace SM.Core.Stats;

public sealed record StatModifier(
    StatKey Stat,
    ModifierOp Op,
    float Value,
    ModifierSource Source,
    string SourceId,
    Tag? Tag = null);
