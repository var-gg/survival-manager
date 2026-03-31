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
    bool IsDefending,
    float Barrier = 0f,
    IReadOnlyList<string>? StatusIds = null,
    float HeadAnchorHeight = 2f,
    float NavigationRadius = 0.5f,
    float SeparationRadius = 0.7f,
    float PreferredRangeMin = 0f,
    float PreferredRangeMax = 0f,
    float EngagementSlotRadius = 1.2f,
    int EngagementSlotCount = 4);

public sealed record BattleSimulationStep(
    int StepIndex,
    float TimeSeconds,
    IReadOnlyList<BattleUnitReadModel> Units,
    IReadOnlyList<BattleEvent> Events,
    bool IsFinished,
    TeamSide? Winner);
