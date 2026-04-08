using System;
using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.Sandbox;

[Serializable]
public sealed class CombatSandboxPresetMemberSpec
{
    public string MemberId = string.Empty;
    public SandboxUnitSourceKind SourceKind = SandboxUnitSourceKind.Archetype;
    public string HeroId = string.Empty;
    public string DisplayName = string.Empty;
    public string ArchetypeId = string.Empty;
    public string CharacterId = string.Empty;
    public DeploymentAnchorId Anchor = DeploymentAnchorId.FrontCenter;
    public string RoleInstructionId = string.Empty;
    public UnitBuildOverrideAsset BuildOverride = null!;
    [TextArea] public string Notes = string.Empty;
}

[Serializable]
public sealed class CombatSandboxPresetSourceDefinition
{
    public SandboxLoadoutSourceKind SourceMode = SandboxLoadoutSourceKind.AuthoredSyntheticTeam;
    public TeamPostureType TeamPosture = TeamPostureType.StandardAdvance;
    public string TeamTacticId = string.Empty;
    public List<string> Tags = new();
    public List<CombatSandboxPresetMemberSpec> Members = new();
    public List<string> TeamTemporaryAugmentIds = new();
    public List<string> TeamPermanentAugmentIds = new();
    public CombatSandboxTeamSnapshotAsset SnapshotAsset = null!;
    public TextAsset SnapshotJsonAsset = null!;
    [TextArea] public string SnapshotJson = string.Empty;
    public CombatSandboxRemoteDeckReference RemoteDeck = new();
    [TextArea] public string Notes = string.Empty;
}

[CreateAssetMenu(menuName = "SM/Sandbox/Unit Build Override", fileName = "unit_build_override_")]
public sealed class UnitBuildOverrideAsset : ScriptableObject
{
    public CombatSandboxBuildOverrideData Data = new();
}

[CreateAssetMenu(menuName = "SM/Sandbox/Team Loadout Preset", fileName = "team_loadout_preset_")]
public sealed class TeamLoadoutPresetAsset : ScriptableObject
{
    public string PresetId = string.Empty;
    public string DisplayName = "Team Loadout Preset";
    public bool IsFavorite = false;
    public CombatSandboxPresetSourceDefinition Source = new();
}

[CreateAssetMenu(menuName = "SM/Sandbox/Team Snapshot", fileName = "team_snapshot_")]
public sealed class CombatSandboxTeamSnapshotAsset : ScriptableObject
{
    public string SnapshotId = string.Empty;
    public string DisplayName = "Team Snapshot";
    public CombatSandboxTeamDefinition Snapshot = new();
    [TextArea] public string Notes = string.Empty;
}

[CreateAssetMenu(menuName = "SM/Sandbox/Execution Preset", fileName = "combat_sandbox_execution_")]
public sealed class CombatSandboxExecutionPreset : ScriptableObject
{
    public CombatSandboxExecutionSettings Settings = new();
}

[CreateAssetMenu(menuName = "SM/Sandbox/Scenario Asset", fileName = "combat_sandbox_scenario_")]
public sealed class CombatSandboxScenarioAsset : ScriptableObject
{
    public string ScenarioId = string.Empty;
    public string DisplayName = "Combat Sandbox Scenario";
    public List<string> Tags = new();
    public bool IsFavorite = false;
    public TeamLoadoutPresetAsset LeftTeam = null!;
    public TeamLoadoutPresetAsset RightTeam = null!;
    public CombatSandboxExecutionPreset ExecutionPreset = null!;
    [TextArea] public string Notes = string.Empty;
    [TextArea] public string ExpectedOutcome = string.Empty;
}
