using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;
using SM.Core.Stats;

namespace SM.Tests.EditMode;

/// <summary>
/// wave-augment-depth Inc 1a — 증강·패시브 트리거 효과 엔진(CombatTriggerEngine) 단위 검증.
/// 콘텐츠 파이프라인 없이 BattleUnitLoadout 에 직접 TriggeredEffects 를 주입해 엔진 거동만 격리 테스트.
/// </summary>
[Category("FastUnit")]
public sealed class CombatTriggerEngineTests
{
    private static UnitSnapshot CreateUnit(
        string id,
        TeamSide side,
        CombatTriggeredEffect[]? triggers = null,
        float hp = 40f,
        float physPower = 5f,
        float armor = 0f,
        DeploymentAnchorId anchor = DeploymentAnchorId.FrontCenter)
    {
        var loadout = CombatTestFactory.CreateLoopAUnit(
            id,
            anchor: anchor,
            hp: hp,
            physPower: physPower,
            armor: armor);
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

    [Test]
    public void BattleStart_Barrier_AppliesToSelf()
    {
        var effect = new CombatTriggeredEffect(
            "aug_test_barrier", CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier,
            EffectScope.Self, Magnitude: 25f);
        var unit = CreateUnit("ally_barrier", TeamSide.Ally, new[] { effect });
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { unit }, new[] { enemy });

        Assert.That(unit.Barrier, Is.EqualTo(0f), "Precondition: no barrier");

        CombatTriggerEngine.OnBattleStart(state);

        Assert.That(unit.Barrier, Is.EqualTo(25f), "BattleStart Barrier effect should apply to self");
    }

    [Test]
    public void BattleStart_ApplyStatus_AppliesToAlliedTeamOnly()
    {
        var effect = new CombatTriggeredEffect(
            "aug_test_guard", CombatTriggerKind.BattleStart, TriggeredEffectOp.ApplyStatus,
            EffectScope.AlliedCombatants, DurationSeconds: 10f, StatusId: "guarded");
        var leader = CreateUnit("ally_leader", TeamSide.Ally, new[] { effect });
        var mate = CreateUnit("ally_mate", TeamSide.Ally);
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { leader, mate }, new[] { enemy });

        CombatTriggerEngine.OnBattleStart(state);

