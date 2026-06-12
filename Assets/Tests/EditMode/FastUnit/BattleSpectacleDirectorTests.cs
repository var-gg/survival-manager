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
/// Phase 4 spectacle director — beat/킬 강조의 acceptance 계약을 고정한다:
/// 카메라 컷 예산(0.8s)·기본공격 비강조·개전 보드 뷰(1.0s)·킬/클러치 close-up·콜아웃 밀도(1초 1건)·
/// 되감기 재소비 무재발화, 콤보 소비 contact 의 히트스톱 승급, 원장의 발현 요약 줄,
/// 그리고 "presentation 소비가 sim 을 1비트도 바꾸지 않는다".
/// </summary>
[Category("FastUnit")]
public sealed class BattleSpectacleDirectorTests
{
    private const double T0 = 100.0; // 임의 벽시계 기점 — 디렉터는 주입된 시계만 본다.

    [Test]
    public void CameraCut_RespectsBudget_ThenHigherImportancePreempts()
    {
        var director = new BattleSpectacleDirector();
        director.IngestStep(Step(beats: new[] { ComboConsumedBeat(sequence: 0) }), T0);

        Assert.That(director.TryGetCameraEmphasis(T0, out var first), Is.True, "콤보(80)는 카메라 강조 진입");
        Assert.That(first.ShotKind, Is.EqualTo(SpectacleShotKind.Medium), "콤보는 medium — close-up 은 킬/클러치 전용");

        // 0.8s 예산 안에서는 더 큰 사건(킬 90)도 컷하지 못한다.
        director.IngestStep(Step(stepIndex: 12, events: new[] { KillEvent("killer", "victim") }), T0 + 0.3);
        Assert.That(director.TryGetCameraEmphasis(T0 + 0.3, out var held), Is.True);
        Assert.That(held.StartedAtSeconds, Is.EqualTo(first.StartedAtSeconds), "예산 안 — 기존 샷 유지");

        // 예산이 지나면 대기 중이던 킬이 컷한다(중요도 90 > 80).
        Assert.That(director.TryGetCameraEmphasis(T0 + 0.9, out var preempted), Is.True);
        Assert.That(preempted.ShotKind, Is.EqualTo(SpectacleShotKind.Close), "킬은 close-up");
        Assert.That(preempted.StartedAtSeconds, Is.EqualTo(T0 + 0.9));
    }

    [Test]
    public void BasicAttacksAndMinorBeats_NeverStealCamera()
    {
        var director = new BattleSpectacleDirector();
        director.IngestStep(
            Step(
                events: new[] { DamageEvent("ally", "enemy", 22f) },
                beats: new[] { SynergyBeat() }), // 시너지 60 < 카메라 임계 70
            T0);

        Assert.That(director.TryGetCameraEmphasis(T0, out _), Is.False,
            "기본공격/시너지(60)는 카메라를 훔치지 못한다");
    }

    [Test]
    public void OpeningBoardView_SuppressesEmphasis_DuringFirstSimSecond()
    {
        var director = new BattleSpectacleDirector();
        director.IngestStep(Step(timeSeconds: 0.5f, beats: new[] { ComboConsumedBeat() }), T0);
        Assert.That(director.TryGetCameraEmphasis(T0, out _), Is.False, "개전 1.0s 는 전술 보드 뷰 고정");

        director.IngestStep(Step(timeSeconds: 1.5f, stepIndex: 15, beats: new[] { ComboConsumedBeat(stepIndex: 15) }), T0 + 1.0);
        Assert.That(director.TryGetCameraEmphasis(T0 + 1.0, out _), Is.True, "1.0s 이후엔 강조 정상");
    }

    [Test]
    public void ClutchBeat_GetsCloseShot_AndEmphasisExpires()
    {
        var director = new BattleSpectacleDirector();
        director.IngestStep(Step(beats: new[] { ClutchBeat() }), T0);

        Assert.That(director.TryGetCameraEmphasis(T0, out var emphasis), Is.True);
        Assert.That(emphasis.ShotKind, Is.EqualTo(SpectacleShotKind.Close), "클러치(빈사 발동)는 close-up");

        var afterHold = T0 + BattleSpectacleDirector.EmphasisHoldSeconds + 0.1;
        Assert.That(director.TryGetCameraEmphasis(afterHold, out _), Is.False, "강조는 만료 후 보드 뷰로 복귀");
    }

