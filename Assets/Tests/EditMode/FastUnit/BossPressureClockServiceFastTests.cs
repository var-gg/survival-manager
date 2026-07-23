using System;
using NUnit.Framework;
using SM.Combat.Model;
using SM.Combat.Services;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class BossPressureClockServiceFastTests
{
    [Test]
    public void PressureClock_PulsesOnAuthoredTicks_UsesBarrierAndStopsAtCap()
    {
        var ally = CreateWaitingUnit("ally", 100f);
        var boss = CreateWaitingUnit("boss", 1000f) with
        {
            BossPressureClock = new BossPressureClockSpec(2.5f, 1f, 0.10f, 2),
        };
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { boss });
        state.Allies[0].AddBarrier(5f);
        var simulator = new BattleSimulator(state, 100);

        Advance(simulator, 25);
        Assert.That(state.Allies[0].CurrentHealth, Is.EqualTo(100f).Within(0.001f));

        var firstPulse = simulator.Step();
        Assert.That(state.Allies[0].CurrentHealth, Is.EqualTo(95f).Within(0.001f));
        Assert.That(firstPulse.Events, Has.Some.Matches<BattleEvent>(resolved =>
            resolved.Note == "boss_pressure_clock:pulse_1"));

        Advance(simulator, 9);
        Assert.That(state.Allies[0].CurrentHealth, Is.EqualTo(95f).Within(0.001f));
        simulator.Step();
        Assert.That(state.Allies[0].CurrentHealth, Is.EqualTo(85f).Within(0.001f));

        Advance(simulator, 20);
        Assert.That(state.Allies[0].CurrentHealth, Is.EqualTo(85f).Within(0.001f));
    }

    [Test]
    public void PressureClock_DoesNotPulseAfterOwnerDies()
    {
        var ally = CreateWaitingUnit("ally", 100f);
        var boss = CreateWaitingUnit("boss", 100f) with
        {
            BossPressureClock = new BossPressureClockSpec(0.1f, 0.1f, 0.50f, 3),
        };
        var state = CombatTestFactory.CreateBattleState(new[] { ally }, new[] { boss });
        state.Enemies[0].TakeDamage(1000f);

        _ = new BattleSimulator(state, 10).Step();

        Assert.That(state.Allies[0].CurrentHealth, Is.EqualTo(100f).Within(0.001f));
    }

    private static BattleUnitLoadout CreateWaitingUnit(string id, float hp)
    {
        return CombatTestFactory.CreateUnit(
            id,
            hp: hp,
            tactics: new[]
            {
                new TacticRule(
                    0,
                    TacticConditionType.Fallback,
                    0f,
                    BattleActionType.WaitDefend,
                    TargetSelectorType.Self),
            });
    }

    private static void Advance(BattleSimulator simulator, int steps)
    {
        for (var index = 0; index < steps; index++)
        {
            simulator.Step();
        }
    }
}
