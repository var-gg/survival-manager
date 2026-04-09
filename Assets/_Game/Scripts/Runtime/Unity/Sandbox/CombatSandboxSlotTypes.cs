using System;
using System.Collections.Generic;
using SM.Combat.Model;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Unity.Sandbox;

[Serializable]
public sealed class CombatSandboxAllySlot
{
    public string HeroId = string.Empty;
    public DeploymentAnchorId Anchor = DeploymentAnchorId.FrontCenter;
    public string RoleInstructionIdOverride = string.Empty;
}

[Serializable]
public sealed class CombatSandboxEnemySlot
{
    public string ParticipantId = string.Empty;
    public string DisplayName = string.Empty;
    public string CharacterId = string.Empty;
    [FormerlySerializedAs("ArchetypeId")] public string ArchetypeIdOverride = string.Empty;
    public DeploymentAnchorId Anchor = DeploymentAnchorId.FrontCenter;
    public string PositiveTraitId = string.Empty;
    public string NegativeTraitId = string.Empty;
    public string RoleInstructionId = string.Empty;
    [HideInInspector] public string RoleTag = "auto";
    public List<string> TemporaryAugmentIds = new();
}
