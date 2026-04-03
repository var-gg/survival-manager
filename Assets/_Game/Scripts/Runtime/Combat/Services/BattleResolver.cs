using SM.Combat.Model;

namespace SM.Combat.Services;

public static class BattleResolver
{
    public static BattleResult Run(BattleState state, int maxTicks = BattleSimulator.DefaultMaxSteps)
    {
        var simulator = new BattleSimulator(state, maxTicks);
        return simulator.RunToEnd();
    }
}
