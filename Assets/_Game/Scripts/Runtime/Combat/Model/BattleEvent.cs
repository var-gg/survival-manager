using SM.Core.Ids;

namespace SM.Combat.Model;

public sealed record BattleEvent(
    int Tick,
    EntityId ActorId,
    BattleActionType ActionType,
    EntityId? TargetId,
    float Value,
    string Note);
