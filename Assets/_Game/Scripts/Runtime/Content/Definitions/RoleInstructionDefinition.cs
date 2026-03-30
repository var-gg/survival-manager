using System.Collections.Generic;
using UnityEngine;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Role Instruction Definition", fileName = "role_")]
public sealed class RoleInstructionDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string DisplayName = string.Empty;
    public DeploymentAnchorValue Anchor = DeploymentAnchorValue.FrontCenter;
    public string RoleTag = string.Empty;
    public float ProtectCarryBias = 0f;
    public float BacklinePressureBias = 0f;
    public float RetreatBias = 0f;
    public List<StableTagDefinition> CompileTags = new();
}
