using System;
using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.Sandbox;

[CreateAssetMenu(menuName = "SM/Sandbox/Combat Sandbox Config", fileName = "combat_sandbox_")]
public sealed class CombatSandboxConfig : ScriptableObject
{
    public string Id = "sandbox.default";
    public string DisplayName = "Combat Sandbox";
    public bool UseScenarioAuthoring = false;
    public CombatSandboxLaneKind DefaultLaneKind = CombatSandboxLaneKind.DirectCombatSandbox;
    public CombatSandboxSceneLayoutAsset SceneLayout = null!;
    public CombatSandboxPreviewSettingsAsset PreviewSettings = null!;
    public CombatSandboxScenarioMetadata Scenario = new();
    public CombatSandboxTeamDefinition LeftTeam = new();
    public CombatSandboxTeamDefinition RightTeam = new();
    public CombatSandboxExecutionSettings Execution = new();
    public TeamPostureType AllyPosture = TeamPostureType.StandardAdvance;
    public string TeamTacticId = string.Empty;
    public TeamPostureType EnemyPosture = TeamPostureType.StandardAdvance;
    // TODO: BattleSetupBuilder에 enemy team tactic content lookup 경로 추가 후 연결
    [HideInInspector] public string EnemyTeamTacticId = string.Empty;
    public int Seed = 17;
    public int BatchCount = 1;
    public List<CombatSandboxAllySlot> AllySlots = new();
    public List<CombatSandboxEnemySlot> EnemySlots = new();
    // TODO: sandbox GameSessionState에 expedition overlay 경로 추가 후 ally 임시 증강에 연결
    [HideInInspector] public List<string> TemporaryAugmentIds = new();
}
