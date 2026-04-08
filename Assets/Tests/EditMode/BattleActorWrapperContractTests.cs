using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Editor.Validation;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattleActorWrapperContractTests
{
    [Test]
    public void FallbackSockets_ResolveContractWorldPositions()
    {
        var go = new GameObject("Wrapper");

        try
        {
            var wrapper = go.AddComponent<BattleActorWrapper>();
            go.transform.position = new Vector3(2f, 0f, 3f);
            wrapper.Configure(CreateUnit(headAnchorHeight: 1.6f));

            var center = wrapper.GetSocketWorld(BattleActorSocketId.Center);
            var head = wrapper.GetSocketWorld(BattleActorSocketId.Head);
            var hud = wrapper.GetSocketWorld(BattleActorSocketId.Hud);
            var feet = wrapper.GetSocketWorld(BattleActorSocketId.FeetRing);
            var telegraph = wrapper.GetSocketWorld(BattleActorSocketId.Telegraph);

            AssertVector(center, new Vector3(2f, 0.10f, 3f));
            Assert.That(head.y, Is.EqualTo(1.6f).Within(0.001f));
            AssertVector(hud, head);
            Assert.That(feet.y, Is.EqualTo(-0.98f).Within(0.001f));
            AssertVector(telegraph, feet);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Validator_AllowsFallbackWarnings_WhenViewAndAdapterExist()
    {
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(root.transform, false);

        try
        {
            var wrapper = root.AddComponent<BattleActorWrapper>();
            root.AddComponent<BattleActorView>();
            var adapter = root.AddComponent<BattlePrimitiveActorVisualAdapter>();
            wrapper.ConfigureAuthoring(
                visualRoot,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            adapter.ConfigureAuthoring(visualRoot, null, null, false);

            var report = BattleActorWrapperValidator.Validate(wrapper, "memory");

            Assert.That(report.HasErrors, Is.False, report.BuildSummary());
            Assert.That(report.Issues.Any(issue => !issue.IsError && issue.Code == "fallback_Head"), Is.True);
            Assert.That(report.Issues.Any(issue => !issue.IsError && issue.Code == "fallback_Telegraph"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void AuthoredSockets_CanSeparateHeadHudFeetAndTelegraph()
    {
        var root = new GameObject("Wrapper");
        var socketRig = new GameObject("SocketRig").transform;
        socketRig.SetParent(root.transform, false);
        var visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(root.transform, false);

        var head = new GameObject("Head").transform;
        head.SetParent(socketRig, false);
        head.localPosition = new Vector3(0f, 1.8f, 0f);
        var hud = new GameObject("Hud").transform;
        hud.SetParent(socketRig, false);
        hud.localPosition = new Vector3(0f, 2.2f, 0f);
        var feet = new GameObject("Feet").transform;
        feet.SetParent(socketRig, false);
        feet.localPosition = new Vector3(0f, -0.98f, 0f);
        var telegraph = new GameObject("Telegraph").transform;
        telegraph.SetParent(socketRig, false);
        telegraph.localPosition = new Vector3(0f, -0.98f, 0.2f);

        try
        {
            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(
                visualRoot,
                null,
                null,
                head,
                hud,
                null,
                feet,
                telegraph,
                null,
                null,
                null);

            Assert.That(wrapper.GetSocketTransform(BattleActorSocketId.Head), Is.Not.SameAs(wrapper.GetSocketTransform(BattleActorSocketId.Hud)));
            Assert.That(wrapper.GetSocketTransform(BattleActorSocketId.FeetRing), Is.Not.SameAs(wrapper.GetSocketTransform(BattleActorSocketId.Telegraph)));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void CastAndProjectileOrigin_FallbackToSiblingVisualSocket_WhenOnlyOneIsAuthored()
    {
        var castOnlyRoot = CreateWrapperWithVisualSocket(castAuthored: true, projectileAuthored: false);
        var projectileOnlyRoot = CreateWrapperWithVisualSocket(castAuthored: false, projectileAuthored: true);

        try
        {
            var castOnlyWrapper = castOnlyRoot.GetComponent<BattleActorWrapper>();
            var projectileOnlyWrapper = projectileOnlyRoot.GetComponent<BattleActorWrapper>();

            var castAuthoredWorld = castOnlyWrapper.GetSocketTransform(BattleActorSocketId.Cast)!.position;
            var projectileAuthoredWorld = projectileOnlyWrapper.GetSocketTransform(BattleActorSocketId.ProjectileOrigin)!.position;

            AssertVector(castOnlyWrapper.GetSocketWorld(BattleActorSocketId.ProjectileOrigin), castAuthoredWorld);
            AssertVector(projectileOnlyWrapper.GetSocketWorld(BattleActorSocketId.Cast), projectileAuthoredWorld);
        }
        finally
        {
            Object.DestroyImmediate(castOnlyRoot);
            Object.DestroyImmediate(projectileOnlyRoot);
        }
    }

    private static BattleUnitReadModel CreateUnit(float headAnchorHeight)
    {
        return new BattleUnitReadModel(
            Id: "ally",
            Name: "Ally",
            Side: TeamSide.Ally,
            Anchor: DeploymentAnchorId.FrontCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: new CombatVector2(-1f, 0f),
            CurrentHealth: 20f,
            MaxHealth: 20f,
            IsAlive: true,
            ActionState: CombatActionState.AcquireTarget,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: "enemy",
            TargetName: "Enemy",
            WindupProgress: 0f,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: false,
            HeadAnchorHeight: headAnchorHeight);
    }

    private static void AssertVector(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
    }

    private static GameObject CreateWrapperWithVisualSocket(bool castAuthored, bool projectileAuthored)
    {
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(root.transform, false);

        Transform? cast = null;
        Transform? projectile = null;
        if (castAuthored)
        {
            cast = new GameObject("Cast").transform;
            cast.SetParent(visualRoot, false);
            cast.localPosition = new Vector3(0f, 0.22f, 0.72f);
        }

        if (projectileAuthored)
        {
            projectile = new GameObject("ProjectileOrigin").transform;
            projectile.SetParent(visualRoot, false);
            projectile.localPosition = new Vector3(0f, 0.28f, 0.84f);
        }

        var wrapper = root.AddComponent<BattleActorWrapper>();
        wrapper.ConfigureAuthoring(
            visualRoot,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            cast,
            projectile,
            null);
        return root;
    }
}
