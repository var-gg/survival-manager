using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

internal sealed record EndlessHeatRewardGridCell(
    double MeanNumerator,
    double JackpotStep,
    IReadOnlyList<EndlessHeatEquippedAggregate> Equipped,
    IReadOnlyList<EndlessHeatDropAggregate> Drops);

internal sealed record EndlessHeatRewardGridMetric(
    string Knob,
    int Heat,
    int HorizonMaps,
    double Low,
    double High,
    double EpicPlusPointsPerUnit,
    EndlessHeatConfidenceInterval EpicPlusPointsPerUnitClusteredCi95,
    double LegendaryPointsPerUnit,
    EndlessHeatConfidenceInterval LegendaryPointsPerUnitClusteredCi95);

internal sealed record EndlessHeatRewardGridAdjacentComparison(
    string Knob,
    double Low,
    double High,
    double HeldOtherKnob,
    int Heat,
    int HorizonMaps,
    double EpicPlusDeltaPoints,
    EndlessHeatConfidenceInterval EpicPlusDeltaPointsClusteredCi95,
    bool EpicPlusInsideNoise,
    double LegendaryDeltaPoints,
    EndlessHeatConfidenceInterval LegendaryDeltaPointsClusteredCi95,
    bool LegendaryInsideNoise);

internal sealed record EndlessHeatMeanLegendaryLeverage(
    int Heat,
    int HorizonMaps,
    double LowMeanNumerator,
    double HighMeanNumerator,
    double JackpotStep,
    double LatentMeanShiftDelta,
    double LegendaryDropPointsPerLatentGrade,
    EndlessHeatConfidenceInterval ClusteredCi95);

internal sealed record EndlessHeatRewardGridReport(
    string SchemaVersion,
    int SeedsPerCell,
    int CellsPerTuning,
    IReadOnlyList<int> Heats,
    IReadOnlyList<int> HorizonsMaps,
    IReadOnlyList<double> MeanNumerators,
    IReadOnlyList<double> JackpotSteps,
    double MinimumResolvableEquippedShare,
    int BootstrapSeedClusters,
    int BootstrapReplicates,
    string BootstrapMethod,
    string MarginalEffectMethod,
    IReadOnlyList<EndlessHeatRewardGridCell> Grid,
    IReadOnlyList<EndlessHeatRewardGridMetric> MarginalEffects,
    IReadOnlyList<EndlessHeatRewardGridAdjacentComparison> AdjacentComparisons,
    IReadOnlyList<EndlessHeatMeanLegendaryLeverage> MeanLegendaryLeverage,
    IReadOnlyDictionary<int, string> HeatZeroHashesByHorizon,
    bool HeatZeroIdenticalAcrossGrid,
    string CanonicalHash);

internal static class EndlessHeatRewardGridMeasurement
{
    internal static readonly double[] MeanNumerators = { 0.12d, 0.15d, 0.18d, 0.21d };
    internal static readonly double[] JackpotSteps = { 0.000d, 0.002d, 0.004d };
    internal static readonly int[] MeasuredHeats = { 0, 1, 3 };

    internal static EndlessHeatRewardGridReport Measure(
        IReadOnlyList<EndlessHeatPreparedScenario> prepared,
        string targetSiteId,
        IReadOnlyList<int> horizons,
        int degree)
    {
        var measured = new List<MeasuredCell>(MeanNumerators.Length * JackpotSteps.Length);
        foreach (var meanNumerator in MeanNumerators)
        {
            foreach (var jackpotStep in JackpotSteps)
            {
                measured.Add(new MeasuredCell(
                    meanNumerator,
                    jackpotStep,
                    EndlessHeatRewardMeasurement.Measure(
                        prepared,
                        targetSiteId,
                        horizons,
                        MeasuredHeats,
                        degree,
                        meanNumerator,
                        jackpotStep)));
            }
        }

        var heatZeroHashes = horizons.ToDictionary(
            horizon => horizon,
            horizon => RequireSingleHeatZeroHash(measured, horizon));
        var report = new EndlessHeatRewardGridReport(
            SchemaVersion: "endless-heat-reward-grid-v1",
            SeedsPerCell: prepared.Count / 3,
            CellsPerTuning: 3,
            Heats: MeasuredHeats,
            HorizonsMaps: horizons,
            MeanNumerators,
            JackpotSteps,
            MinimumResolvableEquippedShare:
            1d / (prepared.Count
                  * HeadlessCampaignEquipmentPowerPolicy.ExpectedHeroCount
                  * HeadlessCampaignEquipmentPowerPolicy.ExpectedSlotsPerHero),
            BootstrapSeedClusters: prepared.Select(value => value.SeedSalt).Distinct().Count(),
            BootstrapReplicates: EndlessHeatSeedClusteredBootstrap.Replicates,
            BootstrapMethod:
            "Paired percentile bootstrap over seed salts; each seed cluster retains all three canonical squads and every equipped slot remains inside its scenario.",
            MarginalEffectMethod:
            "Endpoint finite-difference slope averaged with equal weight over every level of the held knob; effects are equipped-share percentage points per one knob unit and preserve seed/squad pairing.",
            Grid: measured
                .Select(value => new EndlessHeatRewardGridCell(
                    value.MeanNumerator,
                    value.JackpotStep,
                    value.Measurement.Equipped,
                    value.Measurement.Drops))
                .ToArray(),
            MarginalEffects: BuildMarginalEffects(measured, horizons),
            AdjacentComparisons: BuildAdjacentComparisons(measured, horizons),
            MeanLegendaryLeverage: BuildMeanLegendaryLeverage(measured, horizons),
            HeatZeroHashesByHorizon: heatZeroHashes,
            HeatZeroIdenticalAcrossGrid: true,
            CanonicalHash: string.Empty);
        return report with { CanonicalHash = HashReport(report) };
    }

