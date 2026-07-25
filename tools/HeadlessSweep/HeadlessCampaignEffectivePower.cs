using SM.Core.Stats;
using SM.Meta.Services;

internal static class HeadlessCampaignEffectivePower
{
    private static readonly StatKey[] PowerKeys =
    {
        StatKey.MaxHealth,
        StatKey.PhysPower,
        StatKey.MagPower,
    };

    internal static RefitFarmPowerObservation Measure(HeadlessCampaignState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var snapshot = state.BuildBattleSetup().AllySnapshot;
        var health = 0d;
        var offense = 0d;
        foreach (var ally in snapshot.Allies)
        {
            var stats = HeroEffectiveStatPreview.Resolve(ally, PowerKeys)
                .ToDictionary(value => value.Key, value => (double)value.EffectiveValue);
            health += Math.Max(0.000001d, stats.GetValueOrDefault(StatKey.MaxHealth));
            offense += Math.Max(
                0.000001d,
                stats.GetValueOrDefault(StatKey.PhysPower)
                + stats.GetValueOrDefault(StatKey.MagPower));
        }

        if (!double.IsFinite(health)
            || !double.IsFinite(offense)
            || health <= 0d
            || offense <= 0d)
        {
            throw new InvalidDataException(
                $"Effective-power measurement requires finite positive health/offense, got {health:R}/{offense:R}.");
        }

        var effectivePower = health * offense;
        var logPower = Math.Log(health) + Math.Log(offense);
        if (!double.IsFinite(effectivePower) || !double.IsFinite(logPower))
        {
            throw new InvalidDataException(
                $"Effective-power measurement was non-finite: power={effectivePower:R}, log={logPower:R}.");
        }

        return new RefitFarmPowerObservation(health, offense, effectivePower, logPower);
    }
}