        Assert.That(leader.HasStatus("guarded"), Is.True, "Effect owner should be guarded");
        Assert.That(mate.HasStatus("guarded"), Is.True, "Allied team should be guarded");
        Assert.That(enemy.HasStatus("guarded"), Is.False, "Enemy should NOT be guarded");
    }

    [Test]
    public void OnBattleStart_Bulwark_GuardsLivingVanguardsOnly_WithDeterministicRuleBeats()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("vanguard_human", race: "human", classId: "vanguard"),
            CombatTestFactory.CreateLoopAUnit("vanguard_beastkin", race: "beastkin", classId: "vanguard"),
            CombatTestFactory.CreateLoopAUnit("vanguard_undead", race: "undead", classId: "vanguard"),
            CombatTestFactory.CreateLoopAUnit("duelist_probe", race: "outsider", classId: "duelist"),
        };
        var state = BattleFactory.Create(
            allies,
            new[] { CombatTestFactory.CreateLoopAUnit("enemy", race: "enemy", classId: "enemy") });

        CombatTriggerEngine.OnBattleStart(state);

        var vanguards = state.Allies.Where(unit => unit.Definition.ClassId == "vanguard").ToList();
        var nonVanguard = state.Allies.Single(unit => unit.Definition.ClassId != "vanguard");
        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.BulwarkRuleId), Is.True);
        Assert.That(vanguards, Has.Count.EqualTo(3));
        Assert.That(vanguards.All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.BulwarkRuleId).StatusId == "guarded"), Is.True);
        Assert.That(vanguards.All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.BulwarkRuleId).RemainingSeconds == 600f), Is.True);
        Assert.That(vanguards.All(unit => unit.GetIncomingDamageMultiplier() == 0.9f), Is.True,
            "bulwark guarded는 Move 3 guarded 채널(-0.1)을 그대로 소비해야 한다");
        Assert.That(nonVanguard.Statuses.Any(status =>
            status.SourceApplicationId == TeamRuleSet.BulwarkRuleId), Is.False);

        var ruleBeats = state.DrainStepBeats()
            .Where(beat => beat.Tag == TeamRuleSet.BulwarkRuleId)
            .ToList();
        Assert.That(ruleBeats, Has.Count.EqualTo(3));
        Assert.That(ruleBeats.All(beat => beat.Type == CombatBeatType.BattleStartEffect
                                         && beat.SourceId == null
                                         && beat.TargetId != null), Is.True,
            "개전 팀규칙은 대상별 BattleStartEffect beat로 발화해야 한다");
        var targetIds = ruleBeats.Select(beat => beat.TargetId!.Value.Value).ToList();
        Assert.That(
            targetIds,
            Is.EqualTo(targetIds.OrderBy(id => id, System.StringComparer.Ordinal).ToList()),
            "class recipient는 stable id ordinal 순서로 처리해야 한다");
    }

    [Test]
    public void OnBattleStart_Resonance_GrantsMysticStatusPotency_ConsumedByStatusChokePoint()
    {
        var markSkill = new BattleSkillSpec(
            "skill.resonance.mark",
            "Resonance Mark",
            SkillKind.Utility,
            0f,
            1f,
            AppliedStatuses: new[] { new StatusApplicationSpec("apply.resonance.mark", "marked", 5f, 0.5f) });
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("mystic_human", race: "human", classId: "mystic", signatureActive: markSkill),
            CombatTestFactory.CreateLoopAUnit("mystic_beastkin", race: "beastkin", classId: "mystic"),
            CombatTestFactory.CreateLoopAUnit("mystic_undead", race: "undead", classId: "mystic"),
            CombatTestFactory.CreateLoopAUnit("ranger_probe", race: "outsider", classId: "ranger"),
        };
        var state = BattleFactory.Create(
            allies,
            new[] { CombatTestFactory.CreateLoopAUnit("target", race: "enemy", classId: "enemy") });

        CombatTriggerEngine.OnBattleStart(state);

        var mystics = state.Allies.Where(unit => unit.Definition.ClassId == "mystic").ToList();
        var nonMystic = state.Allies.Single(unit => unit.Definition.ClassId != "mystic");
        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.ResonanceRuleId), Is.True);
        Assert.That(mystics.All(unit => unit.Stats.Get(StatKey.StatusPotency) == 0.20f), Is.True);
        Assert.That(nonMystic.Stats.Get(StatKey.StatusPotency), Is.Zero);

        var events = new List<BattleEvent>();
        StatusResolutionService.ApplySkillStatuses(state, mystics[0], state.Enemies.Single(), markSkill, events);

        Assert.That(
            state.Enemies.Single().Statuses.Single(status => status.StatusId == "marked").Magnitude,
            Is.EqualTo(0.6f).Within(0.0001f),
            "resonance potency 0.2가 Move 5 choke point에서 marked 0.5를 ×1.2 해야 한다");
        Assert.That(state.DrainStepBeats().Count(beat => beat.Tag == TeamRuleSet.ResonanceRuleId), Is.EqualTo(3));
    }

    [Test]
    public void OnBattleStart_WithoutUpperClassRule_IsCanonicalNoOp()
    {
        var state = BattleFactory.Create(
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("vanguard_human", race: "human", classId: "vanguard"),
                CombatTestFactory.CreateLoopAUnit("vanguard_beastkin", race: "beastkin", classId: "vanguard"),
            },
            new[] { CombatTestFactory.CreateLoopAUnit("enemy", race: "enemy", classId: "enemy") });
        var before = BattleStateCanonicalHash.Compute(state);

        CombatTriggerEngine.OnBattleStart(state);

        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.BulwarkRuleId), Is.False);
        Assert.That(BattleStateCanonicalHash.Compute(state), Is.EqualTo(before),
            "class@2 stat-only comp의 OnBattleStart는 canonical state byte-identity를 보존한다");
        Assert.That(state.DrainStepBeats(), Is.Empty);
    }

    [Test]
    public void OnHpBelow_FiresOnce_WhenThresholdCrossed()
    {
        var effect = new CombatTriggeredEffect(
            "aug_test_laststand", CombatTriggerKind.OnHpBelow, TriggeredEffectOp.Barrier,
            EffectScope.Self, Magnitude: 30f, ThresholdRatio: 0.5f);
        var unit = CreateUnit("ally_laststand", TeamSide.Ally, new[] { effect }, hp: 100f);
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { unit }, new[] { enemy });

        CombatTriggerEngine.OnPostStep(state);
        Assert.That(unit.Barrier, Is.EqualTo(0f), "Above threshold should not fire");

        unit.TakeDamage(60f); // 100 -> 40 (ratio 0.4, below 0.5)
        CombatTriggerEngine.OnPostStep(state);
        Assert.That(unit.Barrier, Is.EqualTo(30f), "Crossing threshold should fire Barrier once");

        unit.TakeDamage(unit.Barrier + 5f); // consume barrier + chip hp, still below threshold
        CombatTriggerEngine.OnPostStep(state);
        Assert.That(unit.Barrier, Is.EqualTo(0f), "OnHpBelow should NOT refire once latched");
    }

    [Test]
    public void OnKill_Heal_RestoresKillerHealth_AndRecordsHealingTelemetry()
    {
        var effect = new CombatTriggeredEffect(
            "aug_test_reap", CombatTriggerKind.OnKill, TriggeredEffectOp.Heal,
            EffectScope.Self, Magnitude: 15f);
        var killer = CreateUnit("ally_reaper", TeamSide.Ally, new[] { effect }, hp: 100f);
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { killer }, new[] { enemy });

        killer.TakeDamage(50f); // 100 -> 50
        var hpBefore = killer.CurrentHealth;

        CombatTriggerEngine.OnKill(state, killer);

        Assert.That(killer.CurrentHealth, Is.GreaterThan(hpBefore), "OnKill Heal should restore killer health");
        var telemetry = state.TelemetryEvents.Single(record =>
            record.EventKind == TelemetryEventKind.HealingApplied);
        Assert.That(telemetry.Actor?.UnitInstanceId, Is.EqualTo(killer.Id.Value));
        Assert.That(telemetry.Target?.UnitInstanceId, Is.EqualTo(killer.Id.Value));
        Assert.That(telemetry.ValueA, Is.EqualTo(15f));
        Assert.That(telemetry.Explain?.SourceContentId, Is.EqualTo(effect.SourceId));
        Assert.That(telemetry.Explain?.ReasonCode, Is.EqualTo(DecisionReasonCode.TriggeredReaction));
        Assert.That(telemetry.StringValueA, Is.EqualTo("triggered_flat_heal"));
    }

    [Test]
    public void OnTeamKill_Bloodrush_StacksTempoForLivingBeastkin_AndExpiresAfterPointTwoFiveSeconds()
    {
        var allies = Enumerable.Range(0, 4)
            .Select(index => CombatTestFactory.CreateLoopAUnit($"beast_{index}", race: "beastkin", classId: $"class_{index}"))
            .ToArray();
        var state = BattleFactory.Create(
            allies,
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("victim_a", race: "human"),
                CombatTestFactory.CreateLoopAUnit("victim_b", race: "human"),
            });
        var baselineAttackSpeed = state.Allies[0].AttackSpeed;
        var baselineMoveSpeed = state.Allies[0].MoveSpeed;

        var firstVictim = state.Enemies[0];
        firstVictim.TakeDamage(firstVictim.CurrentHealth + 1f);
        CombatTriggerEngine.OnTeamKill(state, firstVictim);
        var secondVictim = state.Enemies[1];
        secondVictim.TakeDamage(secondVictim.CurrentHealth + 1f);
        CombatTriggerEngine.OnTeamKill(state, secondVictim);

        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.BloodrushRuleId), Is.True);
        Assert.That(state.Allies.All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.BloodrushRuleId).Stacks == 2), Is.True);
        Assert.That(state.Allies[0].AttackSpeed,
            Is.EqualTo(baselineAttackSpeed * 1.10f).Within(0.0001f));
        Assert.That(state.Allies[0].MoveSpeed,
            Is.EqualTo(baselineMoveSpeed * 1.10f).Within(0.0001f));

        for (var tick = 0; tick < BattleTickMath.DurationToTicks(2.5f); tick++)
        {
            foreach (var unit in state.Allies)
            {
                unit.AdvanceStatusTimers();
            }
        }

        Assert.That(state.Allies.All(unit => unit.Statuses.All(status =>
            status.SourceApplicationId != TeamRuleSet.BloodrushRuleId)), Is.True);
    }

    [Test]
    public void OnTeamKill_DeathToll_PermanentlyStacksPhysPowerAndMaxHealthForLivingUndead()
    {
        var allies = Enumerable.Range(0, 4)
            .Select(index => CombatTestFactory.CreateLoopAUnit($"undead_{index}", race: "undead", classId: $"class_{index}"))
            .ToArray();
        var state = BattleFactory.Create(
            allies,
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("victim_a", race: "human"),
                CombatTestFactory.CreateLoopAUnit("victim_b", race: "human"),
            });
        var baselinePhysPower = state.Allies[0].PhysPower;
        var baselineMaxHealth = state.Allies[0].MaxHealth;
        foreach (var victim in state.Enemies)
        {
            victim.TakeDamage(victim.CurrentHealth + 1f);
        }

        var beforeRules = BattleStateCanonicalHash.Compute(state);
        foreach (var victim in state.Enemies)
        {
            CombatTriggerEngine.OnTeamKill(state, victim);
        }
        var afterRules = BattleStateCanonicalHash.Compute(state);

        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.DeathTollRuleId), Is.True);
        Assert.That(state.Allies.All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.DeathTollRuleId).Stacks == 2), Is.True);
        Assert.That(state.Allies[0].PhysPower,
            Is.EqualTo(baselinePhysPower + 0.5f).Within(0.0001f));
        Assert.That(state.Allies[0].MaxHealth,
            Is.EqualTo(baselineMaxHealth + 2f).Within(0.0001f));
        Assert.That(afterRules, Is.Not.EqualTo(beforeRules),
            "deathtoll 영구 stat modifier의 재구성 marker status가 canonical state에 남아야 한다");

        for (var tick = 0; tick < 500; tick++)
        {
            state.Allies[0].AdvanceStatusTimers();
        }

        Assert.That(state.Allies[0].Statuses.Any(status =>
            status.SourceApplicationId == TeamRuleSet.DeathTollRuleId), Is.True,
            "deathtoll buff는 전투 종료까지 timer로 소멸하지 않는다");
    }

    [Test]
    public void OnTeamKill_Killzone_PermanentlyStacksPhysPowerAndCritForLivingRangers()
    {
        var allies = new[]
        {
            CombatTestFactory.CreateLoopAUnit("ranger_human", race: "human", classId: "ranger"),
            CombatTestFactory.CreateLoopAUnit("ranger_beastkin", race: "beastkin", classId: "ranger"),
            CombatTestFactory.CreateLoopAUnit("ranger_undead", race: "undead", classId: "ranger"),
            CombatTestFactory.CreateLoopAUnit("vanguard_probe", race: "outsider", classId: "vanguard"),
        };
        var state = BattleFactory.Create(
            allies,
            new[]
            {
                CombatTestFactory.CreateLoopAUnit("victim_a", race: "enemy", classId: "enemy_a"),
                CombatTestFactory.CreateLoopAUnit("victim_b", race: "enemy", classId: "enemy_b"),
            });
        var ranger = state.Allies.First(unit => unit.Definition.ClassId == "ranger");
        var nonRanger = state.Allies.Single(unit => unit.Definition.ClassId != "ranger");
        var baselinePhysPower = ranger.PhysPower;
        var baselineCritChance = ranger.Stats.Get(StatKey.CritChance);
        var nonRangerPhysPower = nonRanger.PhysPower;
        var nonRangerCritChance = nonRanger.Stats.Get(StatKey.CritChance);
        foreach (var victim in state.Enemies)
        {
            victim.TakeDamage(victim.CurrentHealth + 1f);
        }

        var beforeRules = BattleStateCanonicalHash.Compute(state);
        foreach (var victim in state.Enemies)
        {
            CombatTriggerEngine.OnTeamKill(state, victim);
        }
        var afterRules = BattleStateCanonicalHash.Compute(state);

        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.KillzoneRuleId), Is.True);
        Assert.That(state.Allies.Where(unit => unit.Definition.ClassId == "ranger").All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.KillzoneRuleId).Stacks == 2), Is.True);
        Assert.That(ranger.PhysPower,
            Is.EqualTo(baselinePhysPower + (2f * 0.25f)).Within(0.0001f));
        Assert.That(ranger.Stats.Get(StatKey.CritChance),
            Is.EqualTo(baselineCritChance + (2f * 0.01f)).Within(0.0001f));
        Assert.That(nonRanger.PhysPower, Is.EqualTo(nonRangerPhysPower));
        Assert.That(nonRanger.Stats.Get(StatKey.CritChance), Is.EqualTo(nonRangerCritChance));
        Assert.That(afterRules, Is.Not.EqualTo(beforeRules),
            "killzone의 동적 kill stack이 permanent marker status로 canonical state에 남아야 한다");
    }

    [Test]
    public void OnTeamKill_WithoutUpperRaceRule_IsNoOp()
    {
        var allies = Enumerable.Range(0, 3)
            .Select(index => CombatTestFactory.CreateLoopAUnit($"beast_{index}", race: "beastkin", classId: $"class_{index}"))
            .ToArray();
        var state = BattleFactory.Create(
            allies,
            new[] { CombatTestFactory.CreateLoopAUnit("victim", race: "human") });
        var baselineSpeed = state.Allies[0].AttackSpeed;
        var victim = state.Enemies[0];
        victim.TakeDamage(victim.CurrentHealth + 1f);
        var before = BattleStateCanonicalHash.Compute(state);

        CombatTriggerEngine.OnTeamKill(state, victim);

        var after = BattleStateCanonicalHash.Compute(state);
        Assert.That(state.TeamRuleSet.Has(TeamSide.Ally, TeamRuleSet.BloodrushRuleId), Is.False);
        Assert.That(state.Allies.All(unit => unit.Statuses.All(status =>
            status.SourceApplicationId != TeamRuleSet.BloodrushRuleId)), Is.True);
        Assert.That(state.Allies[0].AttackSpeed, Is.EqualTo(baselineSpeed));
        Assert.That(after, Is.EqualTo(before), "규칙 없는 comp의 OnTeamKill은 canonical state byte-identity를 보존한다");
    }

    [Test]
    public void BattleSimulator_KillLoop_FiresOnTeamKillExactlyOncePerKill()
    {
        var allies = Enumerable.Range(0, 4)
            .Select(index => CombatTestFactory.CreateLoopAUnit(
                $"beast_{index}",
                race: "beastkin",
                classId: $"class_{index}",
                physPower: index == 0 ? 50f : 1f))
            .ToArray();
        var state = BattleFactory.Create(
            allies,
            new[] { CombatTestFactory.CreateLoopAUnit("victim", race: "human", hp: 1f) });
        var killer = state.Allies[0];
        var victim = state.Enemies[0];
        killer.SetPosition(new CombatVector2(0f, 0f));
        victim.SetPosition(new CombatVector2(0.5f, 0f));
        killer.BeginWindup(BattleActionType.BasicAttack, victim.Id, null);

        var simulator = new BattleSimulator(state);
        simulator.Step();

        Assert.That(victim.IsAlive, Is.False);
        Assert.That(state.Allies.All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.BloodrushRuleId).Stacks == 1), Is.True,
            "단일 Kill 이벤트는 bloodrush stack을 정확히 한 번만 부여해야 한다");
    }

    [Test]
    public void BattleSimulator_PeriodicDamageDeath_FiresOnTeamKillBeforeWinnerEarlyExit()
    {
        var allies = Enumerable.Range(0, 4)
            .Select(index => CombatTestFactory.CreateLoopAUnit(
                $"beast_{index}",
                race: "beastkin",
                classId: $"class_{index}"))
            .ToArray();
        var state = BattleFactory.Create(
            allies,
            new[] { CombatTestFactory.CreateLoopAUnit("victim", race: "human", hp: 1f) });
        var source = state.Allies[0];
        var victim = state.Enemies[0];
        victim.ApplyStatus(
            new StatusApplicationSpec("dot_probe", "burn", 1f, 2f),
            sourceActorId: source.Id.Value,
            sourceApplicationId: "dot_probe");

        var simulator = new BattleSimulator(state);
        simulator.Step();

        Assert.That(victim.IsAlive, Is.False);
        Assert.That(simulator.IsFinished, Is.True, "마지막 적의 status tick 사망은 즉시 승자 판정으로 끝난다");
        Assert.That(state.Allies.All(unit => unit.Statuses.Single(status =>
            status.SourceApplicationId == TeamRuleSet.BloodrushRuleId).Stacks == 1), Is.True,
            "winner early-exit 전 periodic Kill 이벤트도 OnTeamKill을 정확히 한 번 통과해야 한다");
    }

    [Test]
    public void BattleSimulator_Ctor_FiresBattleStartTriggersViaEngineHook()
    {
        var effect = new CombatTriggeredEffect(
            "aug_sim_hook", CombatTriggerKind.BattleStart, TriggeredEffectOp.Barrier,
            EffectScope.Self, Magnitude: 20f);
        var ally = CreateUnit("ally_sim", TeamSide.Ally, new[] { effect });
        var enemy = CreateUnit("enemy_sim", TeamSide.Enemy);
        var state = CreateState(new[] { ally }, new[] { enemy });

        _ = new BattleSimulator(state);

        Assert.That(ally.Barrier, Is.EqualTo(20f),
            "BattleSimulator construction should fire BattleStart triggers through the engine hook");
    }

    [Test]
    public void BattleStart_GainEnergy_RaisesSelfEnergy()
    {
        var effect = new CombatTriggeredEffect(
            "aug_test_charge", CombatTriggerKind.BattleStart, TriggeredEffectOp.GainEnergy,
            EffectScope.Self, Magnitude: 25f);
        var unit = CreateUnit("ally_charge", TeamSide.Ally, new[] { effect });
        var enemy = CreateUnit("enemy_1", TeamSide.Enemy);
        var state = CreateState(new[] { unit }, new[] { enemy });
        var before = unit.CurrentEnergy;

        CombatTriggerEngine.OnBattleStart(state);

        Assert.That(unit.CurrentEnergy, Is.GreaterThan(before), "BattleStart GainEnergy should raise self energy");
    }

    [Test]
    public void OnAllyDeath_FiresForSurvivingAlly_NotEnemies()
    {
        var effect = new CombatTriggeredEffect(
            "aug_test_vengeance", CombatTriggerKind.OnAllyDeath, TriggeredEffectOp.Barrier,
            EffectScope.Self, Magnitude: 30f);
        var survivor = CreateUnit("ally_survivor", TeamSide.Ally, new[] { effect });
        var fallen = CreateUnit("ally_fallen", TeamSide.Ally);
        var enemy = CreateUnit("enemy_vengeance", TeamSide.Enemy, new[] { effect });
        var state = CreateState(new[] { survivor, fallen }, new[] { enemy });

        fallen.TakeDamage(fallen.CurrentHealth + 10f);
        Assert.That(fallen.IsAlive, Is.False, "Precondition: ally has fallen");

        CombatTriggerEngine.OnAllyDeath(state, fallen);

        Assert.That(survivor.Barrier, Is.EqualTo(30f), "Surviving ally with OnAllyDeath should react to the fallen ally");
        Assert.That(enemy.Barrier, Is.EqualTo(0f), "Enemy must NOT react to the opposing team's death");
    }
}
