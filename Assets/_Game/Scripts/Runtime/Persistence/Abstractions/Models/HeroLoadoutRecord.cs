using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class HeroLoadoutRecord
{
    public string HeroId = string.Empty;
    public List<string> EquippedItemInstanceIds = new();
    public List<string> EquippedSkillInstanceIds = new();
    public string PassiveBoardId = string.Empty;
    public List<string> SelectedPassiveNodeIds = new();
    public List<string> EquippedPermanentAugmentIds = new();
}