    private static IReadOnlyList<EndlessHeatRewardGridMetric> BuildMarginalEffects(
        IReadOnlyList<MeasuredCell> measured,
        IReadOnlyList<int> horizons)
    {
        var effects = new List<EndlessHeatRewardGridMetric>();
        foreach (var heat in MeasuredHeats.Where(value => value > 0))
        {
            foreach (var horizon in horizons)
            {
                effects.Add(BuildMarginalEffect(measured, "mean_numerator", heat, horizon));
                effects.Add(BuildMarginalEffect(measured, "jackpot_step", heat, horizon));
            }
        }

        return effects;
    }

    private static EndlessHeatRewardGridMetric BuildMarginalEffect(
        IReadOnlyList<MeasuredCell> measured,
        string knob,
        int heat,
        int horizon)
    {
        var meanKnob = string.Equals(knob, "mean_numerator", StringComparison.Ordinal);
        var low = meanKnob ? MeanNumerators[0] : JackpotSteps[0];
        var high = meanKnob ? MeanNumerators[^1] : JackpotSteps[^1];
        var heldLevels = meanKnob ? JackpotSteps : MeanNumerators;
        var epicClusters = BuildAveragedEndpointSlopeClusters(
            measured,
            meanKnob,
            heldLevels,
            low,
            high,
            heat,
            horizon,
            value => value.Loadout.EpicPlusShare);
        var legendaryClusters = BuildAveragedEndpointSlopeClusters(
            measured,
            meanKnob,
            heldLevels,
            low,
            high,
            heat,
            horizon,
            value => value.Loadout.LegendaryShare);
        var identity = $"grid-marginal|{knob}|h={heat}|maps={horizon}";
        return new EndlessHeatRewardGridMetric(
            knob,
            heat,
            horizon,
            low,
            high,
            epicClusters.Average(value => value.Value),
            EndlessHeatSeedClusteredBootstrap.EstimateMean(
                epicClusters,
                identity + "|epic"),
            legendaryClusters.Average(value => value.Value),
            EndlessHeatSeedClusteredBootstrap.EstimateMean(
                legendaryClusters,
                identity + "|legendary"));
    }

    private static IReadOnlyList<EndlessHeatRewardGridAdjacentComparison> BuildAdjacentComparisons(
        IReadOnlyList<MeasuredCell> measured,
        IReadOnlyList<int> horizons)
    {
        var comparisons = new List<EndlessHeatRewardGridAdjacentComparison>();
        foreach (var heat in MeasuredHeats.Where(value => value > 0))
        {
            foreach (var horizon in horizons)
            {
                AddAdjacentComparisons(
                    comparisons,
                    measured,
                    meanKnob: true,
                    MeanNumerators,
                    JackpotSteps,
                    heat,
                    horizon);
                AddAdjacentComparisons(
                    comparisons,
                    measured,
                    meanKnob: false,
                    JackpotSteps,
                    MeanNumerators,
                    heat,
                    horizon);
            }
        }

        return comparisons;
    }

