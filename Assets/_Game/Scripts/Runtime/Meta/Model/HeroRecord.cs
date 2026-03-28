using System.Collections.Generic;

namespace SM.Meta.Model;

public sealed record HeroRecord(
    string Id,
    string Name,
    string ArchetypeId,
    string RaceId,
    string ClassId,
    string PositiveTraitId,
    string NegativeTraitId,
    IReadOnlyList<MetaModifierPackage> Packages);
