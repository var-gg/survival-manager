using System.Collections.Generic;
using SM.Combat.Model;
using SM.Persistence.Abstractions.Models;

namespace SM.Unity.Sandbox;

public sealed record CombatSandboxCompilationContext(
    SaveProfile Profile,
    IReadOnlyDictionary<DeploymentAnchorId, string?> CurrentDeploymentAssignments,
    IReadOnlyList<string> CurrentSquadHeroIds,
    TeamPostureType CurrentTeamPosture,
    string CurrentTeamTacticId,
    IReadOnlyList<string> CurrentTemporaryAugmentIds,
    int CurrentNodeIndex);

public sealed record CombatSandboxCompiledTeam(
    string TeamId,
    string DisplayName,
    SandboxLoadoutSourceKind SourceMode,
    string ProvenanceLabel,
    SquadBlueprintState Blueprint,
    RunOverlayState Overlay,
    BattleLoadoutSnapshot Snapshot,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Warnings);

public sealed record CombatSandboxCompiledScenario(
    string ScenarioId,
    string DisplayName,
    CombatSandboxLaneKind LaneKind,
    int Seed,
    CombatSandboxExecutionSettings Execution,
    CombatSandboxCompiledTeam LeftTeam,
    CombatSandboxCompiledTeam RightTeam,
    IReadOnlyList<string> Warnings);
