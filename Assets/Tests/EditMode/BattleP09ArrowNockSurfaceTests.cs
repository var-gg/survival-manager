using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleP09ArrowNockSurfaceTests
{
    [Test]
    public void NockArrow_ShowsDuringWindup_HidesAtReleaseTick()
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

            var surface = root.AddComponent<BattleP09ArrowNockSurface>();
            surface.Configure(model, CreateUnit(classId: "ranger"));

            Assert.That(arrow.activeSelf, Is.False, "구성 시 화살은 꺼 둔다");

            surface.ConsumeCue(new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                10,
                "ally",
                ActionType: BattleActionType.BasicAttack,
                AnimationSemantic: BattleAnimationSemantic.BowShot,
                CommitSchedule: new BattleCommitSchedule(new ActionInstanceId(1), 0, WindupStartTick: 10, ContactTick: 15)));

            Assert.That(arrow.activeSelf, Is.True, "windup 동안 시위에 화살이 보인다");
            Assert.That(surface.IsArrowVisibleForTests, Is.True);

            surface.Tick(0.2f, 1f, paused: false);
            Assert.That(arrow.activeSelf, Is.True, "release tick(0.3s = (13−10)×0.1) 전이면 유지");

            surface.Tick(0.15f, 1f, paused: false);
            Assert.That(arrow.activeSelf, Is.False, "release tick에 숨겨 비행 투사체와 바통터치");
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

            var surface = root.AddComponent<BattleP09ArrowNockSurface>();
            surface.Configure(model, CreateUnit(classId: "vanguard"));

            surface.ConsumeCue(new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                10,
                "ally",
                ActionType: BattleActionType.BasicAttack,
                CommitSchedule: new BattleCommitSchedule(new ActionInstanceId(1), 0, WindupStartTick: 10, ContactTick: 15)));

            Assert.That(surface.IsArrowVisibleForTests, Is.False, "근접 유닛은 nock 대상이 아니다");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NockArrow_HidesImmediately_OnClearTransientState()
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

            Assert.That(surface.IsArrowVisibleForTests, Is.True, "스케줄 없는 commit은 폴백 노출");

            surface.ClearTransientState();

            Assert.That(surface.IsArrowVisibleForTests, Is.False, "PlaybackReset/seek 시 즉시 숨김");
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
