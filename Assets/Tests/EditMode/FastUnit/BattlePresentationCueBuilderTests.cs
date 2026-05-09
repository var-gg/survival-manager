using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Core.Ids;
using SM.Unity;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BattlePresentationCueBuilderTests
{
    [Test]
    public void Build_MapsDamageAndHealEvents_ToSemanticSourceAndTargetCues()
    {
        var previous = CreateStep();
        var current = CreateStep(events: new[]
        {
            new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy"), "Enemy", 12f),
            new BattleEvent(1, 0.1f, new EntityId("healer"), "Healer", BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal, new EntityId("ally"), "Ally", 8f),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == "enemy"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.ActionCommitHeal && cue.SubjectActorId == "healer"), Is.True);
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
            CreateUnit("ally", TeamSide.Ally, targetId: "enemy_b", isDefending: true, actionState: CombatActionState.Reposition),
            CreateUnit("enemy_a", TeamSide.Enemy),
            CreateUnit("enemy_b", TeamSide.Enemy),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.TargetChanged && cue.SubjectActorId == "ally" && cue.RelatedActorId == "enemy_b"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.GuardEnter && cue.SubjectActorId == "ally"), Is.True);
        Assert.That(cues.Any(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally"), Is.True);
    }

    [Test]
    public void Build_MapsImpactNotes_ToAnimationSemantics()
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
            events: new[]
            {
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy_miss"), "Enemy Miss", 0f, Note: "miss_range"),
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy_dodge"), "Enemy Dodge", 0f, Note: "dodge"),
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy_block"), "Enemy Block", 4f, Note: "block"),
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy_crit"), "Enemy Crit", 22f, Note: "crit"),
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
    public void Build_MapsBasicAttackProfileNote_ToCommitAnimationSemantic()
    {
        var previous = CreateStep();
        var current = CreateStep(events: new[]
        {
            new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy"), "Enemy", 12f, Note: "profile_lunge"),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        var commit = cues.Single(cue => cue.CueType == BattlePresentationCueType.ActionCommitBasic && cue.SubjectActorId == "ally");
        Assert.That(commit.AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.DashEngage));
        Assert.That(commit.AnimationDirection, Is.EqualTo(BattleAnimationDirection.Forward));
        Assert.That(commit.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Medium));
        Assert.That(commit.Note, Is.EqualTo("profile_lunge"));
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
            events: new[]
            {
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy"), "Enemy", 12f, Note: "profile_lunge"),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        var trace = cues.Single(cue => cue.CueType == BattlePresentationCueType.RepositionStart && cue.SubjectActorId == "ally");
        Assert.That(trace.Magnitude, Is.EqualTo(0.66f).Within(0.001f));
        Assert.That(trace.Note, Does.Contain("trace_preimpact"));
        Assert.That(trace.Note, Does.Contain("profile_lunge"));
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

        var cue = new BattlePresentationCueBuilder()
            .Build(previous, current)
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
        var current = CreateStep(units: new[]
        {
            CreateUnit("ally_engage", TeamSide.Ally, targetId: "enemy_engage", actionState: CombatActionState.Reposition, position: new CombatVector2(0.75f, 0f)),
            CreateUnit("enemy_engage", TeamSide.Enemy, targetId: "ally_engage", position: new CombatVector2(2f, 0f)),
            CreateUnit("ally_lateral", TeamSide.Ally, targetId: "enemy_lateral", actionState: CombatActionState.Reposition, position: new CombatVector2(0f, 0.75f)),
            CreateUnit("enemy_lateral", TeamSide.Enemy, targetId: "ally_lateral", position: new CombatVector2(2f, 0f)),
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
            events: new[]
            {
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy_heavy"), "Enemy Heavy", 18f),
                new BattleEvent(1, 0.1f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy_knockdown"), "Enemy Knockdown", 6f, Note: "knockdown"),
            });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(FindImpact(cues, "enemy_heavy").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.HitHeavy));
        Assert.That(FindImpact(cues, "enemy_heavy").AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy));
        Assert.That(FindImpact(cues, "enemy_knockdown").AnimationSemantic, Is.EqualTo(BattleAnimationSemantic.Knockdown));
        Assert.That(FindImpact(cues, "enemy_knockdown").AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy));
    }

    [Test]
    public void Build_ReturnsDeterministicCuePayloads_ForSameStepAndEvents()
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
            events: new[]
            {
                new BattleEvent(2, 0.2f, new EntityId("ally"), "Ally", BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage, new EntityId("enemy"), "Enemy", 24f, Note: "crit profile_lunge"),
            });

        var first = new BattlePresentationCueBuilder().Build(previous, current);
        var second = new BattlePresentationCueBuilder().Build(previous, current);

        Assert.That(
            first.Select(DescribeCue),
            Is.EqualTo(second.Select(DescribeCue)));
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

    private static BattlePresentationCue FindImpact(IEnumerable<BattlePresentationCue> cues, string subjectActorId)
    {
        return cues.Single(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == subjectActorId);
    }

    private static string DescribeCue(BattlePresentationCue cue)
    {
        return $"{cue.CueType}:{cue.StepIndex}:{cue.SubjectActorId}:{cue.RelatedActorId}:{cue.ActionType}:{cue.Magnitude:0.###}:{cue.Note}:{cue.AnimationSemantic}:{cue.AnimationDirection}:{cue.AnimationIntensity}";
    }

    private static BattleSimulationStep CreateStep(IReadOnlyList<BattleUnitReadModel>? units = null, IReadOnlyList<BattleEvent>? events = null)
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
            Winner: null);
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
        CombatVector2? position = null)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
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
            CurrentFallback: "KeepCurrentIfStillValid");
    }
}
