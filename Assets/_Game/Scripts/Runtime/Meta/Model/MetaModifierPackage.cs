using System.Collections.Generic;
using SM.Core.Stats;

namespace SM.Meta.Model;

public sealed record MetaModifierPackage(
    string SourceId,
    ModifierSource Source,
    IReadOnlyList<StatModifier> Modifiers);
