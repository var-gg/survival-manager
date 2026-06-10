using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleP09ArrowNockSurfaceTests
{
    [Test]
    public void NockArrow_VisibleWhileAiming_HidesAtRelease_ReturnsOnReNock()
    {
        var root = new GameObject("Actor");

        try
        {
            var model = new GameObject("P09_Model").transform;
            model.SetParent(root.transform, false);
            var bowGroup = new GameObject("Bow").transform;
            bowGroup.SetParent(model, false);
            var bowMesh = new GameObject("Bow_001");
            bowMesh.AddComponent<MeshRenderer>();
            bowMesh.transform.SetParent(bowGroup, false);
            var arrow = new GameObject("Arrow_001");
            arrow.AddComponent<MeshRenderer>();
            arrow.transform.SetParent(bowGroup, false);
            arrow.SetActive(false);

            var surface = root.AddComponent<BattleP09ArrowNockSurface>();
            surface.Configure(model, CreateUnit(classId: "ranger"));

            Assert.That(arrow.activeSelf, Is.True, "조준 대기(드로운 Hold idle)부터 화살이 메겨져 있어야 한다");

            surface.ConsumeCue(new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                10,
                "ally",
                ActionType: BattleActionType.BasicAttack,
                AnimationSemantic: BattleAnimationSemantic.BowShot,
                CommitSchedule: new BattleCommitSchedule(new ActionInstanceId(1), 0, WindupStartTick: 10, ContactTick: 15)));

            Assert.That(arrow.activeSelf, Is.True, "windup(조준→발사 직전) 동안 시위에 화살 유지");

            surface.Tick(0.2f, 1f, paused: false);
            Assert.That(arrow.activeSelf, Is.True, "release tick(0.3s = (13−10)×0.1) 전이면 유지");

            surface.Tick(0.15f, 1f, paused: false);
            Assert.That(arrow.activeSelf, Is.False, "release tick에 숨겨 비행 투사체와 바통터치");

            surface.Tick(0.5f, 1f, paused: false);
            Assert.That(arrow.activeSelf, Is.False, "재장전 박자(1.0s) 전에는 빈 시위");

            surface.Tick(0.6f, 1f, paused: false);
            Assert.That(arrow.activeSelf, Is.True, "재장전 타이밍에 새 화살이 메겨진다");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NockArrow_IsNoOp_ForNonBowUnits()
    {
        var root = new GameObject("Actor");

        try
        {
            var model = new GameObject("P09_Model").transform;
            model.SetParent(root.transform, false);
            var bowGroup = new GameObject("Bow").transform;
            bowGroup.SetParent(model, false);
            var arrow = new GameObject("Arrow_001");
            arrow.AddComponent<MeshRenderer>();
            arrow.transform.SetParent(bowGroup, false);
            arrow.SetActive(false);

            var surface = root.AddComponent<BattleP09ArrowNockSurface>();
            surface.Configure(model, CreateUnit(classId: "vanguard"));

            surface.ConsumeCue(new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                10,
                "ally",
                ActionType: BattleActionType.BasicAttack,
                CommitSchedule: new BattleCommitSchedule(new ActionInstanceId(1), 0, WindupStartTick: 10, ContactTick: 15)));

            Assert.That(arrow.activeSelf, Is.False, "근접 유닛은 nock 대상이 아니다");
            Assert.That(surface.IsArrowVisibleForTests, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NockArrow_ResetsToVisibleBaseline_OnClearTransientState()
    {
        var root = new GameObject("Actor");

        try
        {
            var model = new GameObject("P09_Model").transform;
            model.SetParent(root.transform, false);
            var arrow = new GameObject("Arrow_002");
            arrow.AddComponent<MeshRenderer>();
            arrow.transform.SetParent(model, false);

            var surface = root.AddComponent<BattleP09ArrowNockSurface>();
            surface.Configure(model, CreateUnit(classId: "ranger"));
            surface.ConsumeCue(new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                3,
                "ally",
                ActionType: BattleActionType.BasicAttack));
            surface.Tick(0.2f, 1f, paused: false);

            Assert.That(surface.IsArrowVisibleForTests, Is.False, "폴백 release 후 빈 시위");

            surface.ClearTransientState();

            Assert.That(surface.IsArrowVisibleForTests, Is.True, "seek/reset 후 조준 baseline(화살 메김)으로 복귀");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static BattleUnitReadModel CreateUnit(string classId)
    {
        return new BattleUnitReadModel(
            "ally",
            "Ally",
            TeamSide.Ally,
            DeploymentAnchorId.FrontCenter,
            "human",
            classId,
            new CombatVector2(0f, 0f),
            10f,
            10f,
            true,
            CombatActionState.AcquireTarget,
            null,
            "enemy",
            "Enemy",
            0f,
            0f,
            0f,
            100f,
            false,
            CurrentSelector: "",
            ArchetypeId: "warden",
            CharacterId: "chr_0001");
    }
}
