using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleActorRenderingLayerTests
{
    [Test]
    public void Initialize_AssignsBattleActorLayerToEverySpawnedRenderer_WithoutTouchingStageRenderers()
    {
        var cameraGo = new GameObject("MainCamera");
        var stageGo = new GameObject("StageRoot");
        var overlayGo = new GameObject("OverlayRoot", typeof(RectTransform));
        var controllerGo = new GameObject("BattlePresentationRoot");
        var template = CreateRuntimeRendererTemplate();
        var catalog = ScriptableObject.CreateInstance<BattleActorPresentationCatalog>();

        try
        {
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();

            var controller = controllerGo.AddComponent<BattlePresentationController>();
            SetField(controller, "battleStageRoot", stageGo.transform);
            SetField(controller, "actorOverlayRoot", overlayGo.GetComponent<RectTransform>());
            catalog.SetDefaultWrapper(template);
            controller.ConfigurePresentationCatalog(catalog);

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[BattleLighting\].*BattleRenderEnvironmentAuthoring"));
            controller.Initialize(CreateInitialStep());

            var actorLayerMask = BattleActorWrapper.PresentationRenderingLayerMask;
            Assert.That(
                actorLayerMask,
                Is.Not.EqualTo(0u),
                $"Rendering layer '{BattleActorWrapper.PresentationRenderingLayerName}' is not defined.");

            var spawnedWrappers = controllerGo
                .GetComponentsInChildren<BattleActorWrapper>(true)
                .Where(wrapper => wrapper.gameObject.activeInHierarchy)
                .ToArray();
            Assert.That(spawnedWrappers, Has.Length.EqualTo(2), "Expected one spawned wrapper per battle unit.");

            var missing = new List<string>();
            var spawnedRendererCount = 0;
            foreach (var wrapper in spawnedWrappers)
            {
                foreach (var renderer in wrapper.GetComponentsInChildren<Renderer>(true))
                {
                    spawnedRendererCount++;
                    if ((renderer.renderingLayerMask & actorLayerMask) == 0u)
                    {
                        missing.Add(
                            $"{GetPath(renderer.transform, wrapper.transform)} has mask " +
                            $"0x{renderer.renderingLayerMask:X8}; missing " +
                            $"'{BattleActorWrapper.PresentationRenderingLayerName}' (0x{actorLayerMask:X8}).");
                    }
                }
            }

            Assert.That(spawnedRendererCount, Is.GreaterThan(0), "Spawned wrappers exposed no renderers to validate.");
            Assert.That(
                missing,
                Is.Empty,
                "Spawned actor renderer coverage is incomplete: " + string.Join(" ", missing));

            var stageLeaks = stageGo
                .GetComponentsInChildren<Renderer>(true)
                .Where(renderer => (renderer.renderingLayerMask & actorLayerMask) != 0u)
                .Select(renderer =>
                    $"{GetPath(renderer.transform, stageGo.transform)} unexpectedly has actor mask " +
                    $"0x{renderer.renderingLayerMask:X8}.")
                .ToArray();
            Assert.That(
                stageGo.GetComponentsInChildren<Renderer>(true),
                Is.Not.Empty,
                "The fixture created no stage renderers, so environment isolation was not witnessed.");
            Assert.That(
                stageLeaks,
                Is.Empty,
                "Environment renderers must remain outside the actor rendering layer: " + string.Join(" ", stageLeaks));
        }
        finally
        {
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(template.gameObject);
            Object.DestroyImmediate(controllerGo);
            Object.DestroyImmediate(overlayGo);
            Object.DestroyImmediate(stageGo);
            Object.DestroyImmediate(cameraGo);
        }
    }

    private static BattleActorWrapper CreateRuntimeRendererTemplate()
    {
        var root = new GameObject("BattleActorRuntimeRendererTemplate");
        var wrapper = root.AddComponent<BattleActorWrapper>();
        root.AddComponent<BattleActorView>();
        var adapter = root.AddComponent<BattlePrimitiveActorVisualAdapter>();

        var visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(root.transform, false);
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        vendorSlot.SetParent(visualRoot, false);

        wrapper.ConfigureAuthoring(
            visualRoot,
            vendorSlot,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        adapter.ConfigureAuthoring(visualRoot, null, null, true);
        root.SetActive(false);
        return wrapper;
    }

    private static BattleSimulationStep CreateInitialStep()
    {
        return new BattleSimulationStep(
            0,
            0f,
            new[]
            {
                CreateUnit("ally", TeamSide.Ally, DeploymentAnchorId.FrontCenter, new CombatVector2(-1f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, DeploymentAnchorId.BackCenter, new CombatVector2(1f, 0f)),
            },
            System.Array.Empty<BattleEvent>(),
            false,
            null);
    }

    private static BattleUnitReadModel CreateUnit(
        string id,
        TeamSide side,
        DeploymentAnchorId anchor,
        CombatVector2 position)
    {
        return new BattleUnitReadModel(
            id,
            id,
            side,
            anchor,
            "human",
            "vanguard",
            position,
            20f,
            20f,
            true,
            CombatActionState.AcquireTarget,
            BattleActionType.BasicAttack,
            null,
            null,
            0f,
            0f,
            0f,
            100f,
            false);
    }

    private static string GetPath(Transform target, Transform root)
    {
        var names = new Stack<string>();
        var current = target;
        while (current != null)
        {
            names.Push(current.name);
            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field!.SetValue(target, value);
    }
}
