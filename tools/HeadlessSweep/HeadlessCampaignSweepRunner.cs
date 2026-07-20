using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Editor.Validation;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class HeadlessCampaignSweepRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefaultOutputRelativePath = "Temp/HeadlessSweep/headless-campaign-sweep.json";
    private const string DefaultStopAfterEncounterId = "site_wolfpine_trail_boss_1";

    internal static int Run(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        try
        {
            var options = Parse(arguments);
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var config = CampaignBalanceSweepConfig.Default;
            config.Validate();
            var cells = SampleCells(config.BuildGrid(), options.Cells);

            var primary = Execute(lookup, config, cells, options.Degree, options.StopAfterEncounterId);
            var verification = new HeadlessCampaignVerification(
                Requested: options.Verify,
                ReproducibleAcrossParallelRuns: false,
                MatchesSingleThread: false,
                RepeatHash: string.Empty,
                SingleThreadHash: string.Empty,
                RepeatElapsedSeconds: 0d,
                SingleThreadElapsedSeconds: 0d);
            if (options.Verify)
            {
                var repeat = Execute(lookup, config, cells, options.Degree, options.StopAfterEncounterId);
                var single = Execute(lookup, config, cells, 1, options.StopAfterEncounterId);
                verification = new HeadlessCampaignVerification(
                    Requested: true,
                    ReproducibleAcrossParallelRuns: string.Equals(
                        primary.Hash,
                        repeat.Hash,
                        StringComparison.Ordinal),
                    MatchesSingleThread: string.Equals(
                        primary.Hash,
                        single.Hash,
                        StringComparison.Ordinal),
                    RepeatHash: repeat.Hash,
                    SingleThreadHash: single.Hash,
                    RepeatElapsedSeconds: repeat.ElapsedSeconds,
                    SingleThreadElapsedSeconds: single.ElapsedSeconds);
                if (!verification.ReproducibleAcrossParallelRuns || !verification.MatchesSingleThread)
                {
                    throw new InvalidOperationException(
                        $"Campaign sweep determinism failed: primary={primary.Hash}, repeat={repeat.Hash}, single={single.Hash}");
                }
            }

            var report = new HeadlessCampaignSweepReport(
                SchemaVersion: "headless-campaign-sweep-v1",
                Cells: cells.Count,
                Arms: config.Arms.Count,
                DegreeOfParallelism: options.Degree,
                LogicalProcessorCount: Environment.ProcessorCount,
                ElapsedSeconds: primary.ElapsedSeconds,
                CanonicalHash: primary.Hash,
                Result: primary.Canonical,
                TargetBoss: primary.TargetBoss,
                TargetBossSurvival: primary.TargetBossSurvival,
                TargetBossAoeSurvival: primary.TargetBossAoeSurvival,
                Verification: verification);
            var outputPath = Resolve(repositoryRoot, options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(
                outputPath,
                JsonConvert.SerializeObject(report, Formatting.Indented, SerializerSettings()));
            Console.WriteLine(
                $"headless-campaign-sweep MATCH cells={cells.Count} arms={config.Arms.Count} "
                + $"degree={options.Degree} elapsed={primary.ElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture)}s "
                + $"hash={primary.Hash} boss={primary.TargetBoss.Naive.WinRate.ToString("0.000000", CultureInfo.InvariantCulture)}"
                + $"->{primary.TargetBoss.Informed.WinRate.ToString("0.000000", CultureInfo.InvariantCulture)} "
                + $"gap={primary.TargetBoss.Gap.ToString("0.000000", CultureInfo.InvariantCulture)} "
                + $"aoe-catch={primary.TargetBossAoeSurvival.Naive.MeanShooterAoeCatches.ToString("0.000000", CultureInfo.InvariantCulture)}"
                + $"->{primary.TargetBossAoeSurvival.Informed.MeanShooterAoeCatches.ToString("0.000000", CultureInfo.InvariantCulture)} "
                + $"shooter-survival={primary.TargetBossAoeSurvival.Naive.ShooterSurvivalRate.ToString("0.000000", CultureInfo.InvariantCulture)}"
                + $"->{primary.TargetBossAoeSurvival.Informed.ShooterSurvivalRate.ToString("0.000000", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"headless-campaign-sweep report={outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"headless-campaign-sweep ERROR: {exception}");
            return 2;
        }
    }

    private static SweepExecution Execute(
        SnapshotSessionContentLookup lookup,
        CampaignBalanceSweepConfig config,
        IReadOnlyList<CampaignBalanceGridCell> cells,
        int degree,
        string stopAfterEncounterId)
    {
        var stopwatch = Stopwatch.StartNew();
        var executions = new HeadlessCampaignCellExecution[cells.Count];
        Parallel.ForEach(
            Enumerable.Range(0, cells.Count),
            new ParallelOptions { MaxDegreeOfParallelism = degree },
            cellIndex =>
            {
                var cell = cells[cellIndex];
                var arms = config.Arms
                    .Select(arm => HeadlessCampaignPlaythrough.Run(
                        lookup,
                        config,
                        arm,
                        cell,
                        stopAfterEncounterId))
                    .ToArray();
                executions[cellIndex] = new HeadlessCampaignCellExecution(
                    cellIndex,
                    cell.CellId,
                    cell.Squad.SquadId,
                    arms);
            });
        stopwatch.Stop();

        var accumulator = new CampaignTwoArmSweepAccumulator(config);
        foreach (var cell in executions)
        {
            foreach (var arm in cell.Arms)
            {
                foreach (var node in arm.Nodes)
                {
                    accumulator.RecordNode(
                        arm.Arm,
                        arm.CellId,
                        arm.SquadId,
                        node.Identity,
                        node.Won,
                        node.AnswerTagPresent,
                        node.FormationHash,
                        node.GearCounterUsed);
                }

                foreach (var site in arm.Sites)
                {
                    accumulator.RecordSite(
                        arm.Arm,
                        arm.SquadId,
                        site.Identity,
                        site.FirstVisitAndClear);
                }

                foreach (var decision in arm.SiteEntryDecisions)
                {
                    accumulator.RecordSiteEntryDecision(
                        arm.Arm,
                        decision.PreviewAvailable,
                        decision.SetupChanged);
                }

                foreach (var decision in arm.PrepDecisions)
                {
                    accumulator.RecordPrepDecision(
                        arm.Arm,
                        decision.Opportunity,
                        decision.SetupChanged,
                        decision.EquipmentAssignmentCount);
                }
            }
        }

        var nodeBands = accumulator.BuildNodeAggregates()
            .Select(ToNodeBand)
            .ToArray();
        var targetBoss = nodeBands.Single(node => string.Equals(
            node.EncounterId,
            stopAfterEncounterId,
            StringComparison.Ordinal));
        var targetBossSurvival = BuildSurvivalBand(
            executions,
            config,
            stopAfterEncounterId);
        var targetBossAoeSurvival = BuildAoeBand(
            executions,
            config,
            stopAfterEncounterId);
        var canonical = new HeadlessCampaignCanonicalResult(
            SchemaVersion: "headless-campaign-canonical-v1",
            StopAfterEncounterId: stopAfterEncounterId,
            CellIds: executions.Select(cell => cell.CellId).ToArray(),
            Arms: config.Arms.Select(arm => $"{arm.ArmId}:{arm.PolicyId}").ToArray(),
            Nodes: nodeBands,
            Cells: executions.Select(cell => new HeadlessCampaignCellResult(
                    cell.CellIndex,
                    cell.CellId,
                    cell.SquadId,
                    cell.Arms.SelectMany(arm => arm.Nodes.Select(node => new HeadlessCampaignCellNodeResult(
                            arm.Arm.ArmId,
                            node.Identity.EncounterId,
                            node.Won,
                            node.FormationHash,
                            node.GearCounterUsed,
                            node.FlankSurvival,
                            node.AntiClusterAoeSurvival)))
                        .ToArray()))
                .ToArray());
        var canonicalJson = JsonConvert.SerializeObject(canonical, Formatting.None, SerializerSettings());
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson))).ToLowerInvariant();
        return new SweepExecution(
            canonical,
            targetBoss,
            targetBossSurvival,
            targetBossAoeSurvival,
            hash,
            stopwatch.Elapsed.TotalSeconds);
    }

    private static HeadlessCampaignAoeBand BuildAoeBand(
        IReadOnlyList<HeadlessCampaignCellExecution> executions,
        CampaignBalanceSweepConfig config,
        string encounterId)
    {
        var squadIds = executions.Select(value => value.SquadId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new HeadlessCampaignAoeBand(
            encounterId,
            BuildAoeArm(executions, config.Arms[0], encounterId),
            BuildAoeArm(executions, config.Arms[1], encounterId),
            squadIds.Select(squadId => new HeadlessCampaignSquadAoeBand(
                    squadId,
                    BuildAoeArm(executions, config.Arms[0], encounterId, squadId),
                    BuildAoeArm(executions, config.Arms[1], encounterId, squadId)))
                .ToArray());
    }

    private static HeadlessCampaignAoeArmCount BuildAoeArm(
        IEnumerable<HeadlessCampaignCellExecution> executions,
        CampaignBalanceArmSpec arm,
        string encounterId,
        string? squadId = null)
    {
        var observations = executions
            .Where(cell => squadId == null || string.Equals(cell.SquadId, squadId, StringComparison.Ordinal))
            .SelectMany(cell => cell.Arms)
            .Where(execution => string.Equals(execution.Arm.ArmId, arm.ArmId, StringComparison.Ordinal))
            .SelectMany(execution => execution.Nodes)
            .Where(node => string.Equals(node.Identity.EncounterId, encounterId, StringComparison.Ordinal))
            .Select(node => node.AntiClusterAoeSurvival)
            .ToArray();
        return new HeadlessCampaignAoeArmCount(
            arm.ArmId,
            arm.PolicyId,
            observations.Length,
            observations.Sum(value => value.BossAoeCastCount),
            observations.Sum(value => value.AoeCatchCount),
            observations.Sum(value => value.ShooterAoeCatchCount),
            observations.Sum(value => value.ShooterCount),
            observations.Sum(value => value.SurvivingShooterCount));
    }

    private static HeadlessCampaignSurvivalBand BuildSurvivalBand(
        IReadOnlyList<HeadlessCampaignCellExecution> executions,
        CampaignBalanceSweepConfig config,
        string encounterId)
    {
        var squadIds = executions.Select(value => value.SquadId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new HeadlessCampaignSurvivalBand(
            encounterId,
            BuildSurvivalArm(executions, config.Arms[0], encounterId),
            BuildSurvivalArm(executions, config.Arms[1], encounterId),
            squadIds.Select(squadId => new HeadlessCampaignSquadSurvivalBand(
                    squadId,
                    BuildSurvivalArm(executions, config.Arms[0], encounterId, squadId),
                    BuildSurvivalArm(executions, config.Arms[1], encounterId, squadId)))
                .ToArray());
    }

    private static HeadlessCampaignSurvivalArmCount BuildSurvivalArm(
        IEnumerable<HeadlessCampaignCellExecution> executions,
        CampaignBalanceArmSpec arm,
        string encounterId,
        string? squadId = null)
    {
        var observations = executions
            .Where(cell => squadId == null || string.Equals(cell.SquadId, squadId, StringComparison.Ordinal))
            .SelectMany(cell => cell.Arms)
            .Where(execution => string.Equals(execution.Arm.ArmId, arm.ArmId, StringComparison.Ordinal))
            .SelectMany(execution => execution.Nodes)
            .Where(node => string.Equals(node.Identity.EncounterId, encounterId, StringComparison.Ordinal))
            .Select(node => node.FlankSurvival)
            .ToArray();
        return new HeadlessCampaignSurvivalArmCount(
            arm.ArmId,
            arm.PolicyId,
            observations.Sum(value => value.TargetedShooterCount),
            observations.Sum(value => value.SurvivingTargetedShooterCount),
            observations.Sum(value => value.BacklineDiveKillCount));
    }

    private static HeadlessCampaignNodeBand ToNodeBand(CampaignTwoArmNodeAggregate aggregate)
        => new(
            aggregate.ChapterId,
            aggregate.ChapterOrder,
            aggregate.SiteId,
            aggregate.SiteOrder,
            aggregate.NodeId,
            aggregate.NodeOrder,
            aggregate.EncounterId,
            aggregate.IsElite,
            aggregate.IsBoss,
            ToArmCount(aggregate.Naive),
            ToArmCount(aggregate.Informed),
            aggregate.ByReferenceSquad.Select(squad => new HeadlessCampaignSquadBand(
                    squad.SquadId,
                    ToArmCount(squad.Naive),
                    ToArmCount(squad.Informed)))
                .ToArray());

    private static HeadlessCampaignArmCount ToArmCount(CampaignArmSampleAggregate aggregate)
        => new(
            aggregate.ArmId,
            aggregate.PolicyId,
            aggregate.SampleCount,
            aggregate.WinCount,
            aggregate.BossWinWithAnswerTagCount);

    private static IReadOnlyList<CampaignBalanceGridCell> SampleCells(
        IReadOnlyList<CampaignBalanceGridCell> grid,
        int sampleCount)
        => Enumerable.Range(0, sampleCount)
            .Select(index => grid[(int)Math.Floor(index * grid.Count / (double)sampleCount)])
            .ToArray();

    private static Options Parse(IReadOnlyList<string> arguments)
    {
        var cells = 24;
        var degree = Math.Max(1, Environment.ProcessorCount);
        var stopAfter = DefaultStopAfterEncounterId;
        var outputPath = DefaultOutputRelativePath;
        var verify = false;
        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--cells" when index + 1 < arguments.Count:
                    cells = ParseInt(arguments[++index], "cells", 1, 480);
                    break;
                case "--degree" when index + 1 < arguments.Count:
                    degree = ParseInt(arguments[++index], "degree", 1, 512);
                    break;
                case "--stop-after" when index + 1 < arguments.Count:
                    stopAfter = arguments[++index];
                    break;
                case "--output" when index + 1 < arguments.Count:
                    outputPath = arguments[++index];
                    break;
                case "--verify":
                    verify = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown campaign-sweep argument: {arguments[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(stopAfter))
        {
            throw new ArgumentException("--stop-after requires a non-empty encounter id.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output requires a non-empty path.");
        }

        return new Options(cells, degree, stopAfter, outputPath, verify);
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
        int Cells,
        int Degree,
        string StopAfterEncounterId,
        string OutputPath,
        bool Verify);

    private sealed record SweepExecution(
        HeadlessCampaignCanonicalResult Canonical,
        HeadlessCampaignNodeBand TargetBoss,
        HeadlessCampaignSurvivalBand TargetBossSurvival,
        HeadlessCampaignAoeBand TargetBossAoeSurvival,
        string Hash,
        double ElapsedSeconds);
}