    [Test]
    public void DuplicateDelivery_OfSameBeats_DoesNotRefire_ButResetAllowsRewatch()
    {
        var director = new BattleSpectacleDirector();
        var step = Step(beats: new[] { ComboConsumedBeat() });
        director.IngestStep(step, T0);
        Assert.That(director.TryGetCameraEmphasis(T0, out _), Is.True);

        // 같은 재생 흐름 안에서 같은 step 이 중복 전달되어도 같은 beat 은 재발화하지 않는다(보험).
        var later = T0 + 5.0;
        director.IngestStep(step, later);
        Assert.That(director.TryGetCameraEmphasis(later, out _), Is.False, "(StepIndex, Sequence) dedup");

        // 시크/되감기(Reset) 후 재생은 의도적 재발화 — 재시청은 하이라이트를 다시 본다.
        director.Reset();
        var rewatch = later + 1.0;
        director.IngestStep(step, rewatch);
        Assert.That(director.TryGetCameraEmphasis(rewatch, out _), Is.True, "Reset 은 dedup 까지 청산한다");
    }

    [Test]
    public void ConsecutiveEqualImportanceKills_SecondCloseUpShowsAfterHoldExpiry()
    {
        // 데드존 회귀 가드: stale(1.6s) > hold(1.3s) 라서 컷 직후 적재된 동급 후보(연쇄 킬)가
        // 활성 샷 만료 시점에 승격될 수 있어야 한다.
        var director = new BattleSpectacleDirector();
        director.IngestStep(Step(events: new[] { KillEvent("killer_a", "victim_a") }), T0);
        Assert.That(director.TryGetCameraEmphasis(T0, out var first), Is.True);

        director.IngestStep(Step(stepIndex: 12, events: new[] { KillEvent("killer_b", "victim_b") }), T0 + 0.05);

        var afterHold = T0 + BattleSpectacleDirector.EmphasisHoldSeconds + 0.05;
        Assert.That(director.TryGetCameraEmphasis(afterHold, out var second), Is.True,
            "연쇄 킬의 두 번째 close-up 이 hold 만료 후 승격되어야 한다");
        Assert.That(second.StartedAtSeconds, Is.GreaterThan(first.StartedAtSeconds));
        Assert.That(second.SubjectUnitId, Is.EqualTo("victim_b"));
    }

    [Test]
    public void Callouts_AreGatedToOnePerSecond_HighestImportanceFirst()
    {
        var director = new BattleSpectacleDirector();
        director.IngestStep(
            Step(beats: new[]
            {
                SynergyBeat(sequence: 0),
                ComboConsumedBeat(sequence: 1),
                ComboPrimerBeat(sequence: 2), // 55 < 콜아웃 임계 60 — 진입 불가
            }),
            T0);

        Assert.That(director.TryDrainCallout(T0, out var first), Is.True);
        Assert.That(first.Type, Is.EqualTo(CombatBeatType.ComboConsumed), "같은 순간엔 중요도 높은 사건 먼저");
        Assert.That(director.TryDrainCallout(T0 + 0.5, out _), Is.False, "1초 1건 게이트");
        Assert.That(director.TryDrainCallout(T0 + 1.1, out var second), Is.True);
        Assert.That(second.Type, Is.EqualTo(CombatBeatType.SynergyActivated));
        Assert.That(director.TryDrainCallout(T0 + 2.3, out _), Is.False, "프라이머(55)는 콜아웃 진입 불가");
    }

    [Test]
    public void Reset_ClearsActiveEmphasisAndPending()
    {
        var director = new BattleSpectacleDirector();
        director.IngestStep(Step(beats: new[] { ComboConsumedBeat() }), T0);
        Assert.That(director.TryGetCameraEmphasis(T0, out _), Is.True);

        director.Reset();
        Assert.That(director.TryGetCameraEmphasis(T0 + 0.01, out _), Is.False, "되감기/재시작 후 stale 강조 금지");
        Assert.That(director.TryDrainCallout(T0 + 0.01, out _), Is.False);
    }

