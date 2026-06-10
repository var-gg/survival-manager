using System.Reflection;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Unity;
using UnityEditor;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("BatchOnly")]
public sealed class BattleHumanoidAnimationSetTests
{
    [Test]
    public void Driver_ClearTransientState_IsNoOpBeforePlayableGraphExists()
    {
        var root = new GameObject("Wrapper");

        try
        {
            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();

            Assert.DoesNotThrow(() => driver.ClearTransientState(BattlePresentationCueType.PlaybackReset));
            Assert.That(driver.CurrentOneShotClip, Is.Null);
        }
        finally
        {
            DestroyRoot(root);
        }
    }

    [Test]
    public void Driver_PlaysConfiguredLoopAndCueClipThroughWrapperAnimator()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var basic = CreateClip("basic");
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/P09_Modular_Humanoid/Scenes/DemoScene_Data/Animation/Demo_Pose/Combat_idle.controller");
        Assert.That(controller, Is.Not.Null);

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            var animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            SetField(set, "idle", idle);
            SetField(set, "basicAttacks", new[] { basic });

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            var unit = CreateUnit(CombatActionState.AcquireTarget);

            driver.Initialize(wrapper, unit);

            Assert.That(driver.CurrentLoopClip, Is.SameAs(idle));
            Assert.That(animator.runtimeAnimatorController, Is.Null);

            driver.ConsumeCue(
                new BattlePresentationCue(BattlePresentationCueType.ActionCommitBasic, 1, "ally", ActionType: BattleActionType.BasicAttack),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
                1f);

            Assert.That(driver.CuePlaybackCount, Is.EqualTo(1));
            Assert.That(driver.CurrentOneShotClip, Is.SameAs(basic));

            driver.Tick(1f, 1f, paused: false);

