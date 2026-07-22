using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class VeilBreachTests
{
    private static readonly BehaviorProfile BacklineTarget = new(
        0.25f, 0.1f, 0.1f, 0.1f, 0.5f, 0.5f,
        DodgeChance: 0f,
        BlockChance: 0f,
        BlockMitigation: 0f,
        Stability: 0.5f,
        FormationLine: FormationLine.Backline);

    [Test]
    public void ResolveTick_BlinksBehindTargetBeforeSkillImpact()
    {
        var (state, actor, target, simulator) = CreatePendingBlinkBattle();
        var targetHealthBefore = target.CurrentHealth;

        var resolvedStep = RunUntilBlinkResolves(simulator);

        Assert.That(resolvedStep, Is.Not.Null, "the authored 0.9-second windup must resolve");
        Assert.That(resolvedStep!.Motions?.Any(motion =>
            motion.ActorId == actor.Id && motion.Kind == BattleMotionKind.Blink && motion.IsDiscrete), Is.True);
        Assert.That(actor.Position.X, Is.GreaterThan(target.Position.X),
            "an ally-side blink must land on the target's far side");
        Assert.That(target.CurrentHealth, Is.LessThan(targetHealthBefore),
            "impact must resolve from the post-blink position on the same tick");
    }

    [Test]
    public void DiveIntentGate_RejectsEngageAndAcceptsMatchingDive()
    {
        var skill = BlinkSkill();
        var state = CreateBattle(skill, out var actor, out var target);
        actor.SetCurrentTarget(target.Id);
        actor.SetCombatIntent(CombatIntent.Engage(target.Id));

        var nonDive = TacticEvaluator.Evaluate(state, actor);

        Assert.That(nonDive.ActionType, Is.EqualTo(BattleActionType.BasicAttack));
        Assert.That(nonDive.Skill?.Id, Is.Not.EqualTo(skill.Id));

        actor.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, target.Id, null, default, 20, 80));
        var dive = TacticEvaluator.Evaluate(state, actor);

        Assert.That(dive.ActionType, Is.EqualTo(BattleActionType.ActiveSkill));
        Assert.That(dive.Skill?.Id, Is.EqualTo(skill.Id));
        Assert.That(dive.Target?.Id, Is.EqualTo(target.Id));
    }

    [Test]
    public void LandingReanchor_ResetsDiveMetadataAndKeepsTightGeometryProgressGate()
    {
        var (state, actor, target, simulator) = CreatePendingBlinkBattle();
        var commitBefore = actor.CurrentCombatIntent.CommitUntilStep;

        Assert.That(RunUntilBlinkResolves(simulator), Is.Not.Null);

        var landed = actor.CurrentCombatIntent;
        Assert.That(landed.DiveEntryStep, Is.EqualTo(state.StepIndex - 1));
        Assert.That(landed.DiveEntryTargetId, Is.EqualTo(target.Id));
        Assert.That(landed.DiveEntryPathDistance, Is.Zero);
        Assert.That(landed.DiveEntryActorPosition, Is.EqualTo(actor.Position));
        Assert.That(landed.CommitUntilStep, Is.EqualTo(commitBefore),
            "the one-time landing re-anchor must not extend the dive commit");
        Assert.That(
            actor.Position.DistanceTo(target.Position),
            Is.GreaterThanOrEqualTo(landed.DiveEntryActorPosition.DistanceTo(target.Position)),
            "the landing must satisfy RoleBrain's >= progress test so the tight 5.0/5.5 continuation gate applies");
    }

    [Test]
    public void RootAtResolve_FizzlesWithoutMovementAndLeavesHalfCooldown()
    {
        var (state, actor, target, simulator) = CreatePendingBlinkBattle();
        actor.ApplyStatus(new StatusApplicationSpec("test:root", "root", 20f, 1f));
        var targetHealthBefore = target.CurrentHealth;
        var blinkMotionObserved = false;

        for (var stepIndex = 0; stepIndex < 15 && actor.PendingActionType != null; stepIndex++)
        {
            var step = simulator.Step();
            blinkMotionObserved |= step.Motions?.Any(motion => motion.Kind == BattleMotionKind.Blink) == true;
        }

        Assert.That(actor.PendingActionType, Is.Null);
        Assert.That(blinkMotionObserved, Is.False);
        Assert.That(target.CurrentHealth, Is.EqualTo(targetHealthBefore).Within(0.001f));
        Assert.That(actor.CooldownRemaining, Is.EqualTo(5f).Within(0.11f));
    }

    [Test]
    public void IntentChangesDuringWindup_ResolveGateFizzlesBeforeBlink()
    {
        var (_, actor, target, simulator) = CreatePendingBlinkBattle();
        for (var tick = 0; tick < 3; tick++)
        {
            simulator.Step();
        }

        var actorPositionBefore = actor.Position;
        var targetHealthBefore = target.CurrentHealth;
        actor.SetCombatIntent(CombatIntent.Engage(target.Id));
        var blinkMotionObserved = false;
        for (var tick = 0; tick < 12 && actor.PendingActionType != null; tick++)
        {
            var step = simulator.Step();
            blinkMotionObserved |= step.Motions?.Any(motion => motion.Kind == BattleMotionKind.Blink) == true;
        }

        Assert.That(actor.PendingActionType, Is.Null);
        Assert.That(blinkMotionObserved, Is.False);
        Assert.That(actor.Position, Is.EqualTo(actorPositionBefore));
        Assert.That(target.CurrentHealth, Is.EqualTo(targetHealthBefore).Within(0.001f));
        Assert.That(actor.CooldownRemaining, Is.EqualTo(5f).Within(0.11f));
    }

    [Test]
    public void ArenaEdgeBlink_FallsBackAlongAxisWithoutOverlappingTarget()
    {
        var skill = BlinkSkill();
        var state = CreateBattle(skill, out var actor, out var target);
        actor.SetPosition(new CombatVector2(0f, 0f));
        target.SetPosition(new CombatVector2(8f, 0.0919f));

        var moved = MovementResolver.TryApplySkillSelfBlink(state, actor, target, skill, out var landedBehind);

        Assert.That(moved, Is.True);
        Assert.That(landedBehind, Is.False,
            "an arena-edge fallback is not a far-side landing and must not re-anchor the dive");
        Assert.That(actor.Position.X, Is.LessThan(target.Position.X));
        Assert.That(
            actor.Position.DistanceTo(target.Position),
            Is.GreaterThanOrEqualTo(actor.NavigationRadius + target.NavigationRadius),
            "arena clamping must not collapse the blink onto the target center");
    }

    [Test]
    public void SelfScopedExposed_AppliesToCasterNotTarget()
    {
        var skill = BlinkSkill();
        var state = CreateBattle(skill, out var actor, out var target);
        var events = new List<BattleEvent>();

        StatusResolutionService.ApplySkillStatuses(state, actor, target, skill, events);

        Assert.That(actor.HasStatus("exposed"), Is.True);
        Assert.That(actor.GetStatusMagnitude("exposed"), Is.EqualTo(0.25f).Within(0.001f));
        Assert.That(target.HasStatus("exposed"), Is.False);
    }

    [Test]
    public void OpeningLock_UsesDedicatedDurationAndPreservesRepeatCooldown()
    {
        var skill = BlinkSkill(startsOnCooldown: true, openingLockSeconds: 1f);
        var state = CreateBattle(skill, out var actor, out var target);
        actor.SetCurrentTarget(target.Id);
        actor.SetCombatIntent(new CombatIntent(CombatIntentType.Dive, target.Id, null, default, 200, 80));

        var opening = TacticEvaluator.Evaluate(state, actor);

        Assert.That(actor.ResolveOpeningSkillCooldownRemaining(skill.Id), Is.EqualTo(1f).Within(0.001f));
        Assert.That(skill.BaseCooldownSeconds, Is.EqualTo(10f).Within(0.001f));
        Assert.That(opening.ActionType, Is.EqualTo(BattleActionType.BasicAttack),
            "the skill-only opening lock must not freeze ordinary attacks");

        for (var tick = 0; tick < 10; tick++)
        {
            actor.AdvanceTick();
        }

        var unlocked = TacticEvaluator.Evaluate(state, actor);
        Assert.That(actor.ResolveOpeningSkillCooldownRemaining(skill.Id), Is.Zero);
        Assert.That(unlocked.Skill?.Id, Is.EqualTo(skill.Id));

        actor.BeginWindup(BattleActionType.ActiveSkill, target.Id, skill.Id);
        target.ApplyStatus(new StatusApplicationSpec("test:target-root", "root", 20f, 0f));
        Assert.That(RunUntilBlinkResolves(new BattleSimulator(state, 40)), Is.Not.Null);
        Assert.That(actor.CooldownRemaining, Is.EqualTo(10f).Within(0.11f),
            "using the skill after its one-second opening lock must retain the authored ten-second repeat cooldown");
    }

    private static BattleSimulationStep? RunUntilBlinkResolves(BattleSimulator simulator)
    {
        for (var stepIndex = 0; stepIndex < 15 && !simulator.IsFinished; stepIndex++)
        {
            var step = simulator.Step();
            if (step.Events.Any(@event =>
                    @event.ActionType == BattleActionType.ActiveSkill
                    && @event.LogCode == BattleLogCode.ActiveSkillDamage))
            {
                return step;
            }
        }

        return null;
    }

    private static (BattleState State, UnitSnapshot Actor, UnitSnapshot Target, BattleSimulator Simulator)
        CreatePendingBlinkBattle()
    {
        var skill = BlinkSkill();
        var state = CreateBattle(skill, out var actor, out var target);
        actor.SetPosition(new CombatVector2(-3f, 0f));
        target.SetPosition(new CombatVector2(3.5f, 0f));
        actor.SetCurrentTarget(target.Id);
        actor.SetCombatIntent(new CombatIntent(
            CombatIntentType.Dive,
            target.Id,
            null,
            default,
            20,
            80,
            DiveEntryStep: 0,
            DiveEntryTargetId: target.Id,
            DiveEntryPathDistance: actor.Position.DistanceTo(target.Position),
            DiveEntryActorPosition: actor.Position));
        actor.BeginWindup(BattleActionType.ActiveSkill, target.Id, skill.Id);
        target.ApplyStatus(new StatusApplicationSpec("test:target-root", "root", 20f, 0f));
        return (state, actor, target, new BattleSimulator(state, 40));
    }

    private static BattleState CreateBattle(BattleSkillSpec skill, out UnitSnapshot actor, out UnitSnapshot target)
    {
        var state = CombatTestFactory.CreateBattleState(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit(
                    "veil_actor",
                    classId: "duelist",
                    hp: 300f,
                    physPower: 8f,
                    attackRange: 1.2f,
                    flexActive: skill),
            },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit(
                    "veil_target",
                    race: "undead",
                    classId: "ranger",
                    hp: 300f,
                    physPower: 0f,
                    attackRange: 5f,
                    behavior: BacklineTarget),
            },
            allyPosture: TeamPostureType.AllInBackline,
            seed: 2307);
        actor = state.Allies.Single();
        target = state.Enemies.Single();
        actor.SetActionState(CombatActionState.AcquireTarget);
        target.SetActionState(CombatActionState.AcquireTarget);
        actor.SetPosition(new CombatVector2(-2f, 0f));
        target.SetPosition(new CombatVector2(2f, 0f));
        return state;
    }

    private static BattleSkillSpec BlinkSkill(bool startsOnCooldown = false, float openingLockSeconds = 10f)
    {
        return new BattleSkillSpec(
            "skill_veil_breach",
            "Veil Breach",
            SkillKind.Strike,
            3.6f,
            8f,
            SlotKind: CompiledSkillSlots.UtilityActive,
            DamageType: DamageType.Physical,
            PowerFlat: 3.6f,
            PhysCoeff: 1f,
            CanCrit: true,
            AppliedStatuses: new[]
            {
                new StatusApplicationSpec(
                    "skill_veil_breach:self_exposed",
                    "exposed",
                    2f,
                    0.25f,
                    Scope: EffectScope.Self),
            },
            BaseCooldownSeconds: 10f,
            CastWindupSeconds: 0.9f,
            ResolvedSlotKind: ActionSlotKind.FlexActive,
            ActivationModel: ActivationModel.Cooldown,
            Lane: ActionLane.Primary,
            LockRule: ActionLockRule.HardCommit,
            TargetRuleData: new TargetRule
            {
                Domain = TargetDomain.EnemyUnit,
                PrimarySelector = TargetSelector.CurrentTarget,
                FallbackPolicy = TargetFallbackPolicy.Abort,
                Filters = TargetFilterFlags.InRange | TargetFilterFlags.ExcludeUntargetable,
                MaxAcquireRange = 8f,
                LockTargetAtCastStart = true,
                RetargetLockMode = RetargetLockMode.UntilCastComplete,
            },
            InterruptRefundScalar: 0.5f,
            DisplacementKind: SkillDisplacementKind.SelfBlinkToTarget,
            DisplacementDistance: 8.5f,
            StartsOnCooldown: startsOnCooldown,
            OpeningLockSeconds: startsOnCooldown ? openingLockSeconds : 0f);
    }
}
