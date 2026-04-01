using System.Collections.Generic;

using SM.Core.Contracts;

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
    float CurrentEnergy,
    float MaxEnergy,
    bool IsDefending,
    float Barrier = 0f,
    IReadOnlyList<string>? StatusIds = null,
    float HeadAnchorHeight = 2f,
    float NavigationRadius = 0.5f,
    float SeparationRadius = 0.7f,
    float PreferredRangeMin = 0f,
    float PreferredRangeMax = 0f,
    float EngagementSlotRadius = 1.2f,
    int EngagementSlotCount = 4,
    CombatEntityKind EntityKind = CombatEntityKind.RosterUnit,
    string CurrentSelector = "",
    string CurrentFallback = "",
    float RetargetLockRemaining = 0f,
    float FrontlineGuardRadius = 2.5f,
    float ClusterRadius = 2.5f);

public sealed record BattleSimulationStep(
    int StepIndex,
    float TimeSeconds,
    IReadOnlyList<BattleUnitReadModel> Units,
    IReadOnlyList<BattleEvent> Events,
    bool IsFinished,
    TeamSide? Winner);