    private static void AddAdjacentComparisons(
        ICollection<EndlessHeatRewardGridAdjacentComparison> destination,
        IReadOnlyList<MeasuredCell> measured,
        bool meanKnob,
        IReadOnlyList<double> levels,
        IReadOnlyList<double> heldLevels,
        int heat,
        int horizon)
    {
        for (var levelIndex = 0; levelIndex < levels.Count - 1; levelIndex++)
        {
            var low = levels[levelIndex];
            var high = levels[levelIndex + 1];
            foreach (var held in heldLevels)
            {
                var lowCell = FindCell(
                    measured,
                    meanKnob ? low : held,
                    meanKnob ? held : low);
                var highCell = FindCell(
                    measured,
                    meanKnob ? high : held,
                    meanKnob ? held : high);
                var epicClusters = BuildPairedEquippedDeltaClusters(
                    lowCell,
                    highCell,
                    heat,
                    horizon,
                    value => value.Loadout.EpicPlusShare,
                    scale: 100d);
                var legendaryClusters = BuildPairedEquippedDeltaClusters(
                    lowCell,
                    highCell,
                    heat,
                    horizon,
                    value => value.Loadout.LegendaryShare,
                    scale: 100d);
                var knob = meanKnob ? "mean_numerator" : "jackpot_step";
                var identity =
                    $"grid-adjacent|{knob}|{low:R}-{high:R}|held={held:R}|h={heat}|maps={horizon}";
                var epicInterval = EndlessHeatSeedClusteredBootstrap.EstimateMean(
                    epicClusters,
                    identity + "|epic");
                var legendaryInterval = EndlessHeatSeedClusteredBootstrap.EstimateMean(
                    legendaryClusters,
                    identity + "|legendary");
                destination.Add(new EndlessHeatRewardGridAdjacentComparison(
                    knob,
                    low,
                    high,
                    held,
                    heat,
                    horizon,
                    epicClusters.Average(value => value.Value),
                    epicInterval,
                    IncludesZero(epicInterval),
                    legendaryClusters.Average(value => value.Value),
                    legendaryInterval,
                    IncludesZero(legendaryInterval)));
            }
        }
    }

    private static IReadOnlyList<EndlessHeatMeanLegendaryLeverage> BuildMeanLegendaryLeverage(
        IReadOnlyList<MeasuredCell> measured,
        IReadOnlyList<int> horizons)
    {
        var result = new List<EndlessHeatMeanLegendaryLeverage>();
        var low = FindCell(measured, MeanNumerators[0], JackpotSteps[0]);
        var high = FindCell(measured, MeanNumerators[^1], JackpotSteps[0]);
        foreach (var heat in MeasuredHeats.Where(value => value > 0))
        {
            var latentShiftDelta = ((MeanNumerators[^1] - MeanNumerators[0]) * heat)
                                   / (1d
                                      + (SM.Meta.Services.EndlessCycleService
                                          .HeatDropLatentMeanDenominatorSlope * heat));
            foreach (var horizon in horizons)
            {
                var clusters = BuildPairedDropDeltaClusters(
                    low,
                    high,
                    heat,
                    horizon,
                    observation => (int)observation.Grade == 4 ? 1d : 0d,
                    scale: 100d / latentShiftDelta);
                var identity = $"grid-mean-legendary-leverage|h={heat}|maps={horizon}";
                result.Add(new EndlessHeatMeanLegendaryLeverage(
                    heat,
                    horizon,
                    MeanNumerators[0],
                    MeanNumerators[^1],
                    JackpotSteps[0],
                    latentShiftDelta,
                    clusters.Average(value => value.Value),
                    EndlessHeatSeedClusteredBootstrap.EstimateMean(clusters, identity)));
            }
        }

        return result;
    }

    private static IReadOnlyList<EndlessHeatMeanCluster> BuildAveragedEndpointSlopeClusters(
        IReadOnlyList<MeasuredCell> measured,
        bool meanKnob,
        IReadOnlyList<double> heldLevels,
        double low,
        double high,
        int heat,
        int horizon,
        Func<EndlessHeatRewardScenarioResult, double> selector)
    {
        var slopes = heldLevels
            .Select(held =>
            {
                var lowCell = FindCell(
                    measured,
                    meanKnob ? low : held,
                    meanKnob ? held : low);
                var highCell = FindCell(
                    measured,
                    meanKnob ? high : held,
                    meanKnob ? held : high);
                return BuildPairedEquippedDeltaClusters(
                    lowCell,
                    highCell,
                    heat,
                    horizon,
                    selector,
                    scale: 100d / (high - low));
            })
            .ToArray();
        RequireAligned(slopes);
        return Enumerable.Range(0, slopes[0].Count)
            .Select(index => new EndlessHeatMeanCluster(
                slopes[0][index].SeedSalt,
                slopes.Average(values => values[index].Value)))
            .ToArray();
    }

