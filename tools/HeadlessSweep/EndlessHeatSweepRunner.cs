using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Combat.Model;
using SM.Core.Stats;
using SM.Editor.Validation;
using SM.Meta;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class EndlessHeatSweepRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefaultOutputRelativePath = "Temp/HeadlessSweep/endless-heat-sweep.json";
    private const string TargetSiteId = "site_worldscar_depths";
    private const string TargetEncounterId = "site_worldscar_depths_boss_1";
    private static readonly int[] ClearRateHeats = { 0, 1, 2, 3, 4, 5 };
    private static readonly int[] ScalingProbeHeats = { 1, 3, 5, 10 };

    internal static int Run(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        try
        {
            var options = Parse(arguments);
            ContentSnapshotFreshnessGuard.EnsureFresh(repositoryRoot);
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var config = CampaignBalanceSweepConfig.Default;
            config.Validate();
            var cells = BuildReferenceCells(config);
            var prepared = PrepareScenarios(
                lookup,
                config,
                cells,
                options.SeedsPerCell,
                options.Degree);
            var scaling = VerifyEnemyScaling();
            var rewards = EndlessHeatRewardMeasurement.Measure(
                prepared,
                TargetSiteId,
                options.EquipmentHorizonsMaps,
                options.Degree);
            var clearRates = MeasureClearRates(
                prepared,
                options.EquipmentHorizonsMaps,
                options.Degree);
            var acquisition = EndlessHeatRewardMeasurement.BuildAcquisition(
                rewards.Scenarios,
                clearRates.Paired);

            var report = new EndlessHeatSweepReport(
                SchemaVersion: "endless-heat-sweep-v1",
                TargetEncounterId,
                TargetSiteId,
                ReferenceSquads: cells.Select(cell => cell.Squad.SquadId).ToArray(),
                SeedsPerCell: options.SeedsPerCell,
                AggregateSamplesPerHeat: prepared.Count,
                MinimumResolvableRatePerCell: 1d / options.SeedsPerCell,
                MinimumResolvableAggregateRate: 1d / prepared.Count,
                MinimumResolvableEquippedShare:
                1d / (prepared.Count
                      * HeadlessCampaignEquipmentPowerPolicy.ExpectedHeroCount
                      * HeadlessCampaignEquipmentPowerPolicy.ExpectedSlotsPerHero),
                BootstrapSeedClusters: options.SeedsPerCell,
                BootstrapReplicates: EndlessHeatSeedClusteredBootstrap.Replicates,
                BootstrapMethod:
                "Paired percentile bootstrap over campaign seed salts; each resampled seed cluster retains all three canonical squads and all 12 equipped slots remain inside their scenario cluster.",
                EquipmentHeats: EndlessHeatRewardMeasurement.Heats,
                ClearRateHeats,
                EquipmentHorizonsMaps: options.EquipmentHorizonsMaps,
                PairedClearRateHorizonMaps: options.PairedClearRateHorizonMaps,
                BattleRewardNodesPerFarmMap: rewards.BattleRewardNodesPerMap,
                EquipmentPowerPolicy:
                "Exact maximum-total affix BudgetScore matching per slot across four heroes; grade and stable item order break equal-power ties.",
                EnemyScaling: scaling,
                EquippedByHeat: rewards.Equipped,
                DropsByHeat: rewards.Drops,
                AcquisitionByHeat: acquisition,
                ClearRateFixedGear: clearRates.Fixed,
                ClearRatePairedGear: clearRates.Paired,
                Pairing: clearRates.Pairing,
                ClearRateCodePath:
                "HeadlessCampaignPlaythrough.RunBattle on a prepared final-boss state. FindProgressionResult is used only to reach prior campaign nodes and never participates in either reported clear-rate arm.",
                CanonicalHash: string.Empty);
            var canonicalHash = HashReport(report);
            report = report with { CanonicalHash = canonicalHash };

            var outputPath = Resolve(repositoryRoot, options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(
                outputPath,
                JsonConvert.SerializeObject(report, Formatting.Indented, SerializerSettings()));
            Console.WriteLine(
                $"endless-heat-sweep MATCH cells={cells.Count} seeds-per-cell={options.SeedsPerCell} "
                + $"samples-per-heat={prepared.Count} paired-horizon={options.PairedClearRateHorizonMaps} "
                + $"hash={canonicalHash}");
            Console.WriteLine($"endless-heat-sweep report={outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"endless-heat-sweep ERROR: {exception}");
            return 2;
        }
    }

    private static IReadOnlyList<CampaignBalanceGridCell> BuildReferenceCells(
        CampaignBalanceSweepConfig config)
    {
        var build = config.BuildPowerQuantiles.Single(value =>
            string.Equals(value.QuantileId, "P80", StringComparison.Ordinal));
        var enemy = config.EnemyCompositionVariants.Single(value => value.VariantIndex == 0);
        var coverage = config.RosterCoverageVariants.Single(value => value.BenchArchetypeCount == 0);
        return config.ReferenceSquads
            .Select(squad => new CampaignBalanceGridCell(squad, build, enemy, coverage))
            .ToArray();
    }

    private static IReadOnlyList<EndlessHeatPreparedScenario> PrepareScenarios(
        SnapshotSessionContentLookup lookup,
        CampaignBalanceSweepConfig config,
        IReadOnlyList<CampaignBalanceGridCell> cells,
        int seedsPerCell,
        int degree)
    {
        var arm = config.Arms.Single(value =>
            string.Equals(value.ArmId, "informed", StringComparison.Ordinal));
        var jobs = cells
            .SelectMany(cell => Enumerable.Range(0, seedsPerCell)
                .Select(seedSalt => new ScenarioJob(cell, seedSalt)))
            .ToArray();
        var prepared = new EndlessHeatPreparedScenario[jobs.Length];
        Parallel.ForEach(
            Enumerable.Range(0, jobs.Length),
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            index =>
            {
                var job = jobs[index];
                HeadlessCampaignState? captured = null;
                HeadlessCampaignPlaythrough.Run(
                    lookup,
                    config,
                    arm,
                    job.Cell,
                    TargetEncounterId,
                    campaignSeedSalt: job.SeedSalt,
                    heat: 0,
                    beforeMeasuredBattle: (state, identity) =>
                    {
                        if (!string.Equals(identity.EncounterId, TargetEncounterId, StringComparison.Ordinal))
                        {
                            return;
                        }

                        if (captured != null)
                        {
                            throw new InvalidOperationException(
                                $"Target encounter '{TargetEncounterId}' was captured more than once.");
                        }

                        captured = state.CloneWithHeat(0);
                    });
                prepared[index] = new EndlessHeatPreparedScenario(
                    job.Cell,
                    job.SeedSalt,
                    captured
                    ?? throw new InvalidOperationException(
                        $"Target encounter '{TargetEncounterId}' was not captured for {job.Cell.CellId}/seed={job.SeedSalt}."));
            });
        return prepared;
    }

    private static IReadOnlyList<EndlessHeatEnemyScalingObservation> VerifyEnemyScaling()
    {
        var enemy = new BattleUnitLoadout(
            "heat-probe-enemy",
            "Heat Probe",
            "human",
            "vanguard",
            DeploymentAnchorId.FrontCenter,
            new Dictionary<StatKey, float>
            {
                [StatKey.MaxHealth] = 100f,
                [StatKey.PhysPower] = 40f,
                [StatKey.MagPower] = 25f,
            },
            Array.Empty<UnitRuleChain>(),
            Array.Empty<BattleSkillSpec>());
        var observations = new List<EndlessHeatEnemyScalingObservation>();
        foreach (var heat in ScalingProbeHeats)
        {
            var packages = EndlessCycleService.BuildEnemyHeatPackages(heat);
            var applied = PoliticalCombatConditionService.ApplyEnemyPackages(
                new[] { enemy },
                packages).Single();
            var values = HeroEffectiveStatPreview.Resolve(
                    applied,
                    new[] { StatKey.MaxHealth, StatKey.PhysPower, StatKey.MagPower })
                .ToDictionary(value => value.Key, value => value);
            var hpMultiplier = values[StatKey.MaxHealth].EffectiveValue
                               / values[StatKey.MaxHealth].BaseValue;
            var physMultiplier = values[StatKey.PhysPower].EffectiveValue
                                 / values[StatKey.PhysPower].BaseValue;
            var magMultiplier = values[StatKey.MagPower].EffectiveValue
                                / values[StatKey.MagPower].BaseValue;
            var expectedHp = 1d + (EndlessCycleService.HeatMaxHealthIncreasedPerHeat * heat);
            var expectedPower = 1d + (EndlessCycleService.HeatPowerIncreasedPerHeat * heat);
            RequireNear(hpMultiplier, expectedHp, $"Heat {heat} MaxHealth multiplier");
            RequireNear(physMultiplier, expectedPower, $"Heat {heat} PhysPower multiplier");
            RequireNear(magMultiplier, expectedPower, $"Heat {heat} MagPower multiplier");
            observations.Add(new EndlessHeatEnemyScalingObservation(
                heat,
                hpMultiplier,
                physMultiplier,
                magMultiplier,
                applied.NumericPackages.Any(package =>
                    string.Equals(package.SourceId, $"endless_heat:h{heat}", StringComparison.Ordinal))));
        }

        if (observations.Any(value => !value.ProductionPackagePresent))
        {
            throw new InvalidOperationException(
                "Heat scaling probe did not observe the EndlessCycleService package after ApplyEnemyPackages.");
        }

        return observations;
    }

    private static ClearRateMeasurement MeasureClearRates(
        IReadOnlyList<EndlessHeatPreparedScenario> prepared,
        IReadOnlyList<int> pairedHorizons,
        int degree)
    {
        var fixedAggregates = new List<EndlessHeatClearRateAggregate>();
        var pairedAggregates = new List<EndlessHeatClearRateAggregate>();
        var pairChecks = new List<PairCheck>();
        foreach (var heat in ClearRateHeats)
        {
            var fixedResults = new MeasuredScenarioBattle[prepared.Count];
            Parallel.ForEach(
                Enumerable.Range(0, prepared.Count),
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                index =>
                {
                    var scenario = prepared[index];
                    var fixedState = scenario.State.CloneWithHeat(heat);
                    var fixedBattle = RunMeasuredBattle(fixedState, scenario.Cell, "endless-fixed");
                    fixedResults[index] = new MeasuredScenarioBattle(
                        scenario.SeedSalt,
                        scenario.Cell.Squad.SquadId,
                        fixedBattle.Won,
                        fixedBattle);
                });

            fixedAggregates.Add(AggregateClearRate(
                heat,
                gearHorizonMaps: 0,
                results: fixedResults));
            foreach (var pairedHorizon in pairedHorizons)
            {
                var pairedResults = new MeasuredScenarioBattle[prepared.Count];
                var pairedChecks = new PairCheck[prepared.Count];
                Parallel.ForEach(
                    Enumerable.Range(0, prepared.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = degree },
                    index =>
                    {
                        var scenario = prepared[index];
                        var pairedState = scenario.State.CloneWithHeat(heat);
                        _ = pairedState.FarmSiteMaps(TargetSiteId, pairedHorizon);
                        _ = HeadlessCampaignEquipmentPowerPolicy.Apply(pairedState);
                        var pairedBattle = RunMeasuredBattle(
                            pairedState,
                            scenario.Cell,
                            $"endless-paired-{pairedHorizon}");
                        pairedResults[index] = new MeasuredScenarioBattle(
                            scenario.SeedSalt,
                            scenario.Cell.Squad.SquadId,
                            pairedBattle.Won,
                            pairedBattle);
                        pairedChecks[index] = new PairCheck(
                            fixedResults[index].Battle.BattleSeed == pairedBattle.BattleSeed,
                            fixedResults[index].Battle.EntityIds.SequenceEqual(
                                pairedBattle.EntityIds,
                                StringComparer.Ordinal));
                    });

                pairedAggregates.Add(AggregateClearRate(
                    heat,
                    pairedHorizon,
                    pairedResults));
                pairChecks.AddRange(pairedChecks);
            }
        }

        var pairing = new EndlessHeatPairingVerification(
            SeedsShared: pairChecks.All(value => value.SeedShared),
            EntityIdsShared: pairChecks.All(value => value.EntityIdsShared),
            PairsChecked: pairChecks.Count,
            Method:
            "For every heat/cell/seed pair, compare BattleContext.BattleSeed and the ordered composed BattleState ally+enemy entity-id vector before running either arm.");
        if (!pairing.SeedsShared || !pairing.EntityIdsShared)
        {
            throw new InvalidOperationException(
                $"Endless Heat arm pairing failed: seeds={pairing.SeedsShared}, ids={pairing.EntityIdsShared}.");
        }

        return new ClearRateMeasurement(fixedAggregates, pairedAggregates, pairing);
    }

    private static MeasuredBattle RunMeasuredBattle(
        HeadlessCampaignState state,
        CampaignBalanceGridCell cell,
        string phase)
    {
        var setup = state.BuildBattleSetup();
        var encounter = HeadlessCampaignPlaythrough.ProjectEncounter(
            setup.AuthoredEncounter,
            cell.EnemyComposition);
        if (!string.Equals(encounter.Context.EncounterId, TargetEncounterId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Prepared scenario points at '{encounter.Context.EncounterId}', expected '{TargetEncounterId}'.");
        }

        if (!SessionBattleStateComposer.TryCompose(
                state.Lookup,
                setup.AllySnapshot,
                encounter,
                out var battleState,
                out var error))
        {
            throw new InvalidOperationException(
                $"Endless Heat identity compose failed ({cell.CellId}/{phase}): {error}");
        }

        var entityIds = battleState.Allies
            .Select(unit => unit.Id.Value)
            .Concat(battleState.Enemies.Select(unit => unit.Id.Value))
            .ToArray();
        var result = HeadlessCampaignPlaythrough.RunBattle(
            state,
            setup.AllySnapshot,
            encounter,
            phase).Result;
        return new MeasuredBattle(
            result.Winner == TeamSide.Ally,
            encounter.Context.BattleSeed,
            entityIds);
    }

    private static EndlessHeatClearRateAggregate AggregateClearRate(
        int heat,
        int gearHorizonMaps,
        IReadOnlyList<MeasuredScenarioBattle> results)
    {
        var cells = results
            .GroupBy(result => result.SquadId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var samples = group.Count();
                var wins = group.Count(value => value.Won);
                return new EndlessHeatCellClearRate(
                    group.Key,
                    wins,
                    samples,
                    wins / (double)samples);
            })
            .ToArray();
        var totalWins = results.Count(value => value.Won);
        var clustered = results
            .GroupBy(value => value.SeedSalt)
            .OrderBy(group => group.Key)
            .Select(group => new EndlessHeatRatioCluster(
                group.Key,
                group.Count(value => value.Won),
                group.Count()))
            .ToArray();
        return new EndlessHeatClearRateAggregate(
            heat,
            gearHorizonMaps,
            totalWins,
            results.Count,
            SeedsPerCell: results.Count / cells.Length,
            WinRate: totalWins / (double)results.Count,
            Cells: cells,
            SeedClusteredCi95: EndlessHeatSeedClusteredBootstrap.EstimateRatio(
                clustered,
                $"clear-rate|h={heat}|maps={gearHorizonMaps}"));
    }

    private static string HashReport(EndlessHeatSweepReport report)
    {
        var json = JsonConvert.SerializeObject(report, Formatting.None, SerializerSettings());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static void RequireNear(double actual, double expected, string label)
    {
        if (Math.Abs(actual - expected) > 0.00001d)
        {
            throw new InvalidOperationException(
                $"{label} measured {actual.ToString("R", CultureInfo.InvariantCulture)}, "
                + $"expected {expected.ToString("R", CultureInfo.InvariantCulture)}.");
        }
    }

    private static Options Parse(IReadOnlyList<string> arguments)
    {
        var seedsPerCell = 32;
        var degree = Math.Max(1, Environment.ProcessorCount);
        IReadOnlyList<int> horizons = new[] { 25, 100 };
        var pairedHorizon = 25;
        var outputPath = DefaultOutputRelativePath;
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--seeds" when index + 1 < arguments.Count:
                    seedsPerCell = ParseInt(arguments[++index], "seeds", 1, 512);
                    break;
                case "--degree" when index + 1 < arguments.Count:
                    degree = ParseInt(arguments[++index], "degree", 1, 512);
                    break;
                case "--horizons" when index + 1 < arguments.Count:
                    horizons = ParsePositiveIntList(arguments[++index], "horizons", 1, 1000);
                    break;
                case "--paired-horizon" when index + 1 < arguments.Count:
                    pairedHorizon = ParseInt(arguments[++index], "paired-horizon", 1, 1000);
                    break;
                case "--output" when index + 1 < arguments.Count:
                    outputPath = arguments[++index];
                    break;
                default:
                    throw new ArgumentException($"Unknown endless-heat-sweep argument: {arguments[index]}");
            }
        }

        if (!horizons.Contains(pairedHorizon))
        {
            throw new ArgumentException(
                "--paired-horizon must also be present in --horizons so the clear-rate loadout is reported by Task 1.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output requires a non-empty path.");
        }

        return new Options(seedsPerCell, degree, horizons, pairedHorizon, outputPath);
    }

    private static int ParseInt(string value, string name, int minimum, int maximum)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            || result < minimum
            || result > maximum)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Expected {minimum}-{maximum}.");
        }

        return result;
    }

    private static IReadOnlyList<int> ParsePositiveIntList(
        string value,
        string name,
        int minimum,
        int maximum)
    {
        var values = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => ParseInt(item, name, minimum, maximum))
            .ToArray();
        if (values.Length == 0 || values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException($"--{name} requires distinct comma-separated integers.");
        }

        return values.OrderBy(item => item).ToArray();
    }

    private static JsonSerializerSettings SerializerSettings()
        => new()
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
        };

    private static string Resolve(string repositoryRoot, string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(
                repositoryRoot,
                path.Replace('/', Path.DirectorySeparatorChar)));

    private sealed record Options(
        int SeedsPerCell,
        int Degree,
        IReadOnlyList<int> EquipmentHorizonsMaps,
        int PairedClearRateHorizonMaps,
        string OutputPath);

    private sealed record ScenarioJob(CampaignBalanceGridCell Cell, int SeedSalt);
    private sealed record PairCheck(bool SeedShared, bool EntityIdsShared);
    private sealed record MeasuredScenarioBattle(
        int SeedSalt,
        string SquadId,
        bool Won,
        MeasuredBattle Battle);
    private sealed record ClearRateMeasurement(
        IReadOnlyList<EndlessHeatClearRateAggregate> Fixed,
        IReadOnlyList<EndlessHeatClearRateAggregate> Paired,
        EndlessHeatPairingVerification Pairing);
    private sealed record MeasuredBattle(
        bool Won,
        int BattleSeed,
        IReadOnlyList<string> EntityIds);
}
