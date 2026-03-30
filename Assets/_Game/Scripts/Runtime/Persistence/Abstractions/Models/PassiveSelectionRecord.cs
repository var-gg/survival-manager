using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class PassiveSelectionRecord
{
    public string HeroId = string.Empty;
    public string BoardId = string.Empty;
    public List<string> SelectedNodeIds = new();
}
