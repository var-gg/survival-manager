using System.Collections.Generic;

namespace SM.Combat.Model;

public sealed record BattleUnitReadModel(
    string Id,
    string Name,
    TeamSide Side,
    DeploymentAnchorId Anchor,
    string RaceId,
    string ClassId,
    CombatVector2 Position,
    float CurrentHealth,
    float MaxHealth,
    bool IsAlive,
    CombatActionState ActionState,
    BattleActionType? PendingActionType,
    string? TargetId,
    string? TargetName,
    float WindupProgress,
    float CooldownRemaining,
    bool IsDefending);

public sealed record BattleSimulationStep(
    int StepIndex,
    float TimeSeconds,
    IReadOnlyList<BattleUnitReadModel> Units,
    IReadOnlyList<BattleEvent> Events,
    bool IsFinished,
    TeamSide? Winner);
