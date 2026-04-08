using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.Sandbox;

[Serializable]
public sealed class CombatSandboxAnchorPose
{
    public DeploymentAnchorId Anchor = DeploymentAnchorId.FrontCenter;
    public Vector3 Position = Vector3.zero;
}

[CreateAssetMenu(menuName = "SM/Sandbox/Combat Sandbox Scene Layout", fileName = "combat_sandbox_layout_")]
public sealed class CombatSandboxSceneLayoutAsset : ScriptableObject
{
    private static readonly DeploymentAnchorId[] OrderedAnchors =
    {
        DeploymentAnchorId.FrontTop,
        DeploymentAnchorId.FrontCenter,
        DeploymentAnchorId.FrontBottom,
        DeploymentAnchorId.BackTop,
        DeploymentAnchorId.BackCenter,
        DeploymentAnchorId.BackBottom,
    };

    public string LayoutId = "combat_sandbox_layout_default";
    public string DisplayName = "Combat Sandbox Layout";
    public float SpawnOffsetX = 1.25f;
    public List<CombatSandboxAnchorPose> AllyAnchors = CreateDefaultAnchors(isEnemy: false);
    public List<CombatSandboxAnchorPose> EnemyAnchors = CreateDefaultAnchors(isEnemy: true);

    public BattlefieldLayout BuildBattlefieldLayout()
    {
        return CombatSandboxSceneLayoutCompiler.BuildBattlefieldLayout(AllyAnchors, EnemyAnchors, SpawnOffsetX);
    }

#if UNITY_EDITOR
    public void CaptureFromScene(Transform[] allyHandles, Transform[] enemyHandles)
    {
        CaptureHandles(AllyAnchors, allyHandles, isEnemy: false);
        CaptureHandles(EnemyAnchors, enemyHandles, isEnemy: true);
    }

    public void ApplyToScene(Transform[] allyHandles, Transform[] enemyHandles)
    {
        ApplyHandles(AllyAnchors, allyHandles);
        ApplyHandles(EnemyAnchors, enemyHandles);
    }

    private static void CaptureHandles(List<CombatSandboxAnchorPose> poses, Transform[] handles, bool isEnemy)
    {
        EnsureAnchorList(poses, isEnemy);
        for (var index = 0; index < poses.Count && index < handles.Length; index++)
        {
            if (handles[index] == null)
            {
                continue;
            }

            poses[index].Position = handles[index].position;
        }
    }

    private static void ApplyHandles(List<CombatSandboxAnchorPose> poses, Transform[] handles)
    {
        for (var index = 0; index < poses.Count && index < handles.Length; index++)
        {
            if (handles[index] == null)
            {
                continue;
            }

            handles[index].position = poses[index].Position;
        }
    }
#endif

    public static List<CombatSandboxAnchorPose> CreateDefaultAnchors(bool isEnemy)
    {
        var frontX = isEnemy ? 2f : -2f;
        var backX = isEnemy ? 4f : -4f;
        return new List<CombatSandboxAnchorPose>
        {
            new() { Anchor = DeploymentAnchorId.FrontTop, Position = new Vector3(frontX, 0f, 2f) },
            new() { Anchor = DeploymentAnchorId.FrontCenter, Position = new Vector3(frontX, 0f, 0f) },
            new() { Anchor = DeploymentAnchorId.FrontBottom, Position = new Vector3(frontX, 0f, -2f) },
            new() { Anchor = DeploymentAnchorId.BackTop, Position = new Vector3(backX, 0f, 2f) },
            new() { Anchor = DeploymentAnchorId.BackCenter, Position = new Vector3(backX, 0f, 0f) },
            new() { Anchor = DeploymentAnchorId.BackBottom, Position = new Vector3(backX, 0f, -2f) },
        };
    }

    internal static void EnsureAnchorList(List<CombatSandboxAnchorPose> poses, bool isEnemy)
    {
        while (poses.Count < OrderedAnchors.Length)
        {
            poses.Add(CreateDefaultAnchors(isEnemy)[poses.Count]);
        }

        for (var index = 0; index < OrderedAnchors.Length; index++)
        {
            poses[index].Anchor = OrderedAnchors[index];
        }
    }
}

[CreateAssetMenu(menuName = "SM/Sandbox/Combat Sandbox Preview Settings", fileName = "combat_sandbox_preview_settings_")]
public sealed class CombatSandboxPreviewSettingsAsset : ScriptableObject
{
    public string SettingsId = "combat_sandbox_preview_default";
    public string DisplayName = "Combat Sandbox Preview";
    public float RangePreviewRadius = 2f;
    public float NavigationPreviewRadius = 0.5f;
    public float SeparationPreviewRadius = 0.75f;
    public float PreferredRangeMinPreview = 1f;
    public float PreferredRangeMaxPreview = 3f;
    public float EngagementSlotRadiusPreview = 1.25f;
    public int EngagementSlotCountPreview = 4;
    public float HeadAnchorHeightPreview = 2f;
    public float FrontlineGuardRadiusPreview = 2.5f;
    public float ClusterRadiusPreview = 2.5f;
}

public static class CombatSandboxSceneLayoutCompiler
{
    public static BattlefieldLayout BuildBattlefieldLayout(
        IReadOnlyList<CombatSandboxAnchorPose> allyAnchors,
        IReadOnlyList<CombatSandboxAnchorPose> enemyAnchors,
        float spawnOffsetX)
    {
        var allAnchors = allyAnchors.Concat(enemyAnchors).ToList();
        var frontAnchors = allAnchors.Where(pose => pose.Anchor.IsFrontRow()).ToList();
        var backAnchors = allAnchors.Where(pose => !pose.Anchor.IsFrontRow()).ToList();
        var topAnchors = allAnchors.Where(pose => pose.Anchor.ToString().EndsWith("Top", StringComparison.Ordinal)).ToList();
        var centerAnchors = allAnchors.Where(pose => pose.Anchor.ToString().EndsWith("Center", StringComparison.Ordinal)).ToList();
        var bottomAnchors = allAnchors.Where(pose => pose.Anchor.ToString().EndsWith("Bottom", StringComparison.Ordinal)).ToList();

        if (frontAnchors.Count == 0 || backAnchors.Count == 0 || topAnchors.Count == 0 || centerAnchors.Count == 0 || bottomAnchors.Count == 0)
        {
            return BattlefieldLayout.Default;
        }

        var frontX = frontAnchors.Average(pose => Mathf.Abs(pose.Position.x));
        var backX = backAnchors.Average(pose => Mathf.Abs(pose.Position.x));
        var topY = topAnchors.Average(pose => pose.Position.z);
        var centerY = centerAnchors.Average(pose => pose.Position.z);
        var bottomY = bottomAnchors.Average(pose => pose.Position.z);

        return new BattlefieldLayout(
            frontX,
            backX,
            topY,
            centerY,
            bottomY,
            Mathf.Max(0.25f, spawnOffsetX));
    }
}
