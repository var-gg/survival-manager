using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.HeadlessPolicies;
using SM.Unity;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SM.Editor.Validation;

public static class CampaignWallDeficitMeasurementCli
{
    private const string DefaultOutputPath = "Logs/campaign-wall-deficit-measurement.json";

    public static void RunFromCli()
    {
        try
        {
            var seedCount = ReadInt("SM_CAMPAIGN_WALL_SEEDS", 256, 1, 4096);
            var seedBase = ReadInt("SM_CAMPAIGN_WALL_SEED_BASE", 1701, int.MinValue, int.MaxValue);
            var searchMaximum = ReadDouble("SM_CAMPAIGN_WALL_MAX_X", 4d, 0.01d, 12d);
            var tolerance = ReadDouble("SM_CAMPAIGN_WALL_TOLERANCE", 0.001d, 0.0001d, 0.05d);
            var adaptationRetryCap = ReadInt("SM_CAMPAIGN_WALL_ADAPTATION_CAP", 4, 0, 10);
            if (tolerance >= searchMaximum)
            {
                throw new InvalidOperationException(
                    "Per-wall search tolerance must be below its maximum.");
            }

            var outputPath = Environment.GetEnvironmentVariable("SM_CAMPAIGN_WALL_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = DefaultOutputPath;
            }

            RuntimeInstrumentation.SetPolicy(RuntimeInstrumentationPolicy.Off);
            var report = CampaignWallDeficitMeasurementRunner.Run(
                seedCount,
                seedBase,
                searchMaximum,
                tolerance,
                adaptationRetryCap);
            var absolutePath = Path.IsPathRooted(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(
                absolutePath,
                JsonConvert.SerializeObject(report, Formatting.Indented, SerializerSettings()),
                new UTF8Encoding(false));
            Debug.Log(
                $"[CampaignWallDeficit] seeds={seedCount} walls={report.WallsObserved} "
                + $"mean={report.MeanWallDeficit:F6} sigma={report.SigmaWallPopulation:F6} "
                + $"hash={report.CanonicalHash} report={absolutePath}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CampaignWallDeficit] failed: {exception}");
            EditorApplication.Exit(1);
        }
    }

    private static int ReadInt(string name, int fallback, int minimum, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be in [{minimum}, {maximum}], got '{raw}'.");
        }

        return value;
    }

    private static double ReadDouble(string name, double fallback, double minimum, double maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException($"{name} must be in [{minimum}, {maximum}], got '{raw}'.");
        }

        return value;
    }

    private static JsonSerializerSettings SerializerSettings()
        => new()
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
        };
}

internal static class CampaignWallDeficitMeasurementRunner
{
    private const string SchemaVersion = "campaign-wall-deficit-v1";

