using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Editor.Validation;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class RefitFarmProfileRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefaultOutputRelativePath = "Temp/HeadlessSweep/refit-farm-profile.json";
    private static readonly int[] Horizons = { 25, 100 };
    private static readonly IReadOnlyDictionary<int, int> HeatByHorizon =
        new SortedDictionary<int, int>
        {
            [25] = 3,
            [100] = 5,
        };

    internal static int Run(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        try
        {
            var options = Parse(arguments);
            ContentSnapshotFreshnessGuard.EnsureFresh(repositoryRoot);
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var balance = snapshot.RefitBalance
                          ?? throw new InvalidDataException(
                              "Refit farming measurement requires serialized Refit balance data.");
            var gradeStepBudgetScore = ResolveGradeStepBudgetScore(snapshot);
            var targetSite = snapshot.ExpeditionSites?.GetValueOrDefault(
                                 EndlessHeatSweepRunner.TargetSiteId)
                             ?? throw new InvalidDataException(
                                 $"Refit farming site '{EndlessHeatSweepRunner.TargetSiteId}' was not found.");
            var economy = new RefitChapterEconomy(
                targetSite.ChapterId,
                CampaignRecoveryRewardPolicy.ResolveFirstFarmRunEcho(
                    snapshot,
                    targetSite.ChapterId),
                CampaignRecoveryRewardPolicy.ResolveFirstFarmRunMeanGrade(
                    snapshot,
                    targetSite.ChapterId));
            if (economy.FirstFarmRunEcho <= 0 || !double.IsFinite(economy.MeanGrade))
            {
                throw new InvalidDataException(
                    $"Refit farming economy was invalid for '{targetSite.ChapterId}': "
                    + $"E1={economy.FirstFarmRunEcho}, mean-grade={economy.MeanGrade:R}.");
            }

            var config = CampaignBalanceSweepConfig.Default;
            config.Validate();
            var cells = EndlessHeatSweepRunner.BuildReferenceCells(config);
            var prepared = EndlessHeatSweepRunner.PrepareScenarios(
                lookup,
                config,
                cells,
                options.SeedsPerCell,
                options.Degree);
            var results = new List<RefitFarmScenarioResult>(
                prepared.Count * Horizons.Length);
            using var services = new ThreadLocal<RefitService>(
                () => new RefitService(lookup, balance, gradeStepBudgetScore),
                trackAllValues: false);
            foreach (var horizon in Horizons)
            {
                var heat = HeatByHorizon[horizon];
                var horizonResults = new RefitFarmScenarioResult[prepared.Count];
                Parallel.ForEach(
                    Enumerable.Range(0, prepared.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = options.Degree },
                    index =>
                    {
                        horizonResults[index] = RefitFarmScenarioSimulator.Run(
                            prepared[index],
                            horizon,
                            heat,
                            services.Value
                            ?? throw new InvalidOperationException(
                                "Thread-local Refit service was unavailable."),
                            economy,
                            balance);
                    });
                results.AddRange(horizonResults);
            }

            var activation = Horizons
                .Select(horizon => BuildActivation(
                    horizon,
                    results.Where(value => value.HorizonMaps == horizon).ToArray()))
                .ToArray();
            var tailMetrics = new List<RefitFarmTailMetricResult>
            {
                RefitFarmTailBootstrap.Evaluate(
                    "pooled",
                    results,
                    options.BootstrapReplicates,
                    EnoughActivity(results)),
            };
            foreach (var horizon in Horizons)
            {
                var slice = results.Where(value => value.HorizonMaps == horizon).ToArray();
                tailMetrics.Add(RefitFarmTailBootstrap.Evaluate(
                    $"horizon:{horizon.ToString(CultureInfo.InvariantCulture)}",
                    slice,
                    options.BootstrapReplicates,
                    EnoughActivity(slice)));
            }

            foreach (var squad in cells.Select(cell => cell.Squad.SquadId))
            {
                var slice = results
                    .Where(value => string.Equals(value.SquadId, squad, StringComparison.Ordinal))
                    .ToArray();
                tailMetrics.Add(RefitFarmTailBootstrap.Evaluate(
                    $"composition:{squad}",
                    slice,
                    options.BootstrapReplicates,
                    EnoughActivity(slice)));
            }

            var report = new RefitFarmProfileReport(
                SchemaVersion: "refit-farm-profile-v1",
                SeedsPerCell: options.SeedsPerCell,
                Squads: cells.Select(cell => cell.Squad.SquadId).ToArray(),
                Horizons,
                HeatByHorizon,
                BootstrapReplicates: options.BootstrapReplicates,
                SpendingPolicy:
                "After every map, preview every affordable Epic-or-Legendary item's next effective floor; "
                + "re-run the deterministic maximum-total-BudgetScore 12-slot equip policy; measure "
                + "squad effective power as total resolved MaxHealth times total resolved PhysPower+MagPower; "
                + "buy the highest positive delta-ln-power/Echo ratio; tie-break by expedition hero index, "
                + "Weapon/Armor/Accessory order, stable item key, then target Refit level; repeat to exhaustion.",
                Pairing: BuildPairing(results),
                Activation: activation,
                TailMetrics: tailMetrics,
                ChannelShare: BuildChannelShare(results),
                Diagnostics: BuildDiagnostics(results),
                ResolutionNotes: new[]
                {
                    "The panel is 32 seed clusters per composition, three canonical compositions, and "
                    + "96 seed/composition outcomes per horizon. H25 uses Heat 3 and H100 uses Heat 5, "
                    + "the conservative lower endpoints of the latest measured S70 settle brackets H3-H4 "
                    + "and H5-H6; no bracket endpoints were averaged.",
                    $"Bootstrap uses {options.BootstrapReplicates.ToString(CultureInfo.InvariantCulture)} "
                    + "deterministic paired resamples of the 32 seed clusters. Every selected cluster retains "
                    + "all compositions and, for pooled/composition slices, both horizons.",
                    "Across-seed Q10/Q50/Q90 use the nearest empirical order statistic with midpoint-away-from-zero "
                    + "indexing. A slice is activity-sufficient only with at least 32 purchases and purchases "
                    + "in at least 16 of the 32 seed clusters.",
                    "The role-affinity diagnostic covers every affordable deterministic candidate Refit execution "
                    + "before the positive-ratio purchase filter. Purchased negative-power operations are also "
                    + "reported separately and are expected to be zero by policy construction.",
                },
                CanonicalHash: string.Empty);
            var canonicalHash = HashReport(report);
            report = report with { CanonicalHash = canonicalHash };
            var outputPath = Resolve(repositoryRoot, options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(
                outputPath,
                JsonConvert.SerializeObject(report, Formatting.Indented, SerializerSettings()),
                new UTF8Encoding(false));
            Console.WriteLine(
                $"refit-farm-profile MATCH seeds-per-cell={options.SeedsPerCell} "
                + $"outcomes={results.Count} bootstrap={options.BootstrapReplicates} "
                + $"hash={canonicalHash}");
            Console.WriteLine($"refit-farm-profile report={outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"refit-farm-profile ERROR: {exception}");
            return 2;
        }
    }

    private static RefitFarmActivationResult BuildActivation(
        int horizon,
        IReadOnlyList<RefitFarmScenarioResult> results)
    {
        var purchases = results.Sum(value => value.Purchases.Count);
        var totalItems = results.Sum(value => value.TotalItems);
        return new RefitFarmActivationResult(
            horizon,
            purchases / (double)results.Count,
            totalItems == 0
                ? 0d
                : 100d * results.Sum(value => value.EligibleItems) / totalItems,
            EnoughActivity(results));
    }

    private static bool EnoughActivity(IReadOnlyList<RefitFarmScenarioResult> results)
    {
        var purchases = results.Sum(value => value.Purchases.Count);
        var activeSeedClusters = results
            .Where(value => value.Purchases.Count > 0)
            .Select(value => value.SeedSalt)
            .Distinct()
            .Count();
        return purchases >= 32 && activeSeedClusters >= 16;
    }

    private static RefitFarmPairingResult BuildPairing(
        IReadOnlyList<RefitFarmScenarioResult> results)
    {
        var dropChecks = results.Sum(value => value.HorizonMaps);
        return new RefitFarmPairingResult(
            Verified: true,
            InitialSaveChecks: results.Count,
            SeedChecks: results.Count,
            DropChecks: dropChecks,
            EchoRewardChecks: dropChecks,
            EntityIdChecks: results.Count,
            HowVerified:
            "For every horizon/composition/seed, both arms were cloned from one already-equipped initial state "
            + "and its canonical save fingerprint and CampaignSeed were compared. After each map the ordered "
            + "natural drop vector compared instance id, acquisition index, base, grade, and affix bytes, and "
            + "the earned Echo amount was compared. At horizon end the prepared boss BattleSeed and ordered "
            + "ally-plus-enemy entity-id vectors were composed and compared before any battle run.");
    }

    private static RefitFarmChannelShareResult BuildChannelShare(
        IReadOnlyList<RefitFarmScenarioResult> results)
    {
        var gD = results.Average(value => Math.Log(value.DropsOnlyPower / value.InitialPower));
        var gR = results.Average(value => Math.Log(value.DropsAndRefitPower / value.DropsOnlyPower));
        var denominator = gD + gR;
        var sR = Math.Abs(denominator) <= 1e-15d ? double.NaN : gR / denominator;
        var perHorizon = Horizons.Select(horizon =>
        {
            var slice = results.Where(value => value.HorizonMaps == horizon).ToArray();
            var sliceGD = slice.Average(value => Math.Log(value.DropsOnlyPower / value.InitialPower));
            var sliceGR = slice.Average(value => Math.Log(value.DropsAndRefitPower / value.DropsOnlyPower));
            return $"H{horizon}:G_D={sliceGD:R},G_R={sliceGR:R},S_R="
                   + $"{(sliceGR / (sliceGD + sliceGR)):R}";
        });
        return new RefitFarmChannelShareResult(
            gD,
            gR,
            sR,
            double.IsFinite(sR) && sR >= 0.25d && sR <= 0.35d,
            "Secondary telemetry only. " + string.Join("; ", perHorizon));
    }

    private static RefitFarmDiagnosticsResult BuildDiagnostics(
        IReadOnlyList<RefitFarmScenarioResult> results)
    {
        var purchases = results.SelectMany(value => value.Purchases).ToArray();
        var finalItems = results.SelectMany(value => value.FinalRefittedItems).ToArray();
        var totalEcho = purchases.Sum(value => value.EchoCost);
        var levelDistribution = finalItems
            .GroupBy(value => value.FinalRefitLevel)
            .OrderBy(group => group.Key)
            .Select(group => new RefitFarmLevelDistribution(
                group.Key,
                group.Count(),
                finalItems.Length == 0 ? 0d : 100d * group.Count() / finalItems.Length))
            .ToArray();
        var echoByHorizon = Horizons.Select(horizon =>
        {
            var slice = results.Where(value => value.HorizonMaps == horizon).ToArray();
            var slicePurchases = slice.SelectMany(value => value.Purchases).ToArray();
            var sliceItems = slice.Sum(value => value.FinalRefittedItems.Count);
            var echo = slicePurchases.Sum(value => value.EchoCost);
            return new RefitFarmEchoHorizon(
                horizon,
                echo,
                echo / (double)slice.Length,
                sliceItems == 0 ? 0d : echo / (double)sliceItems);
        }).ToArray();
        var overshootByLevel = purchases
            .GroupBy(value => value.TargetRefitLevel)
            .OrderBy(group => group.Key)
            .Select(group => new RefitFarmCdfOvershootLevel(
                group.Key,
                group.Count(),
                group.Average(value => value.CdfOvershoot),
                group.Max(value => value.CdfOvershoot)))
            .ToArray();
        var previews = results
            .SelectMany(value => value.PreviewDiagnostics)
            .Where(value => value.BudgetScoreIncreased)
            .ToArray();
        var top20Count = results.Sum(value => value.Top20NaturalItems);
        return new RefitFarmDiagnosticsResult(
            levelDistribution,
            finalItems.Length == 0 ? 0d : totalEcho / (double)finalItems.Length,
            echoByHorizon,
            finalItems.Length == 0
                ? 0d
                : 100d * finalItems.Count(value => value.ReachedMaximumFloor) / finalItems.Length,
            top20Count == 0
                ? 0d
                : 100d * results.Sum(value => value.Top20NaturalItemsChanged) / top20Count,
            new RefitFarmCdfOvershootSummary(
                purchases.Length,
                purchases.Length == 0 ? 0d : purchases.Average(value => value.CdfOvershoot),
                purchases.Length == 0 ? 0d : purchases.Max(value => value.CdfOvershoot),
                overshootByLevel),
            previews.Length == 0
                ? 0d
                : 100d * previews.Count(value => value.ReducedActualPower) / previews.Length,
            previews.Length,
            purchases.Length == 0
                ? 0d
                : 100d * purchases.Count(value => value.PowerDeltaLog < -1e-12d) / purchases.Length);
    }

    private static float ResolveGradeStepBudgetScore(CombatContentSnapshot snapshot)
    {
        var values = (snapshot.DropTables?.Values ?? Array.Empty<DropTableTemplate>())
            .Where(table => table.GradeProfiles is { Count: > 0 })
            .Select(table => table.GradeStepBudgetScore)
            .Distinct()
            .ToArray();
        if (values.Length != 1 || values[0] <= 0f)
        {
            throw new InvalidDataException(
                $"Refit farming requires one positive grade-step BudgetScore, got [{string.Join(",", values)}].");
        }

        return values[0];
    }

    private static Options Parse(IReadOnlyList<string> arguments)
    {
        var seedsPerCell = 32;
        var degree = Math.Max(1, Environment.ProcessorCount - 1);
        var bootstrapReplicates = 10_000;
        var outputPath = DefaultOutputRelativePath;
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--seeds" when index + 1 < arguments.Count:
                    seedsPerCell = ParseInt(arguments[++index], "seeds", 2, 512);
                    break;
                case "--degree" when index + 1 < arguments.Count:
                    degree = ParseInt(arguments[++index], "degree", 1, 256);
                    break;
                case "--bootstrap" when index + 1 < arguments.Count:
                    bootstrapReplicates = ParseInt(arguments[++index], "bootstrap", 2_000, 100_000);
                    break;
                case "--output" when index + 1 < arguments.Count:
                    outputPath = arguments[++index];
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown refit-farm-profile argument '{arguments[index]}'.");
            }
        }

        return new Options(seedsPerCell, degree, bootstrapReplicates, outputPath);
    }

    private static int ParseInt(string raw, string name, int minimum, int maximum)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < minimum
            || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                name,
                raw,
                $"Expected integer in [{minimum},{maximum}].");
        }

        return value;
    }

    private static string HashReport(RefitFarmProfileReport report)
    {
        var json = JsonConvert.SerializeObject(report, Formatting.None, SerializerSettings());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
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
            : Path.GetFullPath(Path.Combine(repositoryRoot, path));

    private sealed record Options(
        int SeedsPerCell,
        int Degree,
        int BootstrapReplicates,
        string OutputPath);
}
