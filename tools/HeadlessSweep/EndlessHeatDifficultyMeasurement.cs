using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Core.Numerics;
using SM.Editor.Validation;
using SM.Meta.Services;

internal static class EndlessHeatDifficultyMeasurement
{
    internal static EndlessHeatDifficultyReport Measure(
        IReadOnlyList<EndlessHeatPreparedScenario> prepared,
        IReadOnlyList<EndlessHeatPreparedScenario> validationPrepared,
        string targetSiteId,
        IReadOnlyList<int> heats,
        int gearHorizonMaps,
        string validationBuildId,
        int degree)
    {
        var representative = MeasureArm(
            prepared,
            heats,
            degree,
            stateFactory: (scenario, heat) =>
            {
                var state = scenario.State.CloneWithHeat(0);
                _ = state.FarmSiteMaps(targetSiteId, gearHorizonMaps);
                _ = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
                return state.CloneWithHeat(heat);
            },
            phasePrefix: "endless-fixed",
            gearHorizonMaps);

        var validation = MeasureArm(
            validationPrepared,
            heats,
            degree,
            stateFactory: (scenario, heat) =>
            {
                var state = scenario.State.CloneWithHeat(0);
                if (string.Equals(scenario.Cell.Squad.SquadId, "frontline", StringComparison.Ordinal))
                {
                    _ = state.FarmSiteMaps(targetSiteId, gearHorizonMaps);
                    _ = HeadlessCampaignEquipmentPowerPolicy.Apply(state);
                }

                return state.CloneWithHeat(heat);
            },
            phasePrefix: $"endless-nonceiling-{validationBuildId.ToLowerInvariant()}",
            gearHorizonMaps: -1);

        var observations = validation.Results
            .SelectMany(value => value.Results.Select(result => new FirthClearObservation(
                result.SquadId,
                result.SeedSalt,
                value.Aggregate.Heat,
                result.Battle.Won)))
            .ToArray();
        var fit = FirthLogisticRegression.Fit(observations);
        var heatZero = validation.Aggregates.Single(value => value.Heat == 0);
        var heatThree = validation.Aggregates.Single(value => value.Heat == 3);
        var losses = heatZero.Cells
            .Select(cell =>
            {
                var paired = heatThree.Cells.Single(value =>
                    string.Equals(value.SquadId, cell.SquadId, StringComparison.Ordinal));
                return (cell.WinRate - paired.WinRate) * 100d;
            })
            .ToArray();
        var lossSpread = losses.Max() - losses.Min();
        var allInRange = heatZero.Cells.All(value => value.WinRate >= 0.20d && value.WinRate <= 0.90d);
        var validationCell = new EndlessHeatValidationCell(
            Description:
            "Measurement-only fixed-loadout cell at the shipped worldscar boss. Frontline uses the "
            + $"{gearHorizonMaps}-map H0 P80 frozen snapshot; mixed and ranged are independently "
            + $"prepared through the campaign as {validationBuildId} (without P80 passive growth). "
            + "Each resulting loadout is frozen before applying Heat.",
            FrontlineLoadout: $"P80 plus {gearHorizonMaps} deterministic H0 farm maps",
            MixedLoadout: $"{validationBuildId} independently captured fixed boss-state loadout",
            RangedLoadout: $"{validationBuildId} independently captured fixed boss-state loadout",
            ProductionHeatZeroUnchanged: true,
            AllHeatZeroRatesWithinTwentyToNinety: allInRange);
        var neutrality = new EndlessHeatNeutralityFit(
            Method:
            "Firth bias-reduced logistic regression with composition intercepts, a shared linear Heat "
            + "term, composition-by-Heat interactions, and fixed categorical seed-salt effects. "
            + "Reported gammas are centered composition-specific total Heat slopes.",
            fit.Converged,
            fit.Iterations,
            fit.GammaByComposition,
            fit.MaxGammaSpread,
            lossSpread);
        return new EndlessHeatDifficultyReport(
            validationCell,
            representative.Aggregates,
            validation.Aggregates,
            neutrality);
    }

    private static ArmMeasurement MeasureArm(
        IReadOnlyList<EndlessHeatPreparedScenario> prepared,
        IReadOnlyList<int> heats,
        int degree,
        Func<EndlessHeatPreparedScenario, int, HeadlessCampaignState> stateFactory,
        string phasePrefix,
        int gearHorizonMaps)
    {
        var measurements = new List<HeatMeasurement>(heats.Count);
        foreach (var heat in heats)
        {
            var results = new ScenarioResult[prepared.Count];
            Parallel.ForEach(
                Enumerable.Range(0, prepared.Count),
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                index =>
                {
                    var scenario = prepared[index];
                    var state = stateFactory(scenario, heat);
                    var battle = EndlessHeatSweepRunner.RunMeasuredBattle(
                        state,
                        scenario.Cell,
                        $"{phasePrefix}-h{heat}");
                    results[index] = new ScenarioResult(
                        scenario.SeedSalt,
                        scenario.Cell.Squad.SquadId,
                        battle);
                });
            measurements.Add(new HeatMeasurement(
                Aggregate(heat, gearHorizonMaps, results),
                results));
        }

        return new ArmMeasurement(
            measurements.Select(value => value.Aggregate).ToArray(),
            measurements);
    }