            Assert.That(driver.CurrentOneShotClip, Is.Null);
            Assert.That(driver.CurrentLoopClip, Is.SameAs(idle));
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, basic);
        }
    }

    [Test]
    public void Driver_StartsImpactReactionAtRecoilLead_AndDeathFromClipStart()
    {
        // 피격 리액션 가시화 계약: ImpactDamage 리액션 one-shot은 클립 0초(중립)가 아니라
        // reactionPoseLeadSeconds 지점에서 시작한다. hitstop이 시작 포즈를 고정하고 다음 액션이
        // 수십 ms 안에 리액션을 교체해도 "맞은 포즈"가 화면에 보이게 하는 수술. 사망은 0초부터.
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var hit = CreateClip("hit");
        hit.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        var death = CreateClip("death");
        death.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "hits", new[] { hit });
            SetField(set, "death", death);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            driver.ConsumeCue(
                new BattlePresentationCue(BattlePresentationCueType.ImpactDamage, 1, "ally", Magnitude: 5f),
                CreateUnit(CombatActionState.AcquireTarget),
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(hit));
            Assert.That(driver.CurrentOneShotClipTimeForTests, Is.EqualTo(0.18d).Within(0.001d),
                "reaction one-shot starts at the recoil lead, not at the neutral first frame");

            driver.ConsumeCue(
                new BattlePresentationCue(BattlePresentationCueType.DeathStart, 2, "ally"),
                CreateUnit(CombatActionState.Dead, isAlive: false),
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(death));
            Assert.That(driver.CurrentOneShotClipTimeForTests, Is.EqualTo(0d).Within(0.001d),
                "death one-shot plays the full fall from the clip start");
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, hit, death);
        }
    }

    [Test]
    public void Driver_HoldsDeathFinalPoseAfterDeathCue()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var death = CreateClip("death");
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "death", death);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            var dead = CreateUnit(CombatActionState.Dead, isAlive: false);
            driver.ConsumeCue(
                new BattlePresentationCue(BattlePresentationCueType.DeathStart, 4, "ally"),
                dead,
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(death));
            Assert.That(driver.IsHoldingTerminalPose, Is.False);

            driver.Tick(1f, 1f, paused: false);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(death));
            Assert.That(driver.IsHoldingTerminalPose, Is.True);

            driver.Tick(1f, 1f, paused: false);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(death));
            Assert.That(driver.IsHoldingTerminalPose, Is.True);
            Assert.That(driver.CuePlaybackCount, Is.EqualTo(1));
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, death);
        }
    }

    [Test]
    public void ResolveLoopClip_UsesUnitStateBeforeIdleFallback()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var move = CreateClip("move");
        var guard = CreateClip("guard");
        var death = CreateClip("death");

        try
        {
            SetField(set, "idle", idle);
            SetField(set, "move", move);
            SetField(set, "guardLoop", guard);
            SetField(set, "death", death);

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.Approach), isLocomoting: false, out var blockedApproachClip), Is.True);
            Assert.That(blockedApproachClip, Is.SameAs(idle));

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.Approach), isLocomoting: true, out var movingApproachClip), Is.True);
            Assert.That(movingApproachClip, Is.SameAs(move));

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.Recover, isDefending: true), out var guardClip), Is.True);
            Assert.That(guardClip, Is.SameAs(guard));

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.Dead, isAlive: false), out var deathClip), Is.True);
            Assert.That(deathClip, Is.SameAs(death));

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.AcquireTarget), out var idleClip), Is.True);
            Assert.That(idleClip, Is.SameAs(idle));
        }
        finally
        {
            Destroy(set, idle, move, guard, death);
        }
    }

    [Test]
    public void ResolveLoopClip_UsesBowReadyIdle_ForBowClassifiedUnits()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var bowReady = CreateClip("bow_ready");

        try
        {
            SetField(set, "idle", idle);
            SetField(set, "bowReadyIdle", bowReady);

            // 활 분류(ranger) 유닛의 전투 대기는 bow ready idle.
            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.AcquireTarget, classId: "ranger"), out var bowIdleClip), Is.True);
            Assert.That(bowIdleClip, Is.SameAs(bowReady));

            // 근접 유닛은 기존 idle 그대로.
            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.AcquireTarget), out var meleeIdleClip), Is.True);
            Assert.That(meleeIdleClip, Is.SameAs(idle));

            // 클립이 비어 있으면 활 유닛도 idle로 폴백.
            SetField(set, "bowReadyIdle", null!);
            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.AcquireTarget, classId: "ranger"), out var fallbackClip), Is.True);
            Assert.That(fallbackClip, Is.SameAs(idle));
        }
        finally
        {
            Destroy(set, idle, bowReady);
        }
    }

    [Test]
    public void ResolveLoopClip_UsesLocomotionOverrideWhenSnapshotPositionChanges()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var move = CreateClip("move");

        try
        {
            SetField(set, "idle", idle);
            SetField(set, "move", move);

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.Recover), isLocomoting: true, out var locomotionClip), Is.True);
            Assert.That(locomotionClip, Is.SameAs(move));

            Assert.That(set.TryResolveLoopClip(CreateUnit(CombatActionState.Recover), isLocomoting: false, out var idleClip), Is.True);
            Assert.That(idleClip, Is.SameAs(idle));
        }
        finally
        {
            Destroy(set, idle, move);
        }
    }

    [Test]
    public void ResolveLoopClip_UsesWalkAndRunRootMotionCadenceByWorldSpeed()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var walk = CreateClip("walk_rm");
        var run = CreateClip("run_rm");

        try
        {
            SetField(set, "idle", idle);
            SetField(set, "walkMove", walk);
            SetField(set, "move", run);
            SetField(set, "authoredWalkLocomotionSpeed", BattleHumanoidAnimationSet.DefaultWalkAuthoredLocomotionSpeed);
            SetField(set, "authoredLocomotionSpeed", BattleHumanoidAnimationSet.DefaultRunAuthoredLocomotionSpeed);
            SetField(set, "walkRunSwitchWorldSpeed", 2.45f);

            Assert.That(set.TryResolveLoopClip(
                CreateUnit(CombatActionState.Approach),
                isLocomoting: true,
                BattleActorPresentationPhase.CombatReady,
                worldSpeed: 1.6f,
                out var walkClip), Is.True);
            Assert.That(walkClip, Is.SameAs(walk));
            Assert.That(
                set.ResolveAuthoredLocomotionSpeed(1.6f),
                Is.EqualTo(BattleHumanoidAnimationSet.DefaultWalkAuthoredLocomotionSpeed).Within(0.0001f));

            Assert.That(set.TryResolveLoopClip(
                CreateUnit(CombatActionState.Approach),
                isLocomoting: true,
                BattleActorPresentationPhase.CombatReady,
                worldSpeed: 3.2f,
                out var runClip), Is.True);
            Assert.That(runClip, Is.SameAs(run));
            Assert.That(
                set.ResolveAuthoredLocomotionSpeed(3.2f),
                Is.EqualTo(BattleHumanoidAnimationSet.DefaultRunAuthoredLocomotionSpeed).Within(0.0001f));
        }
        finally
        {
            Destroy(set, idle, walk, run);
        }
    }

    [Test]
    public void ResolveLoopClip_UsesRelaxedIdleForNonCombatPresentationPhases()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var relaxed = CreateClip("relaxed_idle");
        var combat = CreateClip("combat_idle");
        var move = CreateClip("move");

        try
        {
            SetField(set, "relaxedIdle", relaxed);
            SetField(set, "idle", combat);
            SetField(set, "move", move);

            Assert.That(set.TryResolveLoopClip(
                CreateUnit(CombatActionState.AcquireTarget),
                isLocomoting: false,
                BattleActorPresentationPhase.RelaxedIdle,
                out var relaxedClip), Is.True);
            Assert.That(relaxedClip, Is.SameAs(relaxed));

            Assert.That(set.TryResolveLoopClip(
                CreateUnit(CombatActionState.AcquireTarget),
                isLocomoting: false,
                BattleActorPresentationPhase.CombatReady,
                out var combatClip), Is.True);
            Assert.That(combatClip, Is.SameAs(combat));

            Assert.That(set.TryResolveLoopClip(
                CreateUnit(CombatActionState.AcquireTarget),
                isLocomoting: true,
                BattleActorPresentationPhase.ResolvedIdle,
                out var resolvedClip), Is.True);
            Assert.That(resolvedClip, Is.SameAs(relaxed));
        }
        finally
        {
            Destroy(set, relaxed, combat, move);
        }
    }

    [Test]
    public void ResolveCueClip_MapsBattleCueSemanticsToConfiguredVariants()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var basic = CreateClip("basic");
        var damaging = CreateClip("damaging");
        var heal = CreateClip("heal");
        var hit = CreateClip("hit");

        try
        {
            SetField(set, "basicAttacks", new[] { basic });
            SetField(set, "damagingSkills", new[] { damaging });
            SetField(set, "healSkills", new[] { heal });
            SetField(set, "hits", new[] { hit });

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(BattlePresentationCueType.ActionCommitBasic, 1, "ally", ActionType: BattleActionType.BasicAttack),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
                out var basicClip), Is.True);
            Assert.That(basicClip, Is.SameAs(basic));

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(BattlePresentationCueType.ActionCommitSkill, 2, "ally", ActionType: BattleActionType.ActiveSkill),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.ActiveSkill),
                out var damageClip), Is.True);
            Assert.That(damageClip, Is.SameAs(damaging));

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(BattlePresentationCueType.ActionCommitSkill, 3, "ally", ActionType: BattleActionType.ActiveSkill),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.ActiveSkill, currentSelector: "LowestHpAlly"),
                out var healClip), Is.True);
            Assert.That(healClip, Is.SameAs(heal));

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(BattlePresentationCueType.ImpactDamage, 4, "enemy"),
                CreateUnit(CombatActionState.Recover),
                out var hitClip), Is.True);
            Assert.That(hitClip, Is.SameAs(hit));
        }
        finally
        {
            Destroy(set, basic, damaging, heal, hit);
        }
    }

    [Test]
    public void ResolveCueClip_PrefersSemanticVariantPoolBeforeLegacyFallback()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var dodge = CreateClip("dodge");
        var hit = CreateClip("hit");
        var dashGeneric = CreateClip("dash_generic");
        var dashForward = CreateClip("dash_forward");

        try
        {
            SetField(set, "hits", new[] { hit });
            SetField(set, "variants", new[]
            {
                new BattleHumanoidAnimationVariant(BattleAnimationSemantic.Dodge, dodge, intensity: BattleAnimationIntensity.Light),
                new BattleHumanoidAnimationVariant(BattleAnimationSemantic.DashEngage, dashGeneric),
                new BattleHumanoidAnimationVariant(
                    BattleAnimationSemantic.DashEngage,
                    dashForward,
                    BattleAnimationDirection.Forward,
                    BattleAnimationIntensity.Medium),
            });

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(
                    BattlePresentationCueType.ImpactDamage,
                    7,
                    "enemy",
                    AnimationSemantic: BattleAnimationSemantic.Dodge,
                    AnimationIntensity: BattleAnimationIntensity.Light),
                CreateUnit(CombatActionState.Recover),
                out var dodgeClip), Is.True);
            Assert.That(dodgeClip, Is.SameAs(dodge));

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(
                    BattlePresentationCueType.RepositionStart,
                    8,
                    "ally",
                    AnimationSemantic: BattleAnimationSemantic.DashEngage,
                    AnimationDirection: BattleAnimationDirection.Forward,
                    AnimationIntensity: BattleAnimationIntensity.Medium),
                CreateUnit(CombatActionState.Reposition),
                out var dashClip), Is.True);
            Assert.That(dashClip, Is.SameAs(dashForward));

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(BattlePresentationCueType.ImpactDamage, 9, "enemy"),
                CreateUnit(CombatActionState.Recover),
                out var fallbackHitClip), Is.True);
            Assert.That(fallbackHitClip, Is.SameAs(hit));
        }
        finally
        {
            Destroy(set, dodge, hit, dashGeneric, dashForward);
        }
    }

    [Test]
    public void Driver_PresentationRootMotionOffsetsVendorSlotByResidualOnly()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var move = CreateClip("move");
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            var animator = model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "move", move);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            // presentation root motion은 기본 비활성(회귀 방지). 이 테스트는 잔차 오프셋 메커니즘을 검증하므로 명시적으로 켠다.
            driver.SetPresentationRootMotionEnabledForTests(true);
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.Approach));

            Assert.That(animator.applyRootMotion, Is.True);
            Assert.That(model.GetComponent<BattlePresentationRootMotionRelay>(), Is.Not.Null);

            driver.BeginPresentationRootMotionFrame(isLocomoting: true, new Vector3(0.10f, 0f, 0f));
            driver.ConsumePresentationRootMotion(new Vector3(0.12f, 0f, 0f));

            Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(driver.PresentationRootMotionOffsetWorld.x, Is.EqualTo(0.02f).Within(0.0001f));
            Assert.That(vendorSlot.localPosition.x, Is.EqualTo(0.02f).Within(0.0001f));

            driver.BeginPresentationRootMotionFrame(isLocomoting: false, Vector3.zero);

            Assert.That(driver.PresentationRootMotionOffsetWorld, Is.EqualTo(Vector3.zero));
            Assert.That(vendorSlot.localPosition, Is.EqualTo(Vector3.zero));
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, move);
        }
    }

    [Test]
    public void ResolveCueClip_MapsRangedWindupSemanticsToConfiguredVariants()
    {
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var genericWindup = CreateClip("generic_windup");
        var bowDraw = CreateClip("bow_draw");
        var projectileWindup = CreateClip("projectile_windup");

        try
        {
            SetField(set, "windups", new[] { genericWindup });
            SetField(set, "variants", new[]
            {
                new BattleHumanoidAnimationVariant(
                    BattleAnimationSemantic.BowDraw,
                    bowDraw,
                    BattleAnimationDirection.Forward,
                    BattleAnimationIntensity.Medium),
                new BattleHumanoidAnimationVariant(
                    BattleAnimationSemantic.ProjectileWindup,
                    projectileWindup,
                    BattleAnimationDirection.Forward,
                    BattleAnimationIntensity.Medium),
            });

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(
                    BattlePresentationCueType.WindupEnter,
                    2,
                    "ally_ranger",
                    AnimationSemantic: BattleAnimationSemantic.BowDraw,
                    AnimationDirection: BattleAnimationDirection.Forward,
                    AnimationIntensity: BattleAnimationIntensity.Medium),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
                out var bowDrawClip), Is.True);
            Assert.That(bowDrawClip, Is.SameAs(bowDraw));

            Assert.That(set.TryResolveCueClip(
                new BattlePresentationCue(
                    BattlePresentationCueType.WindupEnter,
                    3,
                    "ally_mystic",
                    AnimationSemantic: BattleAnimationSemantic.ProjectileWindup,
                    AnimationDirection: BattleAnimationDirection.Forward,
                    AnimationIntensity: BattleAnimationIntensity.Medium),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
                out var projectileWindupClip), Is.True);
            Assert.That(projectileWindupClip, Is.SameAs(projectileWindup));
        }
        finally
        {
            Destroy(set, genericWindup, bowDraw, projectileWindup);
        }
    }

    [Test]
    public void EditorKevinFallback_ResolvesBowDrawAndBowShotClips()
    {
        var set = BattleHumanoidAnimationSet.ResolveRuntimeSet(null);

        Assert.That(set, Is.Not.Null);
        // 활 전투 대기 = 시위 당긴 조준 유지(Hold) 루프 — '조준 → 발사 → 재장전' 문법의 기준점.
        Assert.That(set!.TryResolveLoopClip(
            CreateUnit(CombatActionState.AcquireTarget, classId: "ranger"),
            out var bowIdleClip), Is.True);
        Assert.That(bowIdleClip.name, Does.Contain("Hold"));
        Assert.That(set!.TryResolveCueClip(
            new BattlePresentationCue(
                BattlePresentationCueType.WindupEnter,
                11,
                "ally_ranger",
                AnimationSemantic: BattleAnimationSemantic.BowDraw,
                AnimationDirection: BattleAnimationDirection.Forward,
                AnimationIntensity: BattleAnimationIntensity.Medium),
            CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
            out var drawClip), Is.True);
        Assert.That(drawClip.name, Does.Contain("BowShot"));

        Assert.That(set.TryResolveCueClip(
            new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                12,
                "ally_ranger",
                ActionType: BattleActionType.BasicAttack,
                AnimationSemantic: BattleAnimationSemantic.BowShot,
                AnimationDirection: BattleAnimationDirection.Forward,
                AnimationIntensity: BattleAnimationIntensity.Medium),
            CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
            out var shotClip), Is.True);
        Assert.That(shotClip.name, Does.Contain("BowShot"));
    }

    [Test]
    public void RuntimeSet_UsesRootMotionLocomotionClipsAndMeasuredSpeeds()
    {
        var set = BattleHumanoidAnimationSet.ResolveRuntimeSet(null);

        Assert.That(set, Is.Not.Null);
        Assert.That(set!.TryResolveLoopClip(
            CreateUnit(CombatActionState.Approach),
            isLocomoting: true,
            BattleActorPresentationPhase.CombatReady,
            worldSpeed: 1.6f,
            out var walkClip), Is.True);
        Assert.That(walkClip.name, Does.Contain("Walk01_Forward"));
        Assert.That(walkClip.name, Does.Contain("[RM]"));
        Assert.That(
            set.ResolveAuthoredLocomotionSpeed(1.6f),
            Is.EqualTo(BattleHumanoidAnimationSet.DefaultWalkAuthoredLocomotionSpeed).Within(0.0001f));

        Assert.That(set.TryResolveLoopClip(
            CreateUnit(CombatActionState.Approach),
            isLocomoting: true,
            BattleActorPresentationPhase.CombatReady,
            worldSpeed: 3.2f,
            out var runClip), Is.True);
        Assert.That(runClip.name, Does.Contain("Run01_Forward"));
        Assert.That(runClip.name, Does.Contain("[RM]"));
        Assert.That(
            set.ResolveAuthoredLocomotionSpeed(3.2f),
            Is.EqualTo(BattleHumanoidAnimationSet.DefaultRunAuthoredLocomotionSpeed).Within(0.0001f));

        Assert.That(set.TryResolveCueClip(
            new BattlePresentationCue(
                BattlePresentationCueType.RepositionStart,
                20,
                "ally",
                AnimationSemantic: BattleAnimationSemantic.LateralStrafe,
                AnimationDirection: BattleAnimationDirection.Left,
                AnimationIntensity: BattleAnimationIntensity.Medium),
            CreateUnit(CombatActionState.Reposition),
            out var strafeClip), Is.True);
        Assert.That(strafeClip.name, Does.Contain("StrafeRun01_Left"));
        Assert.That(strafeClip.name, Does.Contain("[RM]"));
    }

    [Test]
    public void Driver_LoopPlayableSpeed_ComposesTimelineAndLocomotionCadence()
    {
        // 러닝머신 회귀의 직접 수술 검증: SetBlend(ApplyState, cadence 기록) 직후 TickTransients
        // (Tick, 타임라인 배속 기록)가 와도 cadence가 클로버되지 않고 루프 속도 = 타임라인 × cadence.
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var move = CreateClip("move");
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "move", move);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            driver.ApplyState(
                CreateUnit(CombatActionState.Approach),
                playbackSpeed: 1.6f,
                paused: false,
                isLocomoting: true,
                BattleActorPresentationPhase.CombatReady,
                worldSpeed: 3.2f,
                authoritativeFrameDeltaWorld: Vector3.zero);
            driver.Tick(0.016f, 2f, paused: false);

            Assert.That(driver.CurrentLoopClip, Is.SameAs(move));
            Assert.That(driver.LoopPlaybackSpeedForTests, Is.EqualTo(3.2f).Within(0.0001f),
                "loop speed = timeline(2.0) × cadence(1.6) — Tick이 cadence를 덮어쓰면 안 된다");

            // 정지(비로코모션) 상태는 cadence 1 고정 — idle이 0.35x 하한으로 기어가지 않는다.
            driver.ApplyState(
                CreateUnit(CombatActionState.AcquireTarget),
                playbackSpeed: 0.35f,
                paused: false,
                isLocomoting: false,
                BattleActorPresentationPhase.CombatReady,
                worldSpeed: 0f,
                authoritativeFrameDeltaWorld: Vector3.zero);
            driver.Tick(0.016f, 1f, paused: false);

            Assert.That(driver.LoopPlaybackSpeedForTests, Is.EqualTo(1f).Within(0.0001f),
                "비로코모션 루프(idle)는 타임라인 속도 그대로");
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, move);
        }
    }

    [Test]
    public void Driver_CrossfadesLoopClipSwap_AndReleasesOutgoingSlot()
    {
        // 게이트 해제/걷기↔달리기 1프레임 포즈 스냅 수술: 루프 클립 교체는 하드 스왑이 아니라
        // 서브믹서 크로스페이드. 페이드 완료 후 outgoing 슬롯은 해제되어야 한다.
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var move = CreateClip("move");
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "move", move);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            Assert.That(driver.HasOutgoingLoopForTests, Is.False, "첫 루프는 페이드 없이 단독 재생");

            driver.ApplyState(
                CreateUnit(CombatActionState.Approach),
                playbackSpeed: 1f,
                paused: false,
                isLocomoting: true,
                BattleActorPresentationPhase.CombatReady,
                worldSpeed: 3.2f,
                authoritativeFrameDeltaWorld: Vector3.zero);

            Assert.That(driver.CurrentLoopClip, Is.SameAs(move));
            Assert.That(driver.HasOutgoingLoopForTests, Is.True, "이전 루프(idle)가 페이드 아웃 슬롯에 살아 있어야 한다");

            driver.Tick(0.3f, 1f, paused: false);

            Assert.That(driver.HasOutgoingLoopForTests, Is.False, "0.18s 페이드 완료 후 outgoing 슬롯 해제");
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, move);
        }
    }

    [Test]
    public void Driver_IgnoresImpactReaction_WhileDeathOneShotActive()
    {
        // 사망 one-shot의 마지막 발언권: 같은 step의 살해 ImpactDamage가 뒤따라 들어와도
        // 막 시작한 death 낙하를 교체하지 못한다 (cue 소비 시점 state는 stale-alive라 플래그 가드).
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var hit = CreateClip("hit");
        var death = CreateClip("death");
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "hits", new[] { hit });
            SetField(set, "death", death);

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            driver.ConsumeCue(
                new BattlePresentationCue(BattlePresentationCueType.DeathStart, 1, "ally"),
                CreateUnit(CombatActionState.Dead, isAlive: false),
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(death));

            driver.ConsumeCue(
                new BattlePresentationCue(BattlePresentationCueType.ImpactDamage, 1, "ally", Magnitude: 5f),
                CreateUnit(CombatActionState.AcquireTarget),
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(death), "death 재생 중 피격 리액션은 무시된다");
            Assert.That(driver.CuePlaybackCount, Is.EqualTo(1));
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, hit, death);
        }
    }

    [Test]
    public void Driver_StartsKnockdownReactionFromClipStart()
    {
        // 리코일 리드(0.18s)는 리코일 계열 전용 — Knockdown 같은 전신 변위 클립에 적용하면
        // 살아있는 유닛에 첫 프레임부터 수평 포즈가 번쩍인다(lying flash).
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var knockdown = CreateClip("knockdown");
        knockdown.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "variants", new[]
            {
                new BattleHumanoidAnimationVariant(
                    BattleAnimationSemantic.Knockdown,
                    knockdown,
                    intensity: BattleAnimationIntensity.Heavy),
            });

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            driver.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.ImpactDamage,
                    1,
                    "ally",
                    Magnitude: 9f,
                    AnimationSemantic: BattleAnimationSemantic.Knockdown,
                    AnimationIntensity: BattleAnimationIntensity.Heavy),
                CreateUnit(CombatActionState.AcquireTarget),
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(knockdown));
            Assert.That(driver.CurrentOneShotClipTimeForTests, Is.EqualTo(0d).Within(0.001d),
                "Knockdown은 0초부터 — 리코일 리드를 적용하지 않는다");
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, knockdown);
        }
    }

    [Test]
    public void Driver_PinsRangedCommitAtReleaseTick_MeleeAtContactTick()
    {
        // J12 인과 복원: 원거리 commit의 핀 대상은 ContactTick − travel(release tick).
        // 근접은 그대로 ContactTick. budget(초)으로 정렬 지점을 검증한다.
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var basic = CreateClip("basic");
        basic.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "basicAttacks", new[] { basic });

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            var schedule = new BattleCommitSchedule(new ActionInstanceId(1), 0, WindupStartTick: 10, ContactTick: 15);

            driver.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.ActionCommitBasic,
                    10,
                    "ally",
                    ActionType: BattleActionType.BasicAttack,
                    AnimationSemantic: BattleAnimationSemantic.BowShot,
                    CommitSchedule: schedule),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
                1f);

            Assert.That(driver.PinBudgetSecondsForTests, Is.EqualTo(0.3d).Within(1e-6),
                "BowShot release는 tick 13(=15 − travel 2)에 정렬 — budget 0.3s");

            driver.ClearTransientState(BattlePresentationCueType.PlaybackReset);

            driver.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.ActionCommitBasic,
                    10,
                    "ally",
                    ActionType: BattleActionType.BasicAttack,
                    CommitSchedule: schedule),
                CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack),
                1f);

            Assert.That(driver.PinBudgetSecondsForTests, Is.EqualTo(0.5d).Within(1e-6),
                "근접 commit은 ContactTick 15 그대로 — budget 0.5s");
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, basic);
        }
    }

    [Test]
    public void Driver_ChainsBowReloadDraw_AfterNaturalCommitEnd_ButNotAfterCancel()
    {
        // 활 연출 문법: windup(~0.22s)에는 1초짜리 당김이 안 들어가므로 시위 당김(Load)은
        // 발사(Release commit)가 자연 종료된 뒤 재장전 자리에서 재생된다. 취소된 commit은
        // 재장전 follow-through를 보여주지 않는다.
        var set = ScriptableObject.CreateInstance<BattleHumanoidAnimationSet>();
        var idle = CreateClip("idle");
        var basic = CreateClip("basic");
        basic.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        var draw = CreateClip("bow_reload_draw");
        draw.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        var root = new GameObject("Wrapper");
        var visualRoot = new GameObject("VisualRoot").transform;
        var vendorSlot = new GameObject("VendorVisualSlot").transform;
        var model = new GameObject("HumanoidModel");

        try
        {
            visualRoot.SetParent(root.transform, false);
            vendorSlot.SetParent(visualRoot, false);
            model.transform.SetParent(vendorSlot, false);
            model.AddComponent<Animator>();

            SetField(set, "idle", idle);
            SetField(set, "basicAttacks", new[] { basic });
            SetField(set, "variants", new[]
            {
                new BattleHumanoidAnimationVariant(BattleAnimationSemantic.BowDraw, draw),
            });

            var wrapper = root.AddComponent<BattleActorWrapper>();
            wrapper.ConfigureAuthoring(visualRoot, vendorSlot, null, null, null, null, null, null, null, null, null);

            var driver = root.AddComponent<BattleHumanoidAnimationDriver>();
            driver.ConfigureAnimationSet(set);
            driver.Initialize(wrapper, CreateUnit(CombatActionState.AcquireTarget));

            var schedule = new BattleCommitSchedule(new ActionInstanceId(1), 0, WindupStartTick: 10, ContactTick: 15);
            var commitCue = new BattlePresentationCue(
                BattlePresentationCueType.ActionCommitBasic,
                10,
                "ally",
                ActionType: BattleActionType.BasicAttack,
                AnimationSemantic: BattleAnimationSemantic.BowShot,
                CommitSchedule: schedule);

            driver.ConsumeCue(commitCue, CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack), 1f);
            Assert.That(driver.CurrentOneShotClip, Is.SameAs(basic));

            // 자연 종료(스케줄 시간 경과) → 재장전 draw 체인.
            driver.EvaluateContactPin(currentStepIndex: 30, alpha: 0f);

            Assert.That(driver.CurrentOneShotClip, Is.SameAs(draw), "commit 자연 종료 후 재장전 당김이 이어진다");

            // 취소 경로 — 재장전 체인 금지.
            driver.ClearTransientState(BattlePresentationCueType.PlaybackReset);
            driver.ConsumeCue(commitCue, CreateUnit(CombatActionState.ExecuteAction, pendingActionType: BattleActionType.BasicAttack), 1f);
            Assert.That(driver.CurrentOneShotClip, Is.SameAs(basic));

            driver.ConsumeCue(
                new BattlePresentationCue(
                    BattlePresentationCueType.ActionCanceled,
                    11,
                    "ally",
                    CommitSchedule: schedule),
                CreateUnit(CombatActionState.AcquireTarget),
                1f);

            Assert.That(driver.CurrentOneShotClip, Is.Null, "취소된 발사는 재장전을 보여주지 않는다");
        }
        finally
        {
            DestroyRoot(root);
            Destroy(set, idle, basic, draw);
        }
    }

    private static BattleUnitReadModel CreateUnit(
        CombatActionState state,
        bool isAlive = true,
        bool isDefending = false,
        BattleActionType? pendingActionType = null,
        string currentSelector = "",
        string classId = "vanguard")
    {
        return new BattleUnitReadModel(
            "ally",
            "Ally",
            TeamSide.Ally,
            DeploymentAnchorId.FrontCenter,
            "human",
            classId,
            new CombatVector2(0f, 0f),
            isAlive ? 10f : 0f,
            10f,
            isAlive,
            state,
            pendingActionType,
            "enemy",
            "Enemy",
            0f,
            0f,
            0f,
            100f,
            isDefending,
            CurrentSelector: currentSelector,
            ArchetypeId: "warden",
            CharacterId: "chr_0001");
    }

    private static AnimationClip CreateClip(string name)
    {
        return new AnimationClip
        {
            name = name
        };
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field!.SetValue(target, value);
    }

    private static void Destroy(params Object[] objects)
    {
        foreach (var target in objects)
        {
            Object.DestroyImmediate(target);
        }
    }

    private static void DestroyRoot(GameObject root)
    {
        foreach (var driver in root.GetComponentsInChildren<BattleHumanoidAnimationDriver>(true))
        {
            driver.DisposePresentationGraphForTests();
        }

        Object.DestroyImmediate(root);
    }
}
