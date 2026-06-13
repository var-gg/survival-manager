using System;
using SM.Core.Ids;

namespace SM.Combat.Model;

[Serializable]
public sealed record AiPerceptionBlackboard(
    string ActorUnitId,
    TeamSide Side,
    int StepIndex,
    EntityId? FocusMarkId,
    EntityId? CarryId,
    int FrontlineBreacherCount,
    int AliveAllyCount,
    int AliveEnemyCount,
    float LowestAllyHealthRatio,
    float LowestEnemyHealthRatio,
    float NearestEnemyDistance,
    bool ActorIsBacklineDiver,
    bool HasScreenedBacklineThreat)
{
    public static AiPerceptionBlackboard Empty(string actorUnitId, TeamSide side, int stepIndex)
        => new(
            actorUnitId,
            side,
            stepIndex,
            null,
            null,
            0,
            0,
            0,
            0f,
            0f,
            0f,
            false,
            false);
}
