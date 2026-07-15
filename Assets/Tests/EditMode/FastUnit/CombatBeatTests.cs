using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

namespace SM.Tests.EditMode;

/// <summary>
/// Phase 3 시너지/콤보 발현(CombatBeat 채널) — 트리거 발동·시너지 브레이크포인트·콤보 연쇄가
/// 결정론적 beat 으로 남고(step 0 노출 포함), primer/consumer 가 ChainId 를 공유하며,
/// 윈도우(1.2s)/ICD(2.5s) 계약이 지켜지는지 고정한다.
/// </summary>
[Category("FastUnit")]
public sealed class CombatBeatTests
{
    private static UnitSnapshot CreateUnit(
        string id,
        TeamSide side,
        CombatTriggeredEffect[]? triggers = null,
        float hp = 40f,
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(id, anchor: anchor, hp: hp);
        if (triggers != null)
        {
            loadout = loadout with { TriggeredEffects = triggers };
        }

        var unit = new UnitSnapshot(
            new EntityId(id),
            side,
            loadout,
            BattleFactory.ResolveAnchorPosition(side, anchor),
            BattleFactory.ResolveSpawnPosition(side, anchor));
        unit.SetActionState(CombatActionState.AcquireTarget);
        return unit;
    }

    private static BattleState CreateState(UnitSnapshot[] allies, UnitSnapshot[] enemies, int seed = 42)
    {
        return new BattleState(
            allies,
            enemies,
            TeamPostureType.StandardAdvance,
            TeamPostureType.StandardAdvance,
            BattleSimulator.DefaultFixedStepSeconds,
            seed);
    }