    [Test]
    public void CueBuilder_PromotesComboConsumedContact_ToComboEmphasis()
    {
        var previous = Step(stepIndex: 0);
        var current = new BattleSimulationStep(
            StepIndex: 1,
            TimeSeconds: 0.1f,
            Units: DefaultUnits(),
            Events: new List<BattleEvent>(),
            IsFinished: false,
            Winner: null,
            CombatEventIntents: new[]
            {
                Contacted("ally", Contact("enemy", CombatOutcome.Hit, 6f)),
                Contacted("healer", Contact("enemy", CombatOutcome.Hit, 5f)),
            },
            Beats: new[] { ComboConsumedBeat(stepIndex: 1, sourceId: "ally", targetId: "enemy") });

        var cues = new BattlePresentationCueBuilder().Build(previous, current);
        var comboImpact = cues.Single(cue =>
            cue.CueType == BattlePresentationCueType.ImpactDamage && cue.RelatedActorId == "ally");
        var plainImpact = cues.Single(cue =>
            cue.CueType == BattlePresentationCueType.ImpactDamage && cue.RelatedActorId == "healer");

        Assert.That(comboImpact.IsComboEmphasis, Is.True, "ComboConsumed beat 과 (공격자,피격자) 매칭 contact 만 승급");
        Assert.That(comboImpact.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Heavy),
            "콤보 작렬은 Heavy — 보통 타격 baseline(Medium 90ms)보다 한 단계 굵은 펀치");
        Assert.That(plainImpact.IsComboEmphasis, Is.False);
        Assert.That(plainImpact.AnimationIntensity, Is.EqualTo(BattleAnimationIntensity.Medium), "보통 타격은 비승급(기존 baseline)");
    }

    [Test]
    public void HighlightLedger_AppendsManifestationLine_FromAllyBeats()
    {
        var ledger = new BattleHighlightLedger();
        ledger.Record(Step(beats: new[]
        {
            SynergyBeat(sequence: 0),
            ComboConsumedBeat(sequence: 1),
            TriggeredBeat(sequence: 2),
            ComboConsumedBeat(sequence: 3, side: TeamSide.Enemy), // 적측 — 아군 관점 요약에서 제외
        }));

        var timeline = new List<string>();
        Assert.That(ledger.TryAppendBattleEndLines(FinishedStep(), timeline, id => id), Is.True);
        var manifest = timeline.SingleOrDefault(line => line.StartsWith("발현 |", System.StringComparison.Ordinal));
        Assert.That(manifest, Is.Not.Null, $"timeline: {string.Join(" / ", timeline)}");
        Assert.That(manifest, Does.Contain("시너지 발동 1").And.Contain("콤보 작렬 1").And.Contain("증강 발동 1"));
    }

    [Test]
    public void DirectorConsumption_DoesNotPerturbSimOutcome()
    {
        // acceptance: "presentation 켜든 끄든 sim 동일" — 같은 seed 두 sim, 한쪽만 디렉터+cue 빌더가
        // 모든 step 을 소비. 최종 유닛 상태(HP/위치)와 step 수가 byte-equal 이어야 한다.
        static BattleSimulator CreateSim()
        {
            var state = CombatTestFactory.CreateBattleState(
                new[]
                {
                    CombatTestFactory.CreateLoopAUnit("ally_a", hp: 60f),
                    CombatTestFactory.CreateLoopAUnit("ally_b", anchor: DeploymentAnchorId.FrontTop, hp: 60f),
                },
                new[]
                {
                    CombatTestFactory.CreateLoopAUnit("enemy_a", hp: 60f),
                    CombatTestFactory.CreateLoopAUnit("enemy_b", anchor: DeploymentAnchorId.FrontTop, hp: 60f),
                },
                seed: 31);
            return new BattleSimulator(state, 150);
        }

        var control = CreateSim();
        while (!control.IsFinished)
        {
            control.Step();
        }

        var observed = CreateSim();
        var director = new BattleSpectacleDirector();
        var cueBuilder = new BattlePresentationCueBuilder();
        var clock = T0;
        var previous = observed.CurrentStep;
        while (!observed.IsFinished)
        {
            var current = observed.Step();
            director.IngestStep(current, clock);
            director.TryGetCameraEmphasis(clock, out _);
            director.TryDrainCallout(clock, out _);
            cueBuilder.Build(previous, current);
            previous = current;
            clock += 0.016;
        }

        Assert.That(observed.State.StepIndex, Is.EqualTo(control.State.StepIndex), "step 수 동일");
        var controlUnits = control.CurrentStep.Units;
        var observedUnits = observed.CurrentStep.Units;
        Assert.That(observedUnits.Count, Is.EqualTo(controlUnits.Count));
        for (var i = 0; i < controlUnits.Count; i++)
        {
            Assert.That(observedUnits[i].CurrentHealth, Is.EqualTo(controlUnits[i].CurrentHealth), $"HP[{controlUnits[i].Id}]");
            Assert.That(observedUnits[i].Position.X, Is.EqualTo(controlUnits[i].Position.X), $"X[{controlUnits[i].Id}]");
            Assert.That(observedUnits[i].Position.Y, Is.EqualTo(controlUnits[i].Position.Y), $"Y[{controlUnits[i].Id}]");
        }
    }

    private static IReadOnlyList<BattleUnitReadModel> DefaultUnits()
    {
        return new[]
        {
            Unit("ally", TeamSide.Ally, targetId: "enemy"),
            Unit("healer", TeamSide.Ally, targetId: "enemy"),
            Unit("enemy", TeamSide.Enemy),
        };
    }

    private static BattleSimulationStep Step(
        int stepIndex = 11,
        float timeSeconds = 1.1f,
        IReadOnlyList<BattleEvent>? events = null,
        IReadOnlyList<CombatBeat>? beats = null)
    {
        return new BattleSimulationStep(
            StepIndex: stepIndex,
            TimeSeconds: timeSeconds,
            Units: DefaultUnits(),
            Events: events ?? new List<BattleEvent>(),
            IsFinished: false,
            Winner: null,
            Beats: beats);
    }

    private static BattleSimulationStep FinishedStep()
    {
        return new BattleSimulationStep(
            StepIndex: 40,
            TimeSeconds: 4.0f,
            Units: DefaultUnits(),
            Events: new List<BattleEvent>(),
            IsFinished: true,
            Winner: TeamSide.Ally);
    }

    private static CombatBeat ComboConsumedBeat(
        int stepIndex = 11,
        int sequence = 0,
        string sourceId = "ally",
        string targetId = "enemy",
        TeamSide side = TeamSide.Ally)
    {
        return new CombatBeat(
            stepIndex, sequence, CombatBeatType.ComboConsumed, side,
            new EntityId(sourceId), new EntityId(targetId), ChainId: 1,
            CombatBeatImportance.ComboConsumed, 8f, new CombatVector2(1f, 0f), "marked");
    }

    private static CombatBeat ComboPrimerBeat(int stepIndex = 11, int sequence = 0)
    {
        return new CombatBeat(
            stepIndex, sequence, CombatBeatType.ComboPrimerApplied, TeamSide.Ally,
            new EntityId("ally"), new EntityId("enemy"), ChainId: 1,
            CombatBeatImportance.ComboPrimerApplied, 0f, new CombatVector2(1f, 0f), "marked");
    }

    private static CombatBeat SynergyBeat(int stepIndex = 11, int sequence = 0)
    {
        return new CombatBeat(
            stepIndex, sequence, CombatBeatType.SynergyActivated, TeamSide.Ally,
            null, null, ChainId: 0,
            CombatBeatImportance.SynergyActivated, 0f, new CombatVector2(-1f, 0f), "race:human:2");
    }

    private static CombatBeat ClutchBeat(int stepIndex = 11, int sequence = 0)
    {
        return new CombatBeat(
            stepIndex, sequence, CombatBeatType.HpThresholdEffect, TeamSide.Ally,
            new EntityId("ally"), new EntityId("ally"), ChainId: 0,
            CombatBeatImportance.HpThresholdEffect, 30f, new CombatVector2(-1f, 0f), "aug_last_stand");
    }

    private static CombatBeat TriggeredBeat(int stepIndex = 11, int sequence = 0)
    {
        return new CombatBeat(
            stepIndex, sequence, CombatBeatType.BattleStartEffect, TeamSide.Ally,
            new EntityId("ally"), new EntityId("ally"), ChainId: 0,
            CombatBeatImportance.BattleStartEffect, 25f, new CombatVector2(-1f, 0f), "aug_opening_bulwark");
    }

    private static BattleEvent DamageEvent(string actorId, string targetId, float value)
    {
        return new BattleEvent(
            11, 1.1f, new EntityId(actorId), actorId, BattleActionType.BasicAttack, BattleLogCode.BasicAttackDamage,
            new EntityId(targetId), targetId, value);
    }

    private static BattleEvent KillEvent(string actorId, string targetId)
    {
        return new BattleEvent(
            12, 1.2f, new EntityId(actorId), actorId, BattleActionType.BasicAttack, BattleLogCode.Generic,
            new EntityId(targetId), targetId, 0f, BattleEventKind.Kill, string.Empty, 0f, "kill",
            new KillEventPayload
            {
                ActualKiller = new EntityId(actorId),
                ActualVictim = new EntityId(targetId),
            });
    }

    private static BattleCombatEventIntent Contacted(string actorId, params BattleContactIntent[] contacts)
    {
        return new BattleCombatEventIntent(
            1, new ActionInstanceId(1), new EntityId(actorId), CombatEventKind.BasicAttack, SkillDelivery.Melee,
            0, 1, CombatEventIntentStatus.Contacted,
            contacts.Length > 0 ? contacts[0].TargetId : null, null, null, contacts);
    }

    private static BattleContactIntent Contact(string targetId, CombatOutcome outcome, float value)
    {
        return new BattleContactIntent(0, 0, 1, new EntityId(targetId), outcome, value, false);
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
