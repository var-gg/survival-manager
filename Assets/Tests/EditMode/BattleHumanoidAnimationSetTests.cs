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
            Object.DestroyImmediate(root);
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
            Object.DestroyImmediate(root);
            Destroy(set, idle, basic);
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
            Object.DestroyImmediate(root);
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

    private static BattleUnitReadModel CreateUnit(
        CombatActionState state,
        bool isAlive = true,
        bool isDefending = false,
        BattleActionType? pendingActionType = null,
        string currentSelector = "")
    {
        return new BattleUnitReadModel(
            "ally",
            "Ally",
            TeamSide.Ally,
            DeploymentAnchorId.FrontCenter,
            "human",
            "vanguard",
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
}
