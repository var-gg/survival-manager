using System;
using System.Collections.Generic;
using System.Linq;
using SM.Combat.Model;
using UnityEngine;

namespace SM.Unity.Sandbox;

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
