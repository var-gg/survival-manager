using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using CoreEntityId = SM.Core.Ids.EntityId;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattlePresentationTransientSurfaceTests
{
    [Test]
    public void AdvanceStep_FiresTransientSurfaces_ButSnapshotResetDoesNotReplayThem()
    {
        var cameraGo = new GameObject("MainCamera");
        var stageGo = new GameObject("StageRoot");
        var overlayGo = new GameObject("OverlayRoot", typeof(RectTransform));
        var controllerGo = new GameObject("BattlePresentationRoot");
        var templateRoot = CreateWrapperTemplate("Template");
        var catalog = ScriptableObject.CreateInstance<BattleActorPresentationCatalog>();

        try
        {
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();

            var controller = controllerGo.AddComponent<BattlePresentationController>();
            SetField(controller, "battleStageRoot", stageGo.transform);
            SetField(controller, "actorOverlayRoot", overlayGo.GetComponent<RectTransform>());
            catalog.SetDefaultWrapper(templateRoot);
            controller.ConfigurePresentationCatalog(catalog);

            var initial = CreateStep(0, 20f, true, CombatActionState.AcquireTarget, System.Array.Empty<BattleEvent>());
            var current = CreateStep(1, 12f, true, CombatActionState.Recover, new[]
            {
                new BattleEvent(1, 0.1f, new CoreEntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new CoreEntityId("enemy"), "Enemy", 8f)
            });

            controller.Initialize(initial);
            controller.AdvanceStep(initial, current);

            var vfxCount = controllerGo.GetComponentsInChildren<BattleActorVfxSurface>().Sum(surface => surface.TriggerCount);
            var audioCount = controllerGo.GetComponentsInChildren<BattleActorAudioSurface>().Sum(surface => surface.TriggerCount);
            var bridge = controllerGo.GetComponentsInChildren<BattleAnimationEventBridge>().First();

            Assert.That(vfxCount, Is.GreaterThan(0));
            Assert.That(audioCount, Is.GreaterThan(0));
            Assert.That(bridge.TryHandleHook(BattleAnimationHookType.Impact), Is.True);

            var dispatchCount = bridge.DispatchCount;
            controller.ClearTransients(BattlePresentationCueType.PlaybackReset);
            controller.RenderSnapshot(initial);

            Assert.That(controllerGo.GetComponentsInChildren<BattleActorVfxSurface>().Sum(surface => surface.TriggerCount), Is.EqualTo(vfxCount));
            Assert.That(controllerGo.GetComponentsInChildren<BattleActorAudioSurface>().Sum(surface => surface.TriggerCount), Is.EqualTo(audioCount));
            Assert.That(bridge.TryHandleHook(BattleAnimationHookType.Impact), Is.False);
            Assert.That(bridge.DispatchCount, Is.EqualTo(dispatchCount));

            controller.ClearTransients(BattlePresentationCueType.SeekSnapshotApplied);
            controller.RenderSnapshot(initial);

            Assert.That(controllerGo.GetComponentsInChildren<BattleActorVfxSurface>().Sum(surface => surface.TriggerCount), Is.EqualTo(vfxCount));
            Assert.That(controllerGo.GetComponentsInChildren<BattleActorAudioSurface>().Sum(surface => surface.TriggerCount), Is.EqualTo(audioCount));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(templateRoot.gameObject);
            Object.DestroyImmediate(controllerGo);
            Object.DestroyImmediate(overlayGo);
            Object.DestroyImmediate(stageGo);
            Object.DestroyImmediate(cameraGo);
        }
    }

    private static BattleActorWrapper CreateWrapperTemplate(string name)
    {
        var root = new GameObject(name);
        var wrapper = root.AddComponent<BattleActorWrapper>();
        root.AddComponent<BattleActorView>();
        var adapter = root.AddComponent<BattlePrimitiveActorVisualAdapter>();
        root.AddComponent<BattleAnimationEventBridge>();
        root.AddComponent<BattleActorVfxSurface>();
        root.AddComponent<BattleActorAudioSurface>();

        var socketRig = new GameObject("SocketRig").transform;
        socketRig.SetParent(root.transform, false);
        var center = new GameObject("Center").transform;
        center.SetParent(socketRig, false);
        center.localPosition = new Vector3(0f, 0.10f, 0f);
        var head = new GameObject("Head").transform;
        head.SetParent(socketRig, false);
        head.localPosition = new Vector3(0f, 2.0f, 0f);
        var hud = new GameObject("Hud").transform;
        hud.SetParent(socketRig, false);
        hud.localPosition = new Vector3(0f, 2.2f, 0f);
        var hit = new GameObject("Hit").transform;
        hit.SetParent(socketRig, false);
        hit.localPosition = new Vector3(0f, 1.1f, 0.25f);
        var feet = new GameObject("Feet").transform;
        feet.SetParent(socketRig, false);
        feet.localPosition = new Vector3(0f, -0.98f, 0f);
        var telegraph = new GameObject("Telegraph").transform;
        telegraph.SetParent(socketRig, false);
        telegraph.localPosition = feet.localPosition;
        var cameraFocus = new GameObject("CameraFocus").transform;
        cameraFocus.SetParent(socketRig, false);
        cameraFocus.localPosition = new Vector3(0f, 1.2f, 0f);

        var visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(root.transform, false);
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        vendorSlot.SetParent(visualRoot, false);
        var cast = new GameObject("Cast").transform;
        cast.SetParent(visualRoot, false);
        cast.localPosition = new Vector3(0f, 0.22f, 0.72f);
        var projectile = new GameObject("ProjectileOrigin").transform;
        projectile.SetParent(visualRoot, false);
        projectile.localPosition = cast.localPosition;

        var shadow = GameObject.CreatePrimitive(PrimitiveType.Cylinder).GetComponent<Renderer>();
        shadow.name = "GroundShadow";
        shadow.transform.SetParent(visualRoot, false);
        shadow.transform.localPosition = new Vector3(0f, -1.02f, 0f);
        shadow.transform.localScale = new Vector3(0.58f, 0.03f, 0.58f);
        Object.DestroyImmediate(shadow.GetComponent<Collider>());
        shadow.sharedMaterial = CreateTestMaterial(new Color(0f, 0f, 0f, 0.28f));

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule).GetComponent<Renderer>();
        body.name = "Body";
        body.transform.SetParent(visualRoot, false);
        body.transform.localScale = new Vector3(0.92f, 1.1f, 0.92f);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.sharedMaterial = CreateTestMaterial(new Color(0.28f, 0.58f, 1f, 1f));

        wrapper.ConfigureAuthoring(visualRoot, vendorSlot, center, head, hud, hit, feet, telegraph, cast, projectile, cameraFocus);
        adapter.ConfigureAuthoring(visualRoot, body, shadow, true);
        return wrapper;
    }

    private static BattleSimulationStep CreateStep(int stepIndex, float enemyHealth, bool enemyAlive, CombatActionState allyState, BattleEvent[] events)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                new BattleUnitReadModel("ally", "Ally", TeamSide.Ally, DeploymentAnchorId.FrontCenter, "human", "vanguard", new CombatVector2(-1f, 0f), 20f, 20f, true, allyState, BattleActionType.BasicAttack, "enemy", "Enemy", allyState == CombatActionState.ExecuteAction ? 0.4f : 0f, 0f, 0f, 100f, false, ArchetypeId: "warden", CharacterId: "ally"),
                new BattleUnitReadModel("enemy", "Enemy", TeamSide.Enemy, DeploymentAnchorId.BackCenter, "human", "vanguard", new CombatVector2(1f, 0f), enemyHealth, 20f, enemyAlive, enemyAlive ? CombatActionState.AcquireTarget : CombatActionState.Dead, BattleActionType.BasicAttack, "ally", "Ally", 0f, 0f, 0f, 100f, false, ArchetypeId: "guardian", CharacterId: "enemy"),
            },
            events,
            false,
            null);
    }

    private static Material CreateTestMaterial(Color color)
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader);
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        return material;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field!.SetValue(target, value);
    }
}
