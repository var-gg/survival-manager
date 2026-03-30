using System;
using System.Collections.Generic;

namespace SM.Persistence.Abstractions.Models;

[Serializable]
public sealed class SquadBlueprintRecord
{
    public string BlueprintId = string.Empty;
    public string DisplayName = string.Empty;
    public string TeamPosture = string.Empty;
    public string TeamTacticId = string.Empty;
    public Dictionary<string, string> DeploymentAssignments = new();
    public List<string> ExpeditionSquadHeroIds = new();
    public Dictionary<string, string> HeroRoleIds = new();
}
