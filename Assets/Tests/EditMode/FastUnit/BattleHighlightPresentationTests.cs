using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Unity;

namespace SM.Tests.EditMode;

/// <summary>
/// P2 보는 맛 — P0 detector 사실이 typed accent 채널(J8: note 문자열 비파싱)로 화면까지 흐르는 계약.
/// sim self-label 매핑, cue 전달, 극적 카메라 포커스 우선순위, MVP/하이라이트 원장.
/// </summary>
[Category("FastUnit")]
public sealed class BattleHighlightPresentationTests
{
    [Test]
    public void Simulator_SelfLabelsP0NoteTokens_AsTypedAccents()
    {
        Assert.That(BattleSimulator.ResolveAccent(DamageEvent("a", "b", 10f, note: "crit+rear")),
            Is.EqualTo(CombatContactAccent.Rear));
        Assert.That(BattleSimulator.ResolveAccent(DamageEvent("a", "b", 10f, note: "flank")),
            Is.EqualTo(CombatContactAccent.Flank));
        Assert.That(BattleSimulator.ResolveAccent(DamageEvent("a", "b", 10f, note: "screened+flank")),
            Is.EqualTo(CombatContactAccent.Flank | CombatContactAccent.Screened));
        Assert.That(BattleSimulator.ResolveAccent(HealEvent("a", "b", 8f, note: "save_moment")),
            Is.EqualTo(CombatContactAccent.SaveMoment));
        Assert.That(BattleSimulator.ResolveAccent(DamageEvent("a", "b", 10f, note: "crit")),
            Is.EqualTo(CombatContactAccent.None));
    }

    [Test]
    public void CueBuilder_CarriesAccent_ToImpactCues_AndPromotesRearToHeavy()
    {
        var previous = Step();
        var current = Step(combatEvents: new[]
        {
            Contacted("ally", Contact("enemy", CombatOutcome.Hit, 6f, accent: CombatContactAccent.Rear)),
            Contacted("healer", Contact("ally", CombatOutcome.Hit, 8f, isHeal: true, accent: CombatContactAccent.SaveMoment)),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);
        var impact = cues.Single(cue => cue.CueType == BattlePresentationCueType.ImpactDamage && cue.SubjectActorId == "enemy");
        var heal = cues.Single(cue => cue.CueType == BattlePresentationCueType.ImpactHeal && cue.SubjectActorId == "ally");

        Assert.That(impact.ContactAccent, Is.EqualTo(CombatContactAccent.Rear));
        Assert.That(impact.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy), "후방 타격은 히트스톱 강도 Heavy로 승급");
        Assert.That(heal.ContactAccent, Is.EqualTo(CombatContactAccent.SaveMoment));
    }

    [Test]
    public void CueBuilder_MarksDiveKillDeathCue_FromTypedKillPayload()
    {
        var previous = Step();
        var current = Step(events: new[]
        {
            KillEvent("ally", "enemy", isBacklineDiveKill: true),
        });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);
        var death = cues.Single(cue => cue.CueType == BattlePresentationCueType.DeathStart && cue.SubjectActorId == "enemy");

