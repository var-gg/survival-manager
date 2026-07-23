using SM.Combat.Model;
using SM.Core.Contracts;
using SM.Editor.Validation;
using SM.Meta.Model;

internal sealed class BossKillDynamicsObserver
{
    private readonly string _bossInstanceId;
    private double? _bossDeathTimeSeconds;
    private double? _timeTo75PercentSeconds;
    private double? _timeTo50PercentSeconds;
    private double? _timeTo25PercentSeconds;

    internal BossKillDynamicsObserver(BattleState state, ResolvedEncounterContext encounter)
    {
        var captainDefinitionId = encounter.Enemies
            .FirstOrDefault(enemy => enemy.CompileTags?.Contains("boss_captain") == true)?.Id
            ?? encounter.Enemies.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException(
                $"Boss encounter '{encounter.Context.EncounterId}' has no enemy captain.");
        _bossInstanceId = state.Enemies
            .FirstOrDefault(unit => string.Equals(
                unit.Definition.Id,
                captainDefinitionId,
                StringComparison.Ordinal))?.Id.Value
            ?? throw new InvalidOperationException(
                $"Boss encounter '{encounter.Context.EncounterId}' did not compose captain '{captainDefinitionId}'.");
    }

    internal void Observe(BattleSimulationStep step)
    {
        var boss = step.Units.FirstOrDefault(unit => string.Equals(
            unit.Id,
            _bossInstanceId,
            StringComparison.Ordinal));
        if (boss == null || boss.MaxHealth <= 0f)
        {
            return;
        }

        var healthRatio = Math.Clamp(boss.CurrentHealth / boss.MaxHealth, 0f, 1f);
        RecordThreshold(ref _timeTo75PercentSeconds, healthRatio, 0.75f, step.TimeSeconds);
        RecordThreshold(ref _timeTo50PercentSeconds, healthRatio, 0.50f, step.TimeSeconds);
        RecordThreshold(ref _timeTo25PercentSeconds, healthRatio, 0.25f, step.TimeSeconds);
        if (!boss.IsAlive && !_bossDeathTimeSeconds.HasValue)
        {
            _bossDeathTimeSeconds = step.TimeSeconds;
        }
    }

    internal HeadlessCampaignBossKillDynamicsSample Complete(BattleResult result)
    {
        var telemetry = result.TelemetryEvents ?? Array.Empty<TelemetryEventRecord>();
        var bossDamageDealt = telemetry
            .Where(value => value.EventKind == TelemetryEventKind.DamageApplied
                            && string.Equals(value.Actor?.UnitInstanceId, _bossInstanceId, StringComparison.Ordinal)
                            && value.Target?.SideIndex == (int)TeamSide.Ally)
            .Sum(value => Math.Max(0f, value.ValueA));
        var playerDeaths = result.FinalUnits.Count(unit =>
            unit.Side == TeamSide.Ally
            && unit.EntityKind == CombatEntityKind.RosterUnit
            && !unit.IsAlive);

        return new HeadlessCampaignBossKillDynamicsSample(
            result.DurationSeconds,
            _bossDeathTimeSeconds,
            bossDamageDealt,
            playerDeaths,
            _timeTo75PercentSeconds,
            _timeTo50PercentSeconds,
            _timeTo25PercentSeconds);
    }

    private static void RecordThreshold(
        ref double? destination,
        float healthRatio,
        float threshold,
        float timeSeconds)
    {
        if (!destination.HasValue && healthRatio <= threshold)
        {
            destination = timeSeconds;
        }
    }
}

internal static class BossKillDynamicsAggregator
{
    internal static IReadOnlyList<HeadlessCampaignBossKillDynamicsBand> Build(
        IReadOnlyList<HeadlessCampaignCellExecution> executions,
        CampaignBalanceSweepConfig config)
    {
        var encounterIds = executions
            .SelectMany(cell => cell.Arms)
            .SelectMany(arm => arm.Nodes)
            .Where(node => node.Identity.IsBoss && node.BossKillDynamics != null)
            .OrderBy(node => node.Identity.ChapterOrder)
            .ThenBy(node => node.Identity.SiteOrder)
            .ThenBy(node => node.Identity.NodeOrder)
            .Select(node => node.Identity.EncounterId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return encounterIds
            .Select(encounterId => new HeadlessCampaignBossKillDynamicsBand(
                encounterId,
                BuildArm(executions, config.Arms[0], encounterId),
                BuildArm(executions, config.Arms[1], encounterId)))
            .ToArray();
    }

    private static HeadlessCampaignBossKillDynamicsArm BuildArm(
        IReadOnlyList<HeadlessCampaignCellExecution> executions,
        CampaignBalanceArmSpec arm,
        string encounterId)
    {
        var samples = executions
            .SelectMany(cell => cell.Arms)
            .Where(execution => string.Equals(execution.Arm.ArmId, arm.ArmId, StringComparison.Ordinal))
            .SelectMany(execution => execution.Nodes)
            .Where(node => string.Equals(node.Identity.EncounterId, encounterId, StringComparison.Ordinal))
            .Select(node => node.BossKillDynamics)
            .Where(sample => sample != null)
            .Cast<HeadlessCampaignBossKillDynamicsSample>()
            .ToArray();

        return new HeadlessCampaignBossKillDynamicsArm(
            arm.ArmId,
            arm.PolicyId,
            samples.Length,
            Median(samples.Select(sample => sample.DurationSeconds)),
            MedianNullable(samples.Select(sample => sample.BossDeathTimeSeconds)),
            Median(samples.Select(sample => sample.BossDamageDealt)),
            samples.Sum(sample => sample.PlayerDeaths),
            samples.Count(sample => sample.PlayerDeaths > 0),
            MedianNullable(samples.Select(sample => sample.TimeTo75PercentSeconds)),
            MedianNullable(samples.Select(sample => sample.TimeTo50PercentSeconds)),
            MedianNullable(samples.Select(sample => sample.TimeTo25PercentSeconds)));
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
        {
            return 0d;
        }

        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2d
            : ordered[middle];
    }

    private static double? MedianNullable(IEnumerable<double?> values)
    {
        var present = values.Where(value => value.HasValue).Select(value => value!.Value).ToArray();
        return present.Length == 0 ? null : Median(present);
    }
}
