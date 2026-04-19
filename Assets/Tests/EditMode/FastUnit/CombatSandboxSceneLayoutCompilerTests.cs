using System.Collections.Generic;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity.Sandbox;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class CombatSandboxSceneLayoutCompilerTests
{
    [Test]
    public void BuildBattlefieldLayout_UsesBothSidesForSymmetricProjection()
    {
        var ally = BuildAnchors(frontX: -1f, backX: -2f, topZ: 2f, centerZ: 0f, bottomZ: -2f);
        var enemy = BuildAnchors(frontX: 7f, backX: 8f, topZ: 4f, centerZ: 0f, bottomZ: -4f);

        var layout = CombatSandboxSceneLayoutCompiler.BuildBattlefieldLayout(ally, enemy, 1.75f);

        Assert.That(layout.FrontRowX, Is.EqualTo(4f).Within(0.001f));
        Assert.That(layout.BackRowX, Is.EqualTo(5f).Within(0.001f));
        Assert.That(layout.TopLaneY, Is.EqualTo(3f).Within(0.001f));
        Assert.That(layout.CenterLaneY, Is.EqualTo(0f).Within(0.001f));
        Assert.That(layout.BottomLaneY, Is.EqualTo(-3f).Within(0.001f));
        Assert.That(layout.SpawnOffsetX, Is.EqualTo(1.75f).Within(0.001f));
    }

    [Test]
    public void BuildBattlefieldLayout_IsStableWhenSidesAreSwapped()
    {
        var ally = CombatSandboxSceneLayoutAsset.CreateDefaultAnchors(isEnemy: false);
        var enemy = CombatSandboxSceneLayoutAsset.CreateDefaultAnchors(isEnemy: true);

        var original = CombatSandboxSceneLayoutCompiler.BuildBattlefieldLayout(ally, enemy, 1.25f);
        var swapped = CombatSandboxSceneLayoutCompiler.BuildBattlefieldLayout(enemy, ally, 1.25f);

        Assert.That(swapped, Is.EqualTo(original));
    }

    private static List<CombatSandboxAnchorPose> BuildAnchors(
        float frontX,
        float backX,
        float topZ,
        float centerZ,
        float bottomZ)
    {
        return new List<CombatSandboxAnchorPose>
        {
            new() { Anchor = DeploymentAnchorId.FrontTop, Position = new Vector3(frontX, 0f, topZ) },
            new() { Anchor = DeploymentAnchorId.FrontCenter, Position = new Vector3(frontX, 0f, centerZ) },
            new() { Anchor = DeploymentAnchorId.FrontBottom, Position = new Vector3(frontX, 0f, bottomZ) },
            new() { Anchor = DeploymentAnchorId.BackTop, Position = new Vector3(backX, 0f, topZ) },
            new() { Anchor = DeploymentAnchorId.BackCenter, Position = new Vector3(backX, 0f, centerZ) },
            new() { Anchor = DeploymentAnchorId.BackBottom, Position = new Vector3(backX, 0f, bottomZ) },
        };
    }
}
