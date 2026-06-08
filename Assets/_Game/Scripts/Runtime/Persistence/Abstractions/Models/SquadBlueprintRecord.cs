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
    // 유저가 출전 배치(anchor)를 명시적으로 편성했는지. true면 reload 시 자동배치가 덮지 않고 배치를 복원한다.
    public bool DeploymentUserAuthored;
}
