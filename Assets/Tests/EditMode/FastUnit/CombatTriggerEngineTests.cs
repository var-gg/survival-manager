using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;
using SM.Core.Contracts;
using SM.Core.Ids;

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
    public void OnKill_Heal_RestoresKillerHealth()
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
    }
}