    private static IReadOnlyList<EndlessHeatMeanCluster> BuildPairedEquippedDeltaClusters(
        MeasuredCell low,
        MeasuredCell high,
        int heat,
        int horizon,
        Func<EndlessHeatRewardScenarioResult, double> selector,
        double scale)
    {
        var lowClusters = BuildEquippedClusters(low, heat, horizon, selector);
        var highClusters = BuildEquippedClusters(high, heat, horizon, selector);
        RequireAligned(new[] { lowClusters, highClusters });
        return Enumerable.Range(0, lowClusters.Count)
            .Select(index => new EndlessHeatMeanCluster(
                lowClusters[index].SeedSalt,
                (highClusters[index].Value - lowClusters[index].Value) * scale))
            .ToArray();
    }

    private static IReadOnlyList<EndlessHeatMeanCluster> BuildPairedDropDeltaClusters(
        MeasuredCell low,
        MeasuredCell high,
        int heat,
        int horizon,
        Func<SM.Meta.Services.DropGradeRollObservation, double> numerator,
        double scale)
    {
        var lowClusters = BuildDropClusters(low, heat, horizon, numerator);
        var highClusters = BuildDropClusters(high, heat, horizon, numerator);
        RequireAligned(new[] { lowClusters, highClusters });
        return Enumerable.Range(0, lowClusters.Count)
            .Select(index => new EndlessHeatMeanCluster(
                lowClusters[index].SeedSalt,
                (highClusters[index].Value - lowClusters[index].Value) * scale))
            .ToArray();
    }

    private static IReadOnlyList<EndlessHeatMeanCluster> BuildEquippedClusters(
        MeasuredCell cell,
        int heat,
        int horizon,
        Func<EndlessHeatRewardScenarioResult, double> selector)
        => SelectScenarios(cell, heat, horizon)
            .GroupBy(value => value.SeedSalt)
            .OrderBy(group => group.Key)
            .Select(group => new EndlessHeatMeanCluster(
                group.Key,
                group.Average(selector)))
            .ToArray();

    private static IReadOnlyList<EndlessHeatMeanCluster> BuildDropClusters(
        MeasuredCell cell,
        int heat,
        int horizon,
        Func<SM.Meta.Services.DropGradeRollObservation, double> numerator)
        => SelectScenarios(cell, heat, horizon)
            .GroupBy(value => value.SeedSalt)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var observations = group
                    .SelectMany(value => value.Farm.GradeRolls)
                    .ToArray();
                return new EndlessHeatMeanCluster(
                    group.Key,
                    observations.Sum(numerator) / observations.Length);
            })
            .ToArray();

    private static IReadOnlyList<EndlessHeatRewardScenarioResult> SelectScenarios(
        MeasuredCell cell,
        int heat,
        int horizon)
        => cell.Measurement.Scenarios
            .Where(value => value.Heat == heat && value.HorizonMaps == horizon)
            .OrderBy(value => value.ScenarioIndex)
            .ToArray();

    private static MeasuredCell FindCell(
        IEnumerable<MeasuredCell> measured,
        double meanNumerator,
        double jackpotStep)
        => measured.Single(value =>
            value.MeanNumerator.Equals(meanNumerator)
            && value.JackpotStep.Equals(jackpotStep));

    private static string RequireSingleHeatZeroHash(
        IEnumerable<MeasuredCell> measured,
        int horizon)
    {
        var hashes = measured
            .Select(value => value.Measurement.Equipped.Single(aggregate =>
                aggregate.Heat == 0 && aggregate.HorizonMaps == horizon)
                .OrderedInventoryAndEquipHash)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (hashes.Length != 1)
        {
            throw new InvalidDataException(
                $"H0 equipment hashes diverged across the reward grid at {horizon} maps.");
        }

        return hashes[0];
    }

    private static void RequireAligned(
        IReadOnlyList<IReadOnlyList<EndlessHeatMeanCluster>> clusterSets)
    {
        if (clusterSets.Count == 0 || clusterSets[0].Count <= 1)
        {
            throw new InvalidDataException("Grid bootstrap requires at least two aligned seed clusters.");
        }

        var expected = clusterSets[0].Select(value => value.SeedSalt);
        if (clusterSets.Any(values => !values.Select(value => value.SeedSalt).SequenceEqual(expected)))
        {
            throw new InvalidDataException(
                "Grid comparison requires identical ordered seed-cluster vectors.");
        }
    }

    private static bool IncludesZero(EndlessHeatConfidenceInterval interval)
        => interval.Lower <= 0d && interval.Upper >= 0d;

    private static string HashReport(EndlessHeatRewardGridReport report)
    {
        var json = JsonConvert.SerializeObject(report, Formatting.None);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
    }

    private sealed record MeasuredCell(
        double MeanNumerator,
        double JackpotStep,
        EndlessHeatRewardMeasurementResult Measurement);
}
