using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class HeroProgressionRecord
{
    public string HeroId = string.Empty;
    public int Level = 1;
    public int Experience = 0;
    public List<string> UnlockedPassiveNodeIds = new();
    public List<string> UnlockedSkillIds = new();
}