    private static EndlessHeatDifficultyAggregate Aggregate(
        int heat,
        int gearHorizonMaps,
        IReadOnlyList<ScenarioResult> results)
    {
        var cells = results
            .GroupBy(value => value.SquadId, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var samples = group.Count();
                var wins = group.Count(value => value.Battle.Won);
                return new EndlessHeatCellClearRate(
                    group.Key,
                    wins,
                    samples,
                    wins / (double)samples);
            })
            .ToArray();
        var targetRows = AggregateTargets(results);
        var normalizedRaw = results.Sum(value =>
            value.Battle.SecondaryPressureTelemetry.NormalizedDamageBudgetRaw);
        var primaryRaw = results.Sum(value =>
            value.Battle.SecondaryPressureTelemetry.PrimaryRawBudgetRaw);
        var primaryAfterMitigationRaw = targetRows.Sum(value =>
            value.PrimaryDamageAfterMitigationRaw);
        var secondaryRaw = results.Sum(value =>
            value.Battle.SecondaryPressureTelemetry.SecondaryRawAllocated);
        var secondaryAfterMitigationRaw = results.Sum(value =>
            value.Battle.SecondaryPressureTelemetry.SecondaryDamageAfterMitigationRaw);
        var rawOutput = primaryRaw + secondaryRaw;
        return new EndlessHeatDifficultyAggregate(
            heat,
            gearHorizonMaps,
            results.Count(value => value.Battle.Won),
            results.Count,
            results.Count(value => value.Battle.Won) / (double)results.Count,
            results.Average(value => value.Battle.Result.DurationSeconds),
            cells,
            normalizedRaw,
            primaryRaw,
            primaryAfterMitigationRaw,
            secondaryRaw,
            secondaryAfterMitigationRaw,
            normalizedRaw == 0 ? 0d : primaryRaw / (double)normalizedRaw,
            normalizedRaw == 0 ? 0d : secondaryRaw / (double)normalizedRaw,
            rawOutput == 0 ? 0d : secondaryRaw / (double)rawOutput,
            targetRows,
            HashEnemyModifierPackage(heat),
            HashSecondaryAllocations(results),
            HashBattleOutcomes(results));
    }

    private static IReadOnlyList<EndlessHeatTargetPressureAggregate> AggregateTargets(
        IReadOnlyList<ScenarioResult> results)
    {
        var rows = new Dictionary<string, MutableTargetRow>(StringComparer.Ordinal);
        foreach (var scenario in results
                     .OrderBy(value => value.SquadId, StringComparer.Ordinal)
                     .ThenBy(value => value.SeedSalt))
        {
            var finalAllies = scenario.Battle.Result.FinalUnits
                .Where(value => value.Side == TeamSide.Ally)
                .ToDictionary(value => value.Id, value => value, StringComparer.Ordinal);
            var enemyIds = scenario.Battle.Result.FinalUnits
                .Where(value => value.Side == TeamSide.Enemy)
                .Select(value => value.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var ally in finalAllies.Values)
            {
                var row = GetOrAdd(rows, ally.Id, ally.Anchor.ToString());
                row.BattlesPresent++;
                if (!ally.IsAlive)
                {
                    row.Deaths++;
                }
            }

            foreach (var battleEvent in scenario.Battle.Result.Events)
            {
                var targetId = battleEvent.TargetId?.Value ?? string.Empty;
                if (!finalAllies.TryGetValue(targetId, out var target)
                    || !enemyIds.Contains(battleEvent.ActorId.Value)
                    || battleEvent.Value <= 0f
                    || battleEvent.LogCode is not (BattleLogCode.BasicAttackDamage
                        or BattleLogCode.ActiveSkillDamage))
                {
                    continue;
                }

                GetOrAdd(rows, targetId, target.Anchor.ToString()).PrimaryDamageAfterMitigationRaw
                    += Hp64.FromFloatQuantized(battleEvent.Value).Raw;
            }

            foreach (var action in scenario.Battle.SecondaryPressureTelemetry.Actions)
            {
                foreach (var recipient in action.Recipients)
                {
                    if (!finalAllies.TryGetValue(recipient.TargetId, out var target))
                    {
                        continue;
                    }

                    var row = GetOrAdd(rows, recipient.TargetId, target.Anchor.ToString());
                    row.SecondaryRawAllocated += recipient.RawAllocated;
                    row.SecondaryDamageAfterMitigationRaw += recipient.DamageAfterMitigationRaw;
                }
            }
        }

        return rows.Values
            .OrderBy(value => value.TargetId, StringComparer.Ordinal)
            .Select(value => new EndlessHeatTargetPressureAggregate(
                value.TargetId,
                value.Anchor,
                value.BattlesPresent,
                value.Deaths,
                value.PrimaryDamageAfterMitigationRaw,
                value.SecondaryRawAllocated,
                value.SecondaryDamageAfterMitigationRaw))
            .ToArray();
    }

    private static MutableTargetRow GetOrAdd(
        IDictionary<string, MutableTargetRow> rows,
        string targetId,
        string anchor)
    {
        if (!rows.TryGetValue(targetId, out var row))
        {
            row = new MutableTargetRow(targetId, anchor);
            rows[targetId] = row;
        }

        return row;
    }

    private static string HashEnemyModifierPackage(int heat)
    {
        var payload = new
        {
            heat,
            numeric = EndlessCycleService.BuildEnemyHeatPackages(heat),
            rules = EndlessCycleService.BuildEnemyHeatSecondaryPressurePackages(heat),
        };
        return Sha256(JsonConvert.SerializeObject(payload, Formatting.None, SerializerSettings()));
    }

    private static string HashSecondaryAllocations(IReadOnlyList<ScenarioResult> results)
    {
        var builder = new StringBuilder();
        foreach (var scenario in results
                     .OrderBy(value => value.SquadId, StringComparer.Ordinal)
                     .ThenBy(value => value.SeedSalt))
        {
            builder.Append(scenario.SquadId)
                .Append('|')
                .Append(scenario.SeedSalt.ToString(CultureInfo.InvariantCulture))
                .Append('|');
            foreach (var action in scenario.Battle.SecondaryPressureTelemetry.Actions)
            {
                builder.Append(action.StepIndex.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(action.ActorId)
                    .Append('>')
                    .Append(action.PrimaryTargetId)
                    .Append(':')
                    .Append(action.NormalizedDamageBudgetRaw.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(action.PrimaryRawBudgetRaw.ToString(CultureInfo.InvariantCulture))
                    .Append('[');
                foreach (var recipient in action.Recipients)
                {
                    builder.Append(recipient.TargetId)
                        .Append('=')
                        .Append(recipient.RawAllocated.ToString(CultureInfo.InvariantCulture))
                        .Append('/')
                        .Append(recipient.DamageAfterMitigationRaw.ToString(CultureInfo.InvariantCulture))
                        .Append(';');
                }

                builder.Append(']');
            }

            builder.AppendLine();
        }

        return Sha256(builder.ToString());
    }

    private static string HashBattleOutcomes(IReadOnlyList<ScenarioResult> results)
    {
        var builder = new StringBuilder();
        foreach (var scenario in results
                     .OrderBy(value => value.SquadId, StringComparer.Ordinal)
                     .ThenBy(value => value.SeedSalt))
        {
            builder.Append(scenario.SquadId)
                .Append('|')
                .Append(scenario.SeedSalt.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(JsonConvert.SerializeObject(
                    scenario.Battle.Result,
                    Formatting.None,
                    SerializerSettings()))
                .AppendLine();
        }

        return Sha256(builder.ToString());
    }

    private static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static JsonSerializerSettings SerializerSettings()
        => new()
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

    private sealed record ScenarioResult(
        int SeedSalt,
        string SquadId,
        EndlessHeatMeasuredBattle Battle);

    private sealed record HeatMeasurement(
        EndlessHeatDifficultyAggregate Aggregate,
        IReadOnlyList<ScenarioResult> Results);

    private sealed record ArmMeasurement(
        IReadOnlyList<EndlessHeatDifficultyAggregate> Aggregates,
        IReadOnlyList<HeatMeasurement> Results);

    private sealed class MutableTargetRow
    {
        internal MutableTargetRow(string targetId, string anchor)
        {
            TargetId = targetId;
            Anchor = anchor;
        }

        internal string TargetId { get; }
        internal string Anchor { get; }
        internal int BattlesPresent { get; set; }
        internal int Deaths { get; set; }
        internal long PrimaryDamageAfterMitigationRaw { get; set; }
        internal long SecondaryRawAllocated { get; set; }
        internal long SecondaryDamageAfterMitigationRaw { get; set; }
    }
}