    internal static CampaignWallDeficitMeasurementReport Run(
        int seedCount,
        int seedBase,
        double searchMaximum,
        double tolerance,
        int adaptationRetryCap)
    {
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(
            nameof(CampaignWallDeficitMeasurementCli));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out _, out var contentError))
        {
            throw new InvalidOperationException(
                $"campaign wall-deficit content unavailable: {contentError}");
        }

        var policyId = HeadlessPolicyFactory.PreviewGroundedConceptId;
        var seeds = Enumerable.Range(0, seedCount)
            .Select(index => H100SessionDriver.DeriveSeed(
                "campaign-signed-deficit",
                seedBase + index))
            .ToArray();
        var campaigns = new List<CampaignWallDeficitCampaignObservation>(seedCount);
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < seeds.Length; index++)
        {
            campaigns.Add(CampaignWallDeficitSimulation.Run(
                lookup,
                index,
                seeds[index],
                policyId,
                searchMaximum,
                tolerance,
                adaptationRetryCap));
            if ((index + 1) % 8 == 0 || index + 1 == seeds.Length)
            {
                Debug.Log(
                    $"[CampaignWallDeficit] campaigns={index + 1}/{seeds.Length} "
                    + $"walls={campaigns.Sum(value => value.WallsObserved)} "
                    + $"elapsed={stopwatch.Elapsed.TotalMinutes:F1}m");
            }
        }

        var walls = campaigns.SelectMany(value => value.Walls).ToArray();
        if (walls.Length == 0)
        {
            throw new InvalidOperationException(
                "Per-wall measurement observed no actual cap-exhausted walls.");
        }

        var deficits = walls
            .Select(value => value.AdditionalLogDeficit)
            .OrderBy(value => value)
            .ToArray();
        var wallCounts = campaigns
            .Select(value => value.WallsObserved)
            .OrderBy(value => value)
            .ToArray();
        var progress = walls
            .Select(value => value.NodesAdvancedAfterUnblock)
            .OrderBy(value => value)
            .ToArray();
        var mean = deficits.Average();
        var sigma = Math.Sqrt(
            deficits.Sum(value => Math.Pow(value - mean, 2d))
            / deficits.Length);
        var payload = new CampaignWallDeficitMeasurementReport(
            SchemaVersion,
            "Run the informed production policy with fixed CampaignEncounterSeed values under the corrected "
            + $"no-farm scope: no rewarded cleared-site revisits, {adaptationRetryCap} post-loss adaptation "
            + "cycles per uncleared site through production deployment/prep/recruit/passive/refit seams, and "
            + "discarded defeat rewards. A wall is only recorded when the final permitted attempt loses. "
            + "At that exact battle input and real squad state, binary-search the minimum additional x that "
            + "wins after applying exp(x/2) to ally MaxHealth, PhysPower, and MagPower. Carry each measured "
            + "correction into later battles; nodes advanced ends at the next wall or campaign completion.",
            seedBase,
            seedCount,
            searchMaximum,
            tolerance,
            adaptationRetryCap,
            policyId,
            walls.Length,
            mean,
            sigma,
            BuildDeficitQuantiles(deficits),
            BuildWallCountQuantiles(wallCounts),
            wallCounts.Max(),
            BuildProgressQuantiles(progress),
            campaigns,
            0,
            0,
            string.Empty);
        var canonicalJson = JsonConvert.SerializeObject(
            payload,
            Formatting.None,
            SerializerSettings());
        return payload with { CanonicalHash = Sha256Hex(canonicalJson) };
    }

    private static IReadOnlyList<CampaignWallDeficitQuantile> BuildDeficitQuantiles(
        IReadOnlyList<double> sortedValues)
        => QuantileProbabilities(includeBudgetPoints: true)
            .Select(probability =>
            {
                var rank = NearestRank(sortedValues.Count, probability);
                return new CampaignWallDeficitQuantile(
                    probability,
                    sortedValues[rank],
                    (rank + 1d) / sortedValues.Count);
            })
            .ToArray();

    private static IReadOnlyList<CampaignWallCountQuantile> BuildWallCountQuantiles(
        IReadOnlyList<int> sortedValues)
        => QuantileProbabilities(includeBudgetPoints: false)
            .Select(probability =>
            {
                var rank = NearestRank(sortedValues.Count, probability);
                return new CampaignWallCountQuantile(
                    probability,
                    sortedValues[rank],
                    (rank + 1d) / sortedValues.Count);
            })
            .ToArray();

    private static IReadOnlyList<CampaignWallProgressQuantile> BuildProgressQuantiles(
        IReadOnlyList<int> sortedValues)
        => QuantileProbabilities(includeBudgetPoints: false)
            .Select(probability =>
            {
                var rank = NearestRank(sortedValues.Count, probability);
                return new CampaignWallProgressQuantile(
                    probability,
                    sortedValues[rank],
                    (rank + 1d) / sortedValues.Count);
            })
            .ToArray();

    private static IReadOnlyList<double> QuantileProbabilities(bool includeBudgetPoints)
        => includeBudgetPoints
            ? new[] { 0.10d, 0.25d, 0.50d, 0.70d, 0.75d, 0.80d, 0.90d, 0.95d }
            : new[] { 0.10d, 0.25d, 0.50d, 0.75d, 0.90d, 0.95d };

    private static int NearestRank(int count, double probability)
        => Math.Min(
            count - 1,
            Math.Max(0, (int)Math.Ceiling(probability * count) - 1));

    private static JsonSerializerSettings SerializerSettings()
        => new()
        {
            Culture = CultureInfo.InvariantCulture,
            NullValueHandling = NullValueHandling.Include,
        };

    private static string Sha256Hex(string value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
