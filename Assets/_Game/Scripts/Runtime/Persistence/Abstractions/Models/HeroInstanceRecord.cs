using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class HeroInstanceRecord
{
    public string HeroId = string.Empty;
    public string Name = string.Empty;
    public string ArchetypeId = string.Empty;
    public string RaceId = string.Empty;
    public string ClassId = string.Empty;
    public string PositiveTraitId = string.Empty;
    public string NegativeTraitId = string.Empty;
    public List<string> EquippedItemIds = new();
}
