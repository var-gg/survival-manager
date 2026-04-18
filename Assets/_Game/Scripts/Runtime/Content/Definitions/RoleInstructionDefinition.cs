using System.Collections.Generic;
using SM.Core.Content;
using UnityEngine;
using UnityEngine.Serialization;

namespace SM.Content.Definitions;

[CreateAssetMenu(menuName = "SM/Definitions/Role Instruction Definition", fileName = "role_")]
public sealed class RoleInstructionDefinition : ScriptableObject
{
    public string Id = string.Empty;
    public string NameKey = string.Empty;
    public DeploymentAnchorValue Anchor = DeploymentAnchorValue.FrontCenter;
    public string RoleTag = string.Empty;
    public float ProtectCarryBias = 0f;
    public float BacklinePressureBias = 0f;
    public float RetreatBias = 0f;
    public List<StableTagDefinition> CompileTags = new();

    [FormerlySerializedAs("DisplayName")]
    [SerializeField, HideInInspector] private string legacyDisplayName = string.Empty;

    public string LegacyDisplayName => legacyDisplayName;
}