    private static BattleEvent StatusAppliedEvent(BattleState state, UnitSnapshot actor, UnitSnapshot target, string statusId)
    {
        return new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            BattleActionType.ActiveSkill,
            BattleLogCode.Generic,
            target.Id,
            target.Definition.Name,
            0f,
            BattleEventKind.StatusApplied,
            statusId);
    }

    private static BattleEvent DamageEvent(BattleState state, UnitSnapshot actor, UnitSnapshot target, float value)
    {
        return new BattleEvent(
            state.StepIndex,
            state.ElapsedSeconds,
            actor.Id,
            actor.Definition.Name,
            BattleActionType.BasicAttack,
            BattleLogCode.BasicAttackDamage,
            target.Id,
            target.Definition.Name,
            value);
    }

    private static string Serialize(CombatBeat beat)
    {
        return string.Join(
            ":",
            beat.StepIndex,
            beat.SequenceInStep,
            beat.Type,
            beat.Side,
            beat.SourceId?.Value ?? "-",
            beat.TargetId?.Value ?? "-",
            beat.ChainId,
            beat.Importance,
            beat.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            beat.Tag);
    }

    [Test]
    public void BattleStartTriggerBeat_AppearsAtStepZero_AndInPostFightLog()
    {
        var effect = new CombatTriggeredEffect(
            "aug_opening_bulwark", CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier,
            EffectScope.Self, Magnitude: 25f);
        var ally = CreateUnit("ally_bulwark", TeamSide.Ally, new[] { effect });
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { ally }, new[] { enemy });

        var sim = new BattleSimulator(state, 60);

        Assert.That(sim.CurrentStep.Beats, Is.Not.Null.And.Count.EqualTo(1),
            "전투 시작 트리거는 초기 read model(step 0)에 beat 으로 보여야 한다");
        var beat = sim.CurrentStep.Beats![0];
        Assert.That(beat.StepIndex, Is.EqualTo(0), "BattleStart effects appear at step 0");
        Assert.That(beat.Type, Is.EqualTo(CombatBeatType.BattleStartEffect));
        Assert.That(beat.Tag, Is.EqualTo("aug_opening_bulwark"), "beat 은 증강 출처를 attribution 한다");
        Assert.That(beat.SourceId, Is.EqualTo(ally.Id));
        Assert.That(beat.TargetId, Is.EqualTo(ally.Id));
        Assert.That(beat.Importance, Is.EqualTo(CombatBeatImportance.BattleStartEffect));

        var result = sim.RunToEnd();
        Assert.That(result.Beats, Is.Not.Null, "post-fight 로그(BattleResult.Beats)가 있어야 한다");
        Assert.That(result.Beats!.Count(b => b.Type == CombatBeatType.BattleStartEffect && b.Tag == "aug_opening_bulwark"),
            Is.EqualTo(1), "발동한 트리거는 post-fight 로그에 source/target/value 와 함께 남는다");
    }

    [Test]
    public void SynergyBreakpoints_EmitTeamBeats_AtStepZero()
    {
        // CreateLoopAUnit 기본값은 race=human/class=vanguard — 2인 팀이면 V1 폴백 브레이크포인트
        // (race 2 / class 2)가 둘 다 활성화된다. 시너지는 스탯 패키지로만 녹아 있었으나(stat soup),
        // Phase 3 부터는 step 0 에 팀 beat 으로 발현되어야 한다.
        var state = CombatTestFactory.CreateBattleState(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("ally_a"),
                CombatTestFactory.CreateLoopAUnit("ally_b", anchor: DeploymentAnchorId.FrontTop),
            },
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("enemy_a"),
                CombatTestFactory.CreateLoopAUnit("enemy_b", anchor: DeploymentAnchorId.FrontTop),
            },
            seed: 11);

        var sim = new BattleSimulator(state, 60);

        var beats = sim.CurrentStep.Beats ?? System.Array.Empty<CombatBeat>();
        var synergyBeats = beats.Where(b => b.Type == CombatBeatType.SynergyActivated).ToList();
        Assert.That(synergyBeats, Has.Count.EqualTo(4), "양 팀 각각 race/class 브레이크포인트 2개씩");
        Assert.That(synergyBeats.All(b => b.StepIndex == 0), "시너지 발현은 step 0");
        Assert.That(synergyBeats.All(b => b.SourceId == null), "시너지는 팀 단위 beat — 단일 출처 유닛이 없다");
        Assert.That(synergyBeats.Count(b => b.Side == TeamSide.Ally), Is.EqualTo(2));
        var allyTags = synergyBeats.Where(b => b.Side == TeamSide.Ally).Select(b => b.Tag).ToList();
        Assert.That(allyTags, Has.Some.Contains("race:human"), "race 브레이크포인트 식별자 attribution");
        Assert.That(allyTags, Has.Some.Contains("class:vanguard"), "class 브레이크포인트 식별자 attribution");
    }

    [Test]
    public void ComboPrimer_FollowupHit_SharesChainId()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var finisher = CreateUnit("ally_finisher", TeamSide.Ally, anchor: DeploymentAnchorId.FrontTop);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy);
        var state = CreateState(new[] { setter, finisher }, new[] { victim });

        CombatComboService.ProcessStep(state, new List<BattleEvent>
        {
            StatusAppliedEvent(state, setter, victim, "stun"),
            DamageEvent(state, finisher, victim, 12f),
        });

        var beats = state.DrainStepBeats();
        Assert.That(beats, Has.Count.EqualTo(2), "primer + consume");
        Assert.That(beats[0].Type, Is.EqualTo(CombatBeatType.ComboPrimerApplied));
        Assert.That(beats[0].SourceId, Is.EqualTo(setter.Id));
        Assert.That(beats[1].Type, Is.EqualTo(CombatBeatType.ComboConsumed));
        Assert.That(beats[1].SourceId, Is.EqualTo(finisher.Id));
        Assert.That(beats[1].Value, Is.EqualTo(12f * 0.35f).Within(0.0001f),
            "consume beat Value 는 원 타격이 아니라 실제 콤보 추가타 금액이다");
        Assert.That(beats[0].ChainId, Is.GreaterThan(0));
        Assert.That(beats[1].ChainId, Is.EqualTo(beats[0].ChainId),
            "primer 와 consume 은 같은 ChainId 를 공유한다");
    }

    [TestCase("stun", 0.35f)]
    [TestCase("root", 0.25f)]
    [TestCase("slow", 0.15f)]
    [TestCase("sunder", 0.25f)]
    [TestCase("marked", 0.20f)]
    [TestCase("exposed", 0.30f)]
    public void ComboPayoffBonus_DefaultFamiliesMatchV1Authority(string statusId, float expected)
    {
        Assert.That(CombatStatusRules.Default.ResolveComboPayoffBonus(statusId), Is.EqualTo(expected).Within(0.0001f));
        Assert.That(CombatStatusRules.Default.ResolveComboPayoffBonus("unregistered_family"), Is.Zero,
            "미등록 family는 추가타를 만들지 않아 기존 동작을 보존한다");
    }

    [Test]
    public void ComboPayoff_SunderFollowup_AppliesSeparateDamageEventAndBeatValue()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var finisher = CreateUnit("ally_finisher", TeamSide.Ally, anchor: DeploymentAnchorId.FrontTop);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy, hp: 100f);
        var state = CreateState(new[] { setter, finisher }, new[] { victim });
        var events = new List<BattleEvent>
        {
            StatusAppliedEvent(state, setter, victim, "sunder"),
            DamageEvent(state, finisher, victim, 12f),
        };

        CombatComboService.ProcessStep(state, events);

        var payoff = events.Single(evt => evt.LogCode == BattleLogCode.ComboPayoffDamage);
        var consume = state.DrainStepBeats().Single(beat => beat.Type == CombatBeatType.ComboConsumed);
        Assert.That(payoff.Value, Is.EqualTo(3f).Within(0.0001f));
        Assert.That(victim.CurrentHealth, Is.EqualTo(97f).Within(0.0001f));
        Assert.That(consume.Value, Is.EqualTo(payoff.Value).Within(0.0001f));
        Assert.That(consume.Tag, Is.EqualTo("sunder"));
    }

    [Test]
    public void ComboPayoff_MainHitAlreadyKilledVictim_RecordsConsumeWithoutExtraDamage()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var finisher = CreateUnit("ally_finisher", TeamSide.Ally, anchor: DeploymentAnchorId.FrontTop);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy, hp: 10f);
        var state = CreateState(new[] { setter, finisher }, new[] { victim });
        CombatComboService.ProcessStep(state, new List<BattleEvent>
        {
            StatusAppliedEvent(state, setter, victim, "sunder"),
        });
        state.DrainStepBeats();
        state.AdvanceStep();
        victim.TakeDamage(10f); // CombatActionResolver의 메인 히트가 ProcessStep 전에 이미 사망시킨 상태.
        var events = new List<BattleEvent>
        {
            DamageEvent(state, finisher, victim, 12f),
        };

        CombatComboService.ProcessStep(state, events);

        var consume = state.DrainStepBeats().Single(beat => beat.Type == CombatBeatType.ComboConsumed);
        Assert.That(events.Any(evt => evt.LogCode == BattleLogCode.ComboPayoffDamage), Is.False,
            "죽은 victim을 추가타로 다시 처리하지 않는다");
        Assert.That(events.Count(evt => evt.EventKind == BattleEventKind.Kill), Is.Zero);
        Assert.That(consume.Value, Is.Zero, "완성 beat는 남되 적용되지 않은 페이오프 금액은 0이다");
    }

    [Test]
    public void ComboPayoff_LethalDamage_AppendsNormalKillEventForSimulatorTriggerPass()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var finisher = CreateUnit("ally_finisher", TeamSide.Ally, anchor: DeploymentAnchorId.FrontTop);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy, hp: 2f);
        var state = CreateState(new[] { setter, finisher }, new[] { victim });
        var events = new List<BattleEvent>
        {
            StatusAppliedEvent(state, setter, victim, "sunder"),
            DamageEvent(state, finisher, victim, 12f),
        };

        CombatComboService.ProcessStep(state, events);

        Assert.That(victim.IsAlive, Is.False);
        Assert.That(events.Select(evt => evt.LogCode), Does.Contain(BattleLogCode.ComboPayoffDamage));
        var kill = events.Single(evt => evt.EventKind == BattleEventKind.Kill);
        Assert.That(kill.KillPayload?.ActualKiller, Is.EqualTo(finisher.Id));
        Assert.That(kill.KillPayload?.ActualVictim, Is.EqualTo(victim.Id));
    }

    [Test]
    public void ComboSelfHit_SameStep_DoesNotConsume_LaterStepDoes()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy);
        var state = CreateState(new[] { setter }, new[] { victim });

        // 같은 step 의 같은 유닛 타격은 "세팅 + 후속"이 아니다 — 소비 금지.
        CombatComboService.ProcessStep(state, new List<BattleEvent>
        {
            StatusAppliedEvent(state, setter, victim, "marked"),
            DamageEvent(state, setter, victim, 6f),
        });
        var primeBeats = state.DrainStepBeats();
        Assert.That(primeBeats.Select(b => b.Type), Is.EqualTo(new[] { CombatBeatType.ComboPrimerApplied }),
            "프라이머를 건 유닛의 같은 step 타격은 소비하지 않는다");

        // 다음 step 의 후속 타격은 정당한 소비다(같은 유닛이어도 — hunter mark→shoot 패턴).
        state.AdvanceStep();
        CombatComboService.ProcessStep(state, new List<BattleEvent>
        {
            DamageEvent(state, setter, victim, 8f),
        });
        var consumeBeats = state.DrainStepBeats();
        Assert.That(consumeBeats.Select(b => b.Type), Is.EqualTo(new[] { CombatBeatType.ComboConsumed }));
        Assert.That(consumeBeats[0].ChainId, Is.EqualTo(primeBeats[0].ChainId));
    }

    [Test]
    public void ComboPrimerWindow_ExpiresAfterTwelveTicks()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var finisher = CreateUnit("ally_finisher", TeamSide.Ally, anchor: DeploymentAnchorId.FrontTop);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy);
        var state = CreateState(new[] { setter, finisher }, new[] { victim });

        CombatComboService.ProcessStep(state, new List<BattleEvent>
        {
            StatusAppliedEvent(state, setter, victim, "exposed"),
        });
        state.DrainStepBeats();

        for (var i = 0; i < CombatComboService.PrimerWindowTicks; i++)
        {
            state.AdvanceStep();
        }

        CombatComboService.ProcessStep(state, new List<BattleEvent>
        {
            DamageEvent(state, finisher, victim, 9f),
        });
        Assert.That(state.DrainStepBeats(), Is.Empty,
            "1.2s(12 tick) 윈도우가 지난 프라이머는 소비되지 않는다");
    }

    [Test]
    public void ComboChainIcd_BlocksRefire_ThenRecovers()
    {
        var setter = CreateUnit("ally_setter", TeamSide.Ally);
        var victim = CreateUnit("enemy_victim", TeamSide.Enemy, hp: 500f);
        var state = CreateState(new[] { setter }, new[] { victim });

        // 1차 연쇄: prime → 다음 step 소비.
        CombatComboService.ProcessStep(state, new List<BattleEvent> { StatusAppliedEvent(state, setter, victim, "slow") });
        state.DrainStepBeats();
        state.AdvanceStep();
        CombatComboService.ProcessStep(state, new List<BattleEvent> { DamageEvent(state, setter, victim, 5f) });
        var firstConsume = state.DrainStepBeats();
        Assert.That(firstConsume.Select(b => b.Type), Is.EqualTo(new[] { CombatBeatType.ComboConsumed }));
        var consumedAtStep = state.StepIndex;

        // ICD 안의 재연쇄: 프라이머는 다시 열리지만(새 ChainId) 소비는 차단된다.
        state.AdvanceStep();
        CombatComboService.ProcessStep(state, new List<BattleEvent> { StatusAppliedEvent(state, setter, victim, "slow") });
        var reprime = state.DrainStepBeats();
        Assert.That(reprime.Select(b => b.Type), Is.EqualTo(new[] { CombatBeatType.ComboPrimerApplied }));
        state.AdvanceStep();
        CombatComboService.ProcessStep(state, new List<BattleEvent> { DamageEvent(state, setter, victim, 5f) });
        Assert.That(state.DrainStepBeats(), Is.Empty,
            "같은 (소스, status) 연쇄는 2.5s ICD 안에 재발동하지 않는다");

        // ICD 가 끝나면 연쇄가 다시 가능하다.
        while (state.StepIndex < consumedAtStep + CombatComboService.ChainIcdTicks)
        {
            state.AdvanceStep();
        }

        CombatComboService.ProcessStep(state, new List<BattleEvent> { StatusAppliedEvent(state, setter, victim, "slow") });
        state.DrainStepBeats();
        state.AdvanceStep();
        CombatComboService.ProcessStep(state, new List<BattleEvent> { DamageEvent(state, setter, victim, 5f) });
        Assert.That(state.DrainStepBeats().Select(b => b.Type), Is.EqualTo(new[] { CombatBeatType.ComboConsumed }),
            "ICD 종료 후의 새 연쇄는 정상 소비된다");
    }

    [Test]
    public void HpThresholdTriggerBeat_FiresOnce_WithClutchImportance()
    {
        var effect = new CombatTriggeredEffect(
            "aug_last_stand", CombatTriggerKind.OnHpBelow, TriggeredEffectOp.Barrier,
            EffectScope.Self, Magnitude: 30f, ThresholdRatio: 0.5f);
        var unit = CreateUnit("ally_laststand", TeamSide.Ally, new[] { effect }, hp: 100f);
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { unit }, new[] { enemy });

        CombatTriggerEngine.OnPostStep(state);
        Assert.That(state.StepBeats, Is.Empty, "임계 위에서는 발동/beat 없음");

        unit.TakeDamage(60f);
        CombatTriggerEngine.OnPostStep(state);
        var beats = state.DrainStepBeats();
        Assert.That(beats, Has.Count.EqualTo(1));
        Assert.That(beats[0].Type, Is.EqualTo(CombatBeatType.HpThresholdEffect));
        Assert.That(beats[0].Importance, Is.EqualTo(CombatBeatImportance.HpThresholdEffect),
            "빈사 임계 발동은 clutch 계열 중요도를 받는다");

        CombatTriggerEngine.OnPostStep(state);
        Assert.That(state.StepBeats, Is.Empty, "전투당 1회 latch — 재발동/재beat 없음");
    }

    [Test]
    public void OnKillTriggerBeat_TaggedWithAugmentSource()
    {
        var effect = new CombatTriggeredEffect(
            "aug_reaper", CombatTriggerKind.OnKill, TriggeredEffectOp.Heal,
            EffectScope.Self, Magnitude: 15f);
        var killer = CreateUnit("ally_reaper", TeamSide.Ally, new[] { effect }, hp: 100f);
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { killer }, new[] { enemy });
        killer.TakeDamage(50f);

        CombatTriggerEngine.OnKill(state, killer);

        var beats = state.DrainStepBeats();
        Assert.That(beats, Has.Count.EqualTo(1));
        Assert.That(beats[0].Type, Is.EqualTo(CombatBeatType.OnKillEffect));
        Assert.That(beats[0].Tag, Is.EqualTo("aug_reaper"));
        Assert.That(beats[0].Value, Is.EqualTo(15f));
    }

    [Test]
    public void ContentLikeFight_ManifestsComboChain_AndBeatStreamIsDeterministic()
    {
        // 실제 resolver 경로 통합 검증: flex 스킬이 marked 프라이머를 걸고, 아군의 후속 타격이
        // 윈도우 안에 소비한다 — primer/consume 이 같은 ChainId 로 post-fight 로그에 남아야 한다.
        static BattleResult Run()
        {
            var markFlex = new BattleSkillSpec(
                "skill_mark", "Mark", SkillKind.Strike, 2f, 6f,
                SlotKind: CompiledSkillSlots.UtilityActive,
                ResolvedSlotKind: ActionSlotKind.FlexActive,
                ActivationModel: ActivationModel.Cooldown,
                Lane: ActionLane.Primary,
                LockRule: ActionLockRule.HardCommit,
                BaseCooldownSeconds: 1.5f,
                Delivery: SkillDelivery.Projectile,
                TargetRuleData: new TargetRule
                {
                    Domain = TargetDomain.EnemyUnit,
                    PrimarySelector = TargetSelector.LowestCurrentHpEnemy,
                    FallbackPolicy = TargetFallbackPolicy.NearestReachableEnemy,
                    Filters = TargetFilterFlags.ExcludeUntargetable,
                },
                AppliedStatuses: new[] { new StatusApplicationSpec("skill_mark:marked", "marked", 3f, 0.15f) });
            var state = CombatTestFactory.CreateBattleState(
                new[]
                {
                    CombatTestFactory.CreateLoopAUnit("ally_marker", classId: "ranger", anchor: DeploymentAnchorId.BackCenter, attackRange: 6f, flexActive: markFlex),
                    CombatTestFactory.CreateLoopAUnit("ally_striker", hp: 80f),
                },
                new[]
                {
                    CombatTestFactory.CreateLoopAUnit("enemy_tank", hp: 90f, armor: 3f),
                },
                seed: 23);
            return new BattleSimulator(state, 200).RunToEnd();
        }

        var first = Run();
        var second = Run();

        var beats = first.Beats ?? System.Array.Empty<CombatBeat>();
        var primers = beats.Where(b => b.Type == CombatBeatType.ComboPrimerApplied && b.Tag == "marked").ToList();
        var consumes = beats.Where(b => b.Type == CombatBeatType.ComboConsumed && b.Tag == "marked").ToList();
        Assert.That(primers, Is.Not.Empty, "스킬의 marked 적용이 프라이머 beat 으로 발현되어야 한다");
        Assert.That(consumes, Is.Not.Empty, "윈도우 안의 후속 타격이 콤보를 소비해야 한다");
        Assert.That(consumes.All(consume => primers.Any(p => p.ChainId == consume.ChainId)),
            "모든 소비 beat 은 자기 프라이머와 ChainId 를 공유한다");

        Assert.That(
            (second.Beats ?? System.Array.Empty<CombatBeat>()).Select(Serialize).ToList(),
            Is.EqualTo(beats.Select(Serialize).ToList()),
            "같은 seed 의 beat 스트림은 결정론적으로 동일해야 한다");
    }
}
