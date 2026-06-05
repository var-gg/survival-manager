using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Ids;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// Action choreography seam, D2 firing (GPT Pro): the cue builder consumes the typed combat-event-intent
/// channel — <b>Started → actor commit (strike) at WindupStartTick, contact-pinned via CommitSchedule;
/// Contacted → per-target reactions ONLY (no actor commit); Canceled → an ActionCanceled tombstone cue</b>
/// (no gameplay/reaction) — instead of inferring action cues from BattleEvents or parsing Note strings
/// (J8). Movement / guard / death / target-change cues remain unit-state / motion-intent driven.
/// </summary>
[Category("FastUnit")]
public sealed class BattlePresentationCueBuilderTests
{
    [Test]
    public void Build_MapsDamageAndHealIntents_ToSemanticSourceAndTargetCues()
    {
        var previous = CreateStep();
        var current = CreateStep(combatEvents: new[]
        {
            // The commit fires at Started (windup) and the target reaction at Contacted. In the sim these
            // land on different steps; the builder emits per-intent, so exercising both here is equivalent.
            Started("ally", CombatEventKind.BasicAttack),
            Started("healer", CombatEventKind.Skill),
            Contacted("ally", CombatEventKind.BasicAttack, Contact("enemy", CombatOutcome.Hit, 12f)),
            Contacted("healer", CombatEventKind.Skill, Contact("ally", CombatOutcome.Hit, 8f, isHeal: true)),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == "enemy"), Is.True);
        // The actor commit is outcome-free at Started, so a heal skill's cast is ActionCommitSkill; the
        // heal-specific visual is the target's ImpactHeal reaction at Contacted.
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitSkill && cue.SubjectActorId == "healer"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactHeal && cue.SubjectActorId == "ally"), Is.True);
    }

    [Test]
    public void Build_DetectsTargetSwapGuardAndRepositionTransitions()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy_a", isDefending: false, actionState: CombatActionState.AcquireTarget),
            CreateUnit("enemy_a", TeamSide.Enemy),
            CreateUnit("enemy_b", TeamSide.Enemy),
        });
        var current = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy_b", isDefending: true, actionState: CombatActionState.Reposition, position: new CombatVector2(-0.6f, 0f)),
            CreateUnit("enemy_a", TeamSide.Enemy),
            CreateUnit("enemy_b", TeamSide.Enemy),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.TargetChanged && cue.SubjectActorId == "ally" && cue.RelatedActorId == "enemy_b"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.GuardEnter && cue.SubjectActorId == "ally"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally"), Is.True);
    }

    [Test]
    public void Build_EmitsApproachAndSecurePositionMovementCues_WhenActorsMove()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("approach", TeamSide.Ally, targetId: "enemy_a", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("secure", TeamSide.Ally, targetId: "enemy_b", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy_a", TeamSide.Enemy, targetId: "approach", position: new CombatVector2(2f, 0f)),
            CreateUnit("enemy_b", TeamSide.Enemy, targetId: "secure", position: new CombatVector2(2f, 0f)),
        });
        var current = CreateStep(units: new[]
        {
            CreateUnit("approach", TeamSide.Ally, targetId: "enemy_a", actionState: CombatActionState.Approach, position: new CombatVector2(0.55f, 0f)),
            CreateUnit("secure", TeamSide.Ally, targetId: "enemy_b", actionState: CombatActionState.SecurePosition, position: new CombatVector2(0f, 0.55f)),
            CreateUnit("enemy_a", TeamSide.Enemy, targetId: "approach", position: new CombatVector2(2f, 0f)),
            CreateUnit("enemy_b", TeamSide.Enemy, targetId: "secure", position: new CombatVector2(2f, 0f)),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);
        var approach = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "approach");
        var secure = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "secure");

        Assert.That(approach.Magnitude, Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(approach.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.DashEngage));
        Assert.That(approach.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(secure.Magnitude, Is.EqualTo(0.55f).Within(0.001f));
        Assert.That(secure.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.LateralStrafe));
        Assert.That(secure.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Left));
    }

    [Test]
    public void Build_DoesNotEmitMovementCue_WhenMovementStateHasTinyDelta()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(2f, 0f)),
        });
        var current = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.Approach, position: new CombatVector2(0.02f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(2f, 0f)),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally"), Is.False);
    }

    [Test]
    public void Build_MapsContactOutcomes_ToAnimationSemantics()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally),
            CreateUnit("enemy_miss", TeamSide.Enemy),
            CreateUnit("enemy_dodge", TeamSide.Enemy),
            CreateUnit("enemy_block", TeamSide.Enemy),
            CreateUnit("enemy_crit", TeamSide.Enemy),
        });
        var current = CreateStep(
            units: previous.Units,
            combatEvents: new[]
            {
                Contacted("ally", CombatEventKind.BasicAttack,
                    Contact("enemy_miss", CombatOutcome.Miss, 0f, index: 0),
                    Contact("enemy_dodge", CombatOutcome.Dodge, 0f, index: 1),
                    Contact("enemy_block", CombatOutcome.Block, 4f, index: 2),
                    Contact("enemy_crit", CombatOutcome.Crit, 22f, index: 3)),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(FindImpact(cues, "enemy_miss").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.Miss));
        Assert.That(FindImpact(cues, "enemy_dodge").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.Dodge));
        Assert.That(FindImpact(cues, "enemy_dodge").AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Light));
        Assert.That(FindImpact(cues, "enemy_block").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.BlockImpact));
        Assert.That(FindImpact(cues, "enemy_crit").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.CriticalImpact));
        Assert.That(FindImpact(cues, "enemy_crit").AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy));
    }

    [Test]
    public void Build_DoesNotInferCommitSemanticFromProfile_ForPlainMeleeAttack()
    {
        // The old builder read a "profile_lunge" Note and produced a DashEngage commit. That inference
        // is removed (GPT Pro J8): a plain melee basic attack commit carries no semantic, and no cue
        // carries a profile_* note. The pre-impact lunge, when any, comes from the motion-intent trace.
        var previous = CreateStep();
        var current = CreateStep(combatEvents: new[]
        {
            Started("ally", CombatEventKind.BasicAttack),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        var commit = cues.Single(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally");
        Assert.That(commit.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.None));
        Assert.That(cues.Any(cue => cue.Note.Contains("profile_")), Is.False, "no cue may carry a profile_* note (no string inference).");
    }

    [Test]
    public void Build_MapsRangerBasicAttack_ToBowShotAnimationSemantic()
    {
        var units = new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", classId: "ranger", preferredRangeMin: 5.0f, preferredRangeMax: 5.8f, archetypeId: "marksman"),
            CreateUnit("enemy", TeamSide.Enemy),
        };
        var previous = CreateStep(units: units);
        var current = CreateStep(units: units, combatEvents: new[]
        {
            Started("ally", CombatEventKind.BasicAttack),
        });

        var commit = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally");
        Assert.That(commit.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.BowShot));
        Assert.That(commit.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(commit.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Medium));
    }

    [Test]
    public void Build_FiresActorCommitAtWindupStart_WithContactSchedule()
    {
        // GPT Pro D2 CommitCueFiresAtWindupStart: the actor commit fires at the Started/windup step and
        // carries the tick schedule (ActionInstanceId + WindupStartTick + ContactTick) so the driver can
        // pin the contact frame onto the damage tick. A ranger's commit is the BowShot — its draw is the
        // first part of the same clip — so no separate windup cue is emitted any more.
        var units = new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", classId: "ranger", preferredRangeMin: 5.0f, preferredRangeMax: 5.8f, archetypeId: "marksman"),
            CreateUnit("enemy", TeamSide.Enemy),
        };
        var previous = CreateStep(units: units);
        var cues = new BattlePresentationCueBuilder().Build(previous, CreateStep(units: units, combatEvents: new[] { Started("ally") }));

        var cue = cues.Single(candidate => candidate.CueType == BattlePresentationCueType.ActionCommitBasic && candidate.SubjectActorId == "ally");
        Assert.That(cue.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.BowShot));
        Assert.That(cue.CommitSchedule, Is.Not.Null);
        Assert.That(cue.CommitSchedule!.WindupStartTick, Is.EqualTo(1));
        Assert.That(cue.CommitSchedule!.ContactTick, Is.EqualTo(2));
        Assert.That(cue.CommitSchedule!.ActionInstanceId, Is.EqualTo(new ActionInstanceId(1)));
        Assert.That(cue.CommitSchedule!.ContactGroupIndex, Is.EqualTo(0));
        Assert.That(cues.Any(c => c.CueType == BattlePresentationCueType.WindupEnter), Is.False, "the commit clip spans the windup; no separate windup cue.");
    }

    [Test]
    public void Build_MapsMysticBasicAttack_ToProjectileCastAnimationSemantic()
    {
        var units = new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", classId: "mystic", preferredRangeMin: 2.1f, preferredRangeMax: 2.9f, archetypeId: "hexer"),
            CreateUnit("enemy", TeamSide.Enemy),
        };
        var previous = CreateStep(units: units);
        var current = CreateStep(units: units, combatEvents: new[]
        {
            Started("ally", CombatEventKind.BasicAttack),
        });

        var commit = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally");
        Assert.That(commit.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.ProjectileCast));
        Assert.That(commit.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(commit.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Medium));
    }

    [Test]
    public void Build_EmitsNoActorCommit_AtContactedStep()
    {
        // GPT Pro D2 NoLegacyActorCommitOnContacted: Contacted emits target reactions ONLY. The actor
        // commit fired at Started; re-emitting it here would double the swing. No commit may leak through
        // event inference, note parsing, or a contact-intent fallback.
        var previous = CreateStep();
        var current = CreateStep(combatEvents: new[]
        {
            Contacted("ally", CombatEventKind.BasicAttack, Contact("enemy", CombatOutcome.Hit, 12f)),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic), Is.False);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitSkill), Is.False);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitHeal), Is.False);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == "enemy"), Is.True,
            "the target reaction is still emitted at Contacted.");
    }

    [Test]
    public void Build_TreatsMeleeRangeMysticBasicAttack_AsNonProjectile()
    {
        var units = new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", classId: "mystic", preferredRangeMin: 0.6f, preferredRangeMax: 1.3f, archetypeId: "priest"),
            CreateUnit("enemy", TeamSide.Enemy),
        };
        var previous = CreateStep(units: units);
        var current = CreateStep(units: units, combatEvents: new[]
        {
            Started("ally", CombatEventKind.BasicAttack),
        });

        var commit = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally");
        Assert.That(commit.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.None));
    }

    [Test]
    public void Build_EmitsPreImpactDisplacementTraceCue_WhenProfileAttackMovesInsideOneTick()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.ExecuteAction, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1.2f, 0f)),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.Recover, position: new CombatVector2(0.66f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1.2f, 0f)),
            },
            motions: new[]
            {
                new BattleMotionIntent(1, 0, new EntityId("ally"), BattleMotionKind.Approach, new CombatVector2(0f, 0f), new CombatVector2(0.66f, 0f), null, true),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        var trace = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally");
        Assert.That(trace.Magnitude, Is.EqualTo(0.66f).Within(0.001f));
        Assert.That(trace.Note, Does.Contain("trace_preimpact"));
        Assert.That(trace.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.DashEngage));
        Assert.That(trace.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(trace.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Medium));
    }

    [Test]
    public void Build_MapsBreakContact_ToBackstepAnimationSemantic()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1f, 0f)),
        });
        var current = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.BreakContact, position: new CombatVector2(-0.75f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1f, 0f)),
        });

        var cue = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(candidate => candidate.CueType == BattlePresentationCueType.RepositionStart && candidate.SubjectActorId == "ally");

        Assert.That(cue.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.BackstepDisengage));
        Assert.That(cue.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Backward));
        Assert.That(cue.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Medium));
    }

    [Test]
    public void Build_MapsRepositionDirection_ToEngageAndLateralAnimationSemantics()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally_engage", TeamSide.Ally, targetId: "enemy_engage", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy_engage", TeamSide.Enemy, targetId: "ally_engage", position: new CombatVector2(2f, 0f)),
            CreateUnit("ally_lateral", TeamSide.Ally, targetId: "enemy_lateral", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy_lateral", TeamSide.Enemy, targetId: "ally_lateral", position: new CombatVector2(2f, 0f)),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("ally_engage", TeamSide.Ally, targetId: "enemy_engage", actionState: CombatActionState.Reposition, position: new CombatVector2(0.75f, 0f)),
                CreateUnit("enemy_engage", TeamSide.Enemy, targetId: "ally_engage", position: new CombatVector2(2f, 0f)),
                CreateUnit("ally_lateral", TeamSide.Ally, targetId: "enemy_lateral", actionState: CombatActionState.Reposition, position: new CombatVector2(0f, 0.75f)),
                CreateUnit("enemy_lateral", TeamSide.Enemy, targetId: "ally_lateral", position: new CombatVector2(2f, 0f)),
            },
            motions: new[]
            {
                new BattleMotionIntent(1, 0, new EntityId("ally_engage"), BattleMotionKind.Approach, new CombatVector2(0f, 0f), new CombatVector2(0.75f, 0f)),
                new BattleMotionIntent(1, 1, new EntityId("ally_lateral"), BattleMotionKind.Reposition, new CombatVector2(0f, 0f), new CombatVector2(0f, 0.75f)),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);
        var engage = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally_engage");
        var lateral = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally_lateral");

        Assert.That(engage.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.DashEngage));
        Assert.That(engage.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(lateral.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.LateralStrafe));
        Assert.That(lateral.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Left));
    }

    [Test]
    public void Build_MapsHeavyImpactAndKnockdown_ToDistinctAnimationSemantics()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally),
            CreateUnit("enemy_heavy", TeamSide.Enemy),
            CreateUnit("enemy_knockdown", TeamSide.Enemy),
        });
        var current = CreateStep(
            units: previous.Units,
            combatEvents: new[]
            {
                Contacted("ally", CombatEventKind.BasicAttack,
                    Contact("enemy_heavy", CombatOutcome.Hit, 18f, index: 0),
                    Contact("enemy_knockdown", CombatOutcome.Knockdown, 6f, index: 1)),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(FindImpact(cues, "enemy_heavy").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.HitHeavy));
        Assert.That(FindImpact(cues, "enemy_heavy").AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy));
        Assert.That(FindImpact(cues, "enemy_knockdown").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.Knockdown));
        Assert.That(FindImpact(cues, "enemy_knockdown").AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy));
    }

    [Test]
    public void Build_EmitsKnockbackTrace_WhenContactTargetDisplaces()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1f, 0f)),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("ally", TeamSide.Ally, targetId: "enemy", position: new CombatVector2(0f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1.45f, 0f)),
            },
            combatEvents: new[]
            {
                Contacted("ally", CombatEventKind.BasicAttack, Contact("enemy", CombatOutcome.Crit, 12f)),
            },
            motions: new[]
            {
                new BattleMotionIntent(1, 0, new EntityId("enemy"), BattleMotionKind.Knockback, new CombatVector2(1f, 0f), new CombatVector2(1.45f, 0f), new EntityId("ally"), true),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        var trace = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "enemy");
        Assert.That(trace.RelatedActorId, Is.EqualTo("ally"));
        Assert.That(trace.Magnitude, Is.EqualTo(0.45f).Within(0.001f));
        Assert.That(trace.Note, Does.Contain("trace_knockback"));
        Assert.That(trace.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.BackstepDisengage));
        Assert.That(trace.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Backward));
        Assert.That(trace.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy));
    }

    [Test]
    public void Build_DoesNotEmitKnockbackTrace_ForMissOrTinyTargetDelta()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy_miss", position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy_miss", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1f, 0f)),
            CreateUnit("enemy_tiny", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(2f, 0f)),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("ally", TeamSide.Ally, targetId: "enemy_miss", position: new CombatVector2(0f, 0f)),
                CreateUnit("enemy_miss", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(1.5f, 0f)),
                CreateUnit("enemy_tiny", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(2.1f, 0f)),
            },
            combatEvents: new[]
            {
                Contacted("ally", CombatEventKind.BasicAttack,
                    Contact("enemy_miss", CombatOutcome.Miss, 0f, index: 0),
                    Contact("enemy_tiny", CombatOutcome.Hit, 8f, index: 1)),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "enemy_miss"), Is.False);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "enemy_tiny"), Is.False);
    }

    [Test]
    public void Build_EmitsSingleActorCommit_ButOneReactionPerTarget_ForAoeContactGroup()
    {
        // GPT Pro J22-D2: one scheduled commit group (a single Started) emits exactly one actor commit at
        // the windup; the AOE Contacted (a ContactGroupIndex spanning N targets) emits one impact reaction
        // per target and NO actor commit.
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally),
            CreateUnit("enemy_a", TeamSide.Enemy),
            CreateUnit("enemy_b", TeamSide.Enemy),
            CreateUnit("enemy_c", TeamSide.Enemy),
        });
        var current = CreateStep(
            units: previous.Units,
            combatEvents: new[]
            {
                Started("ally", CombatEventKind.Skill),
                Contacted("ally", CombatEventKind.Skill,
                    Contact("enemy_a", CombatOutcome.Hit, 10f, index: 0, group: 0),
                    Contact("enemy_b", CombatOutcome.Hit, 10f, index: 1, group: 0),
                    Contact("enemy_c", CombatOutcome.Crit, 20f, index: 2, group: 0)),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Count(cue => cue.CueType == BattlePresentationCueType.ActionCommitSkill && cue.SubjectActorId == "ally"), Is.EqualTo(1));
        Assert.That(cues.Count(cue => cue.CueType == BattlePresentationCueType.ImpactDamage), Is.EqualTo(3));
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == "enemy_c" && cue.AnimationSemantic == BattleAnimationSemantic.CriticalImpact), Is.True);
    }

    [Test]
    public void Build_EmitsCanceledTombstone_ButNoCommitOrImpact_ForCanceledIntent()
    {
        // GPT Pro D2-C1: a windup canceled before contact produces NO commit / impact / reaction (no
        // ghost), but DOES emit an ActionCanceled tombstone (keyed by ActionInstanceId) so the driver can
        // interrupt the scheduled commit one-shot.
        var previous = CreateStep();
        var current = CreateStep(combatEvents: new[] { Canceled("ally") });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic), Is.False);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactDamage), Is.False);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactHeal), Is.False);

        var canceled = cues.Single(cue => cue.CueType == BattlePresentationCueType.ActionCanceled && cue.SubjectActorId == "ally");
        Assert.That(canceled.CommitSchedule, Is.Not.Null);
        Assert.That(canceled.CommitSchedule!.ActionInstanceId, Is.EqualTo(new ActionInstanceId(1)));
    }

    [Test]
    public void Build_ReturnsDeterministicCuePayloads_ForSameStepAndIntents()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(2f, 0f)),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("ally", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.Reposition, position: new CombatVector2(0.75f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, targetId: "ally", position: new CombatVector2(2f, 0f)),
            },
            combatEvents: new[]
            {
                Started("ally", CombatEventKind.BasicAttack),
                Contacted("ally", CombatEventKind.BasicAttack, Contact("enemy", CombatOutcome.Crit, 24f)),
            });

        var first = new BattlePresentationCueBuilder().Build(previous, current);
        var second = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(first.Select(DescribeCue), Is.EqualTo(second.Select(DescribeCue)));
    }

    [Test]
    public void Build_EmitsSingleDeathCue_WhenKillEventAndStateTransitionOverlap()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy"),
            CreateUnit("enemy", TeamSide.Enemy, isAlive: true),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("ally", TeamSide.Ally, targetId: "enemy"),
                CreateUnit("enemy", TeamSide.Enemy, isAlive: false),
            },
            events: new[]
            {
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy"), "Enemy", 12f, BattleEventKind.Kill),
            });

        var deathCues = new BattlePresentationCueBuilder()
            .Build(previous, current)
            .Where(cue => cue.CueType == BattlePresentationCueType.DeathStart && cue.SubjectActorId == "enemy")
            .ToList();

        Assert.That(deathCues, Has.Count.EqualTo(1));
    }

    [Test]
    public void Build_EmitsBattleResolvedCue_WithoutForcedMovementTrace()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("ally", TeamSide.Ally, actionState: CombatActionState.Recover, position: new CombatVector2(0.8f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, isAlive: false),
        });
        var current = new BattleSimulationStep(
            StepIndex: 2,
            TimeSeconds: 0.2f,
            Units: new[]
            {
                CreateUnit("ally", TeamSide.Ally, pendingActionType: null, position: new CombatVector2(-1.1f, 0.1f)),
                CreateUnit("enemy", TeamSide.Enemy, isAlive: false),
            },
            Events: new List<BattleEvent>(),
            IsFinished: true,
            Winner: TeamSide.Ally);

        var cue = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(candidate => candidate.CueType == BattlePresentationCueType.BattleResolved && candidate.SubjectActorId == "ally");

        Assert.That(cue.Magnitude, Is.EqualTo(0f));
        Assert.That(cue.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.None));
        Assert.That(cue.Note, Is.EqualTo("battle_resolved"));
    }

    [Test]
    public void Build_UsesMotionIntentForMobility_DashEngageSemantic()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("dasher", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "dasher", position: new CombatVector2(3f, 0f)),
        });
        var current = CreateStep(
            units: new[]
            {
                CreateUnit("dasher", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.Reposition, position: new CombatVector2(1.5f, 0f)),
                CreateUnit("enemy", TeamSide.Enemy, targetId: "dasher", position: new CombatVector2(3f, 0f)),
            },
            motions: new[]
            {
                new BattleMotionIntent(1, 0, new EntityId("dasher"), BattleMotionKind.MobilityDash, new CombatVector2(0f, 0f), new CombatVector2(1.5f, 0f), null, true),
            });

        var cue = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(candidate => candidate.CueType == BattlePresentationCueType.RepositionStart && candidate.SubjectActorId == "dasher");

        Assert.That(cue.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.DashEngage));
        Assert.That(cue.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(cue.Magnitude, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(cue.Note, Does.Contain("MobilityDash"));
    }

    [Test]
    public void Build_DoesNotInferNonWalkSemanticFromPositionDelta_WhenNoMotionRecorded()
    {
        var previous = CreateStep(units: new[]
        {
            CreateUnit("mover", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.AcquireTarget, position: new CombatVector2(0f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "mover", position: new CombatVector2(2f, 0f)),
        });
        var current = CreateStep(units: new[]
        {
            CreateUnit("mover", TeamSide.Ally, targetId: "enemy", actionState: CombatActionState.Reposition, position: new CombatVector2(0.8f, 0f)),
            CreateUnit("enemy", TeamSide.Enemy, targetId: "mover", position: new CombatVector2(2f, 0f)),
        });

        var cue = new BattlePresentationCueBuilder().Build(previous, current)
            .Single(candidate => candidate.CueType == BattlePresentationCueType.RepositionStart && candidate.SubjectActorId == "mover");

        Assert.That(cue.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.LateralStrafe),
            "forward delta toward target must NOT be inferred as DashEngage without a motion intent.");
        Assert.That(cue.Note, Does.StartWith("state_"));
    }

    private static BattlePresentationCue FindImpact(IEnumerable<BattlePresentationCue> cues, string subjectActorId)
    {
        return cues.Single(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == subjectActorId);
    }

    private static string DescribeCue(BattlePresentationCue cue)
    {
        var schedule = cue.CommitSchedule is { } s ? $"{s.ActionInstanceId.Value}/{s.ContactGroupIndex}/{s.WindupStartTick}/{s.ContactTick}" : "-";
        return $"{cue.CueType}:{cue.StepIndex}:{cue.SubjectActorId}:{cue.RelatedActorId}:{cue.ActionType}:{cue.Magnitude:0.###}:{cue.Note}:{cue.AnimationSemantic}:{cue.AnimationDirection}:{cue.AnimationIntensity}:{schedule}";
    }

    private static BattleCombatEventIntent Started(string actorId, CombatEventKind kind = CombatEventKind.BasicAttack)
    {
        return new BattleCombatEventIntent(
            1, new ActionInstanceId(1), new EntityId(actorId), kind, SkillDelivery.Melee,
            1, 2, CombatEventIntentStatus.Started, null, null, null, null);
    }

    private static BattleCombatEventIntent Contacted(string actorId, CombatEventKind kind, params BattleContactIntent[] contacts)
    {
        return new BattleCombatEventIntent(
            1, new ActionInstanceId(1), new EntityId(actorId), kind, SkillDelivery.Melee,
            0, 1, CombatEventIntentStatus.Contacted,
            contacts.Length > 0 ? contacts[0].TargetId : null, null, null, contacts);
    }

    private static BattleCombatEventIntent Canceled(string actorId, CombatEventKind kind = CombatEventKind.BasicAttack)
    {
        return new BattleCombatEventIntent(
            1, new ActionInstanceId(1), new EntityId(actorId), kind, SkillDelivery.Melee,
            1, 3, CombatEventIntentStatus.Canceled, null, 1, null, null);
    }

    private static BattleContactIntent Contact(string targetId, CombatOutcome outcome, float value, bool isHeal = false, int index = 0, int group = 0)
    {
        return new BattleContactIntent(index, group, 1, new EntityId(targetId), outcome, value, isHeal);
    }

    private static BattleSimulationStep CreateStep(
        IReadOnlyList<BattleUnitReadModel>? units = null,
        IReadOnlyList<BattleEvent>? events = null,
        IReadOnlyList<BattleMotionIntent>? motions = null,
        IReadOnlyList<BattleCombatEventIntent>? combatEvents = null)
    {
        return new BattleSimulationStep(
            StepIndex: 1,
            TimeSeconds: 0.1f,
            Units: units ?? new[]
            {
                CreateUnit("ally", TeamSide.Ally, targetId: "enemy"),
                CreateUnit("enemy", TeamSide.Enemy),
                CreateUnit("healer", TeamSide.Ally, targetId: "ally", pendingActionType: BattleActionType.ActiveSkill, selector: "LowestHpAlly"),
            },
            Events: events ?? new List<BattleEvent>(),
            IsFinished: false,
            Winner: null,
            Motions: motions,
            CombatEventIntents: combatEvents);
    }

    private static BattleUnitReadModel CreateUnit(
        string id,
        TeamSide side,
        string? targetId = null,
        bool isDefending = false,
        CombatActionState actionState = CombatActionState.AcquireTarget,
        BattleActionType? pendingActionType = BattleActionType.BasicAttack,
        string selector = "LowestHpEnemy",
        bool isAlive = true,
        CombatVector2? position = null,
        string classId = "vanguard",
        float preferredRangeMin = 0f,
        float preferredRangeMax = 0f,
        string archetypeId = "")
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: classId,
            Position: position ?? (side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f)),
            CurrentHealth: isAlive ? 20f : 0f,
            MaxHealth: 20f,
            IsAlive: isAlive,
            ActionState: actionState,
            PendingActionType: pendingActionType,
            TargetId: targetId,
            TargetName: targetId,
            WindupProgress: actionState == CombatActionState.ExecuteAction ? 0.5f : 0f,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: isDefending,
            CurrentSelector: selector,
            CurrentFallback: "KeepCurrentIfStillValid",
            PreferredRangeMin: preferredRangeMin,
            PreferredRangeMax: preferredRangeMax,
            ArchetypeId: archetypeId);
    }
}
