using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using CoreEntityId = SM.Core.Ids.EntityId;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattlePresentationSnapshotTests
{
    [Test]
    public void RenderSnapshot_DoesNotReplayDiscreteCueCount()
    {
        var cameraGo = new GameObject("MainCamera");
        var stageGo = new GameObject("StageRoot");
        var overlayGo = new GameObject("OverlayRoot", typeof(RectTransform));
        var controllerGo = new GameObject("BattlePresentationController");

        try
        {
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();

            var controller = controllerGo.AddComponent<BattlePresentationController>();
            SetField(controller, "battleStageRoot", stageGo.transform);
            SetField(controller, "actorOverlayRoot", overlayGo.GetComponent<RectTransform>());

            var initial = CreateStep(0, events: System.Array.Empty<BattleEvent>());
            var current = CreateStep(1, events: new[]
            {
                new BattleEvent(1, 0.1f, new CoreEntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new CoreEntityId("enemy"), "Enemy", 10f)
            });

            controller.Initialize(initial);
            controller.AdvanceStep(initial, current);

            Assert.That(controller.LastCueCount, Is.GreaterThan(0));

            controller.ClearTransients(BattlePresentationCueType.SeekSnapshotApplied);
            controller.RenderSnapshot(initial);

            Assert.That(controller.LastCueCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(controllerGo);
            Object.DestroyImmediate(overlayGo);
            Object.DestroyImmediate(stageGo);
            Object.DestroyImmediate(cameraGo);
        }
    }

    [Test]
    public void SetBlend_EasesLargeRepositionWithoutChangingSnapshotEndpoint()
    {
        var cameraGo = new GameObject("MainCamera");
        var stageGo = new GameObject("StageRoot");
        var overlayGo = new GameObject("OverlayRoot", typeof(RectTransform));
        var controllerGo = new GameObject("BattlePresentationController");

        try
        {
            cameraGo.tag = "MainCamera";
            cameraGo.AddComponent<Camera>();

            var controller = controllerGo.AddComponent<BattlePresentationController>();
            SetField(controller, "battleStageRoot", stageGo.transform);
            SetField(controller, "actorOverlayRoot", overlayGo.GetComponent<RectTransform>());

            var initial = CreateStep(
                0,
                System.Array.Empty<BattleEvent>(),
                allyPosition: new CombatVector2(-1f, 0f),
                allyActionState: CombatActionState.AcquireTarget);
            var current = CreateStep(
                1,
                System.Array.Empty<BattleEvent>(),
                allyPosition: new CombatVector2(3f, 0f),
                allyActionState: CombatActionState.Reposition);

            controller.Initialize(initial);
            controller.SetBlend(initial, current, 0.25f);

            var wrapper = controllerGo.transform.Find("BattleActor_ally_Wrapper");
            Assert.That(wrapper, Is.Not.Null);
            var linearX = Mathf.Lerp(-1f, 3f, 0.25f);
            Assert.That(wrapper!.position.x, Is.LessThan(linearX - 0.05f));

            controller.ClearTransients(BattlePresentationCueType.SeekSnapshotApplied);
            controller.RenderSnapshot(current);

            Assert.That(wrapper.position.x, Is.EqualTo(3f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(controllerGo);
            Object.DestroyImmediate(overlayGo);
            Object.DestroyImmediate(stageGo);
            Object.DestroyImmediate(cameraGo);
        }
    }

    private static BattleSimulationStep CreateStep(
        int stepIndex,
        BattleEvent[] events,
        CombatVector2? allyPosition = null,
        CombatActionState allyActionState = CombatActionState.AcquireTarget)
    {
        return new BattleSimulationStep(
            stepIndex,
            stepIndex * 0.1f,
            new[]
            {
                new BattleUnitReadModel("ally", "Ally", TeamSide.Ally, DeploymentAnchorId.FrontCenter, "human", "vanguard", allyPosition ?? new CombatVector2(-1f, 0f), 20f, 20f, true, allyActionState, BattleActionType.BasicAttack, "enemy", "Enemy", 0f, 0f, 0f, 100f, false),
                new BattleUnitReadModel("enemy", "Enemy", TeamSide.Enemy, DeploymentAnchorId.BackCenter, "human", "vanguard", new CombatVector2(1f, 0f), 20f, 20f, true, CombatActionState.AcquireTarget, BattleActionType.BasicAttack, "ally", "Ally", 0f, 0f, 0f, 100f, false),
            },
            events,
            false,
            null);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field!.SetValue(target, value);
    }
}
