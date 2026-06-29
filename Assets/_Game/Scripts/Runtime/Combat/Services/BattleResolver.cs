using System;
using SM.Combat.Model;

namespace SM.Combat.Services;

public static class BattleResolver
{
    // onStep: 매 시뮬레이션 스텝을 관전자에게 흘려보낸다(예: 헤드리스가 BattleHighlightLedger로
    // formation payoff를 집계). 순수 관찰 — 반환 BattleResult는 onStep 유무와 무관하게 byte-identical.
    public static BattleResult Run(
        BattleState state,
        int maxTicks = BattleSimulator.DefaultMaxSteps,
        Action<BattleSimulationStep>? onStep = null)
    {
        var simulator = new BattleSimulator(state, maxTicks);
        return simulator.RunToEnd(onStep);
    }
}
