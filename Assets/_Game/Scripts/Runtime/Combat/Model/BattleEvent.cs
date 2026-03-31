using SM.Core.Ids;

namespace SM.Combat.Model;

public enum BattleEventKind
{
    Action = 0,
    StatusApplied = 1,
    StatusRemoved = 2,
    CleanseTriggered = 3,
    ControlResistApplied = 4,
}

public sealed record BattleEvent(
    int StepIndex,
    float TimeSeconds,
    EntityId ActorId,
    string ActorName,
    BattleActionType ActionType,
    BattleLogCode LogCode,
    EntityId? TargetId,
    string? TargetName,
    float Value,
    BattleEventKind EventKind = BattleEventKind.Action,
    string PayloadId = "",
    float SecondaryValue = 0f,
    string Note = "");
