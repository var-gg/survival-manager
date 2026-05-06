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
        bool isAlive = true)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: side,
            Anchor: side == TeamSide.Ally ? DeploymentAnchorId.FrontCenter : DeploymentAnchorId.BackCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f),
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
