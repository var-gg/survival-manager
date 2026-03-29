using SM.Core.Ids;

namespace SM.Combat.Model;

public sealed record BattleEvent(
    int StepIndex,
    float TimeSeconds,
    EntityId ActorId,
    string ActorName,
    BattleActionType ActionType,
    EntityId? TargetId,
    string? TargetName,
    float Value,
    string Note);
