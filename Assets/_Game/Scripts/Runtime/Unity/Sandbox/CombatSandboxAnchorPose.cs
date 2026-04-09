using System;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.Sandbox;

[Serializable]
public sealed class CombatSandboxAnchorPose
{
    public DeploymentAnchorId Anchor = DeploymentAnchorId.FrontCenter;
    public Vector3 Position = Vector3.zero;
}