        Assert.That(death.ContactAccent, Is.EqualTo(CombatContactAccent.BacklineDiveKill));
    }

    [Test]
    public void ActorView_AccentSuffix_ComposesPositionalTokens()
    {
        Assert.That(BattleActorView.ResolveAccentSuffix(CombatContactAccent.Rear | CombatContactAccent.Screened),
            Is.EqualTo(" 후방! 차단"));
        Assert.That(BattleActorView.ResolveAccentSuffix(CombatContactAccent.Flank), Is.EqualTo(" 측면"));
        Assert.That(BattleActorView.ResolveAccentSuffix(CombatContactAccent.None), Is.Empty);
    }

    [Test]
    public void StepFocus_PrefersDramaticEvent_OverLastEvent()
    {
        var step = Step(events: new[]
        {
            KillEvent("killer", "victim"),
            DamageEvent("late_attacker", "other", 5f),
        });

        Assert.That(BattleReadabilityFormatter.TryResolveStepFocus(step, out var focus), Is.True);
        Assert.That(focus.ActorId, Is.EqualTo("killer"), "마지막 이벤트가 아니라 킬이 포커스를 가진다");
        Assert.That(focus.Semantic, Is.EqualTo(BattleActionSemantic.Down));

        var diveStep = Step(events: new[]
        {
            KillEvent("dive_killer", "backliner", isBacklineDiveKill: true),
            KillEvent("plain_killer", "other"),
        });
        Assert.That(BattleReadabilityFormatter.TryResolveStepFocus(diveStep, out var diveFocus), Is.True);
        Assert.That(diveFocus.ActorId, Is.EqualTo("dive_killer"), "다이브 킬 > 일반 킬");
    }

    [Test]
    public void HighlightLedger_BuildsMvpAndHighlightLines_OnceAtBattleEnd()
    {
        var ledger = new BattleHighlightLedger();
        var fightingStep = Step(
            events: new[]
            {
                DamageEvent("ally", "enemy", 30f),
                HealEvent("healer", "ally", 10f),
                DamageEvent("enemy", "ally", 50f), // 적측 — MVP 집계 제외
                KillEvent("ally", "enemy", isBacklineDiveKill: true),
            },
            combatEvents: new[]
            {
                Contacted("ally", Contact("enemy", CombatOutcome.Hit, 30f, accent: CombatContactAccent.Rear)),
                Contacted("healer", Contact("ally", CombatOutcome.Hit, 10f, isHeal: true, accent: CombatContactAccent.SaveMoment)),
            });
        ledger.Record(fightingStep);

        var finishedStep = Step(isFinished: true);
        var timeline = new List<string>();
        Assert.That(ledger.TryAppendBattleEndLines(finishedStep, timeline, id => id), Is.True);
        Assert.That(timeline.Any(line => line.StartsWith("MVP ally", System.StringComparison.Ordinal)), Is.True,
            $"timeline: {string.Join(" / ", timeline)}");
        Assert.That(timeline.Any(line => line.Contains("후방 강타 1") && line.Contains("구출 1") && line.Contains("후열 격파 1")), Is.True,
            $"timeline: {string.Join(" / ", timeline)}");
        Assert.That(ledger.TryAppendBattleEndLines(finishedStep, timeline, id => id), Is.False, "종료 요약은 1회만");
    }

    private static BattleEvent DamageEvent(string actorId, string targetId, float value, string note = "")
    {
        return new BattleEvent(
            1, 0.1f, new EntityId(actorId), actorId, BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage,
            new EntityId(targetId), targetId, value, BattleEventKind.Action, string.Empty, 0f, note);
    }

    private static BattleEvent HealEvent(string actorId, string targetId, float value, string note = "")
    {
        return new BattleEvent(
            1, 0.1f, new EntityId(actorId), actorId, BattleActionType.ActiveSkill, BattleLogCode.ActiveSkillHeal,
            new EntityId(targetId), targetId, value, BattleEventKind.Action, string.Empty, 0f, note);
    }

    private static BattleEvent KillEvent(string actorId, string targetId, bool isBacklineDiveKill = false)
    {
        return new BattleEvent(
            1, 0.1f, new EntityId(actorId), actorId, BattleActionType.BasicAttack, BattleLogCode.Generic,
            new EntityId(targetId), targetId, 0f, BattleEventKind.Kill, string.Empty, 0f, "kill",
            new KillEventPayload
            {
                ActualKiller = new EntityId(actorId),
                ActualVictim = new EntityId(targetId),
                IsBacklineDiveKill = isBacklineDiveKill,
            });
    }

    private static BattleCombatEventIntent Contacted(string actorId, params BattleContactIntent[] contacts)
    {
        return new BattleCombatEventIntent(
            1, new ActionInstanceId(1), new EntityId(actorId), CombatEventKind.BasicAttack, SkillDelivery.Melee,
            0, 1, CombatEventIntentStatus.Contacted,
            contacts.Length > 0 ? contacts[0].TargetId : null, null, null, contacts);
    }

    private static BattleContactIntent Contact(
        string targetId,
        CombatOutcome outcome,
        float value,
        bool isHeal = false,
        CombatContactAccent accent = CombatContactAccent.None)
    {
        return new BattleContactIntent(0, 0, 1, new EntityId(targetId), outcome, value, isHeal, accent);
    }

    private static BattleSimulationStep Step(
        IReadOnlyList<BattleEvent>? events = null,
        IReadOnlyList<BattleCombatEventIntent>? combatEvents = null,
        bool isFinished = false)
    {
        return new BattleSimulationStep(
            StepIndex: 1,
            TimeSeconds: 0.1f,
            Units: new[]
            {
                Unit("ally", TeamSide.Ally, targetId: "enemy"),
                Unit("healer", TeamSide.Ally, targetId: "ally"),
                Unit("enemy", TeamSide.Enemy),
            },
            Events: events ?? new List<BattleEvent>(),
            IsFinished: isFinished,
            Winner: isFinished ? TeamSide.Ally : null,
            Motions: null,
            CombatEventIntents: combatEvents);
    }

    private static BattleUnitReadModel Unit(string id, TeamSide side, string? targetId = null)
    {
        return new BattleUnitReadModel(
            Id: id,
            Name: id,
            Side: side,
            Anchor: DeploymentAnchorId.FrontCenter,
            RaceId: "human",
            ClassId: "vanguard",
            Position: side == TeamSide.Ally ? new CombatVector2(-1f, 0f) : new CombatVector2(1f, 0f),
            CurrentHealth: 20f,
            MaxHealth: 20f,
            IsAlive: true,
            ActionState: CombatActionState.AcquireTarget,
            PendingActionType: BattleActionType.BasicAttack,
            TargetId: targetId,
            TargetName: targetId,
            WindupProgress: 0f,
            CooldownRemaining: 0f,
            CurrentEnergy: 0f,
            MaxEnergy: 100f,
            IsDefending: false,
            CurrentSelector: "LowestHpEnemy",
            ArchetypeId: "warden",
            CharacterId: "chr_0001");
    }
}
