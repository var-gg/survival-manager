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

public static class CampaignSignedDeficitMeasurementCli
{
    private const string DefaultOutputPath = "Logs/campaign-signed-deficit-measurement.json";

    public static void RunFromCli()
    {
        try
        {
            var seedCount = ReadInt("SM_CAMPAIGN_DEFICIT_SEEDS", 256, 16, 4096);
            var seedBase = ReadInt("SM_CAMPAIGN_DEFICIT_SEED_BASE", 1701, int.MinValue, int.MaxValue);
            var searchMinimum = ReadDouble("SM_CAMPAIGN_DEFICIT_MIN_X", -4d, -12d, -0.001d);
            var searchMaximum = ReadDouble("SM_CAMPAIGN_DEFICIT_MAX_X", 4d, 0.001d, 12d);
            var tolerance = ReadDouble("SM_CAMPAIGN_DEFICIT_TOLERANCE", 0.001d, 0.0001d, 0.05d);
            if (searchMinimum >= searchMaximum)
            {
                throw new InvalidOperationException("Signed-deficit search minimum must be below maximum.");
            }

            var outputPath = Environment.GetEnvironmentVariable("SM_CAMPAIGN_DEFICIT_OUTPUT");
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = DefaultOutputPath;
            }

            RuntimeInstrumentation.SetPolicy(RuntimeInstrumentationPolicy.Off);
            var report = CampaignSignedDeficitMeasurementRunner.Run(
                seedCount,
                seedBase,
                searchMinimum,
                searchMaximum,
                tolerance);
            var absolutePath = Path.IsPathRooted(outputPath)
                ? Path.GetFullPath(outputPath)
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            File.WriteAllText(
                absolutePath,
                JsonConvert.SerializeObject(report, Formatting.Indented, SerializerSettings()),
                new UTF8Encoding(false));
            Debug.Log(
                $"[CampaignSignedDeficit] seeds={seedCount} delta={report.DeltaMean:F6} "
                + $"sigma={report.SigmaPopulation:F6} q0_informed={report.Q0Informed:F6} "
                + $"q0_naive={report.Q0Naive:F6} hash={report.CanonicalHash} report={absolutePath}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[CampaignSignedDeficit] failed: {exception}");
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

internal static class CampaignSignedDeficitMeasurementRunner
{
    private const string SchemaVersion = "campaign-signed-deficit-v1";

    internal static CampaignSignedDeficitMeasurementReport Run(
        int seedCount,
        int seedBase,
        double searchMinimum,
        double searchMaximum,
        double tolerance)
    {
        SM.Editor.SeedData.SampleSeedGenerator.RequireCanonicalSampleContentReady(
            nameof(CampaignSignedDeficitMeasurementCli));
        var lookup = new RuntimeCombatContentLookup(allowEditorRecoveryFallback: true);
        if (!lookup.TryGetCombatSnapshot(out _, out var contentError))
        {
            throw new InvalidOperationException($"campaign signed-deficit content unavailable: {contentError}");
        }

        var informedPolicyId = HeadlessPolicyFactory.PreviewGroundedConceptId;
        var naivePolicyId = HeadlessPolicyFactory.GreedyId;
        var seeds = Enumerable.Range(0, seedCount)
            .Select(index => H100SessionDriver.DeriveSeed("campaign-signed-deficit", seedBase + index))
            .ToArray();
        var informed = new List<CampaignSignedDeficitSeedObservation>(seedCount);
        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < seeds.Length; index++)
        {
            informed.Add(MeasureSeed(
                lookup,
                index,
                seeds[index],
                informedPolicyId,
                searchMinimum,
                searchMaximum,
                tolerance));
            if ((index + 1) % 8 == 0 || index + 1 == seeds.Length)
            {
                Debug.Log(
                    $"[CampaignSignedDeficit] informed={index + 1}/{seeds.Length} "
                    + $"elapsed={stopwatch.Elapsed.TotalMinutes:F1}m");
            }
        }

        var naive = new List<CampaignZeroPowerSeedObservation>(seedCount);
        for (var index = 0; index < seeds.Length; index++)
        {
            var result = CampaignSignedDeficitSimulation.Run(
                lookup,
                index,
                seeds[index],
                naivePolicyId,
                0d);
            naive.Add(new CampaignZeroPowerSeedObservation(
                index,
                seeds[index],
                result.Completed,
                result.TerminalNodeId,
                result.BattleCount,
                result.SiteCount));
            if ((index + 1) % 16 == 0 || index + 1 == seeds.Length)
            {
                Debug.Log(
                    $"[CampaignSignedDeficit] naive={index + 1}/{seeds.Length} "
                    + $"elapsed={stopwatch.Elapsed.TotalMinutes:F1}m");
            }
        }

        var exactDeficits = informed
            .Where(value => value.SignedDeficit.HasValue
                            && !value.LeftCensored
                            && !value.RightCensored
                            && !value.MonotonicityViolated)
            .Select(value => value.SignedDeficit!.Value)
            .OrderBy(value => value)
            .ToArray();
        if (exactDeficits.Length != seedCount)
        {
            throw new InvalidOperationException(
                $"Signed-deficit measurement did not produce {seedCount} exact monotone observations "
                + $"(exact={exactDeficits.Length}).");
        }

        var delta = exactDeficits.Average();
        var sigma = Math.Sqrt(exactDeficits.Sum(value => Math.Pow(value - delta, 2d)) / exactDeficits.Length);
        var q0Informed = informed.Count(value => value.ClearedAtZero) / (double)seedCount;
        var q0Naive = naive.Count(value => value.Cleared) / (double)seedCount;
        double? ratio = q0Naive > 0d ? q0Informed / q0Naive : null;
        var quantiles = BuildQuantiles(exactDeficits);
        var payload = new CampaignSignedDeficitMeasurementReport(
            SchemaVersion,
            "No rewarded revisits; production first-clear loot/reward and normal Town recruit/passive/refit enabled. "
            + "For each informed-policy campaign seed, binary-search the minimum x whose production session clears, "
            + "after multiplying ally MaxHealth, PhysPower, and MagPower by exp(x/2).",
            seedBase,
            seedCount,
            searchMinimum,
            searchMaximum,
            tolerance,
            informedPolicyId,
            naivePolicyId,
            delta,
            sigma,
            q0Informed,
            q0Naive,
            ratio,
            quantiles,
            informed,
            naive,
            false,
            "Production CampaignEncounterSeed intentionally drives both combat RNG and first-clear loot selection; "
            + "there is no independently validated drop-only/tactics-only seed lane, so variance is not decomposed.",
            informed.Count(value => value.MonotonicityViolated),
            informed.Count(value => value.LeftCensored),
            informed.Count(value => value.RightCensored),
            string.Empty);
        var canonicalJson = JsonConvert.SerializeObject(
            payload,
            Formatting.None,
            new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                NullValueHandling = NullValueHandling.Include,
            });
        return payload with { CanonicalHash = Sha256Hex(canonicalJson) };
    }

    private static CampaignSignedDeficitSeedObservation MeasureSeed(
        RuntimeCombatContentLookup lookup,
        int campaignIndex,
        int campaignSeed,
        string policyId,
        double searchMinimum,
        double searchMaximum,
        double tolerance)
    {
        var evaluations = new SortedDictionary<double, CampaignCompletionObservation>();
        CampaignCompletionObservation Evaluate(double logPower)
        {
            if (!evaluations.TryGetValue(logPower, out var observation))
            {
                observation = CampaignSignedDeficitSimulation.Run(
                    lookup,
                    campaignIndex,
                    campaignSeed,
                    policyId,
                    logPower);
                evaluations.Add(logPower, observation);
            }

            return observation;
        }

        var zero = Evaluate(0d);
        var low = 0d;
        var high = 0d;
        var leftCensored = false;
        var rightCensored = false;
        if (zero.Completed)
        {
            high = 0d;
            low = -0.125d;
            while (low > searchMinimum && Evaluate(low).Completed)
            {
                low *= 2d;
            }

            low = Math.Max(low, searchMinimum);
            if (Evaluate(low).Completed)
            {
                leftCensored = true;
            }
        }
        else
        {
            low = 0d;
            high = 0.125d;
            while (high < searchMaximum && !Evaluate(high).Completed)
            {
                high *= 2d;
            }

            high = Math.Min(high, searchMaximum);
            if (!Evaluate(high).Completed)
            {
                rightCensored = true;
            }
        }

        double? deficit = null;
        if (!leftCensored && !rightCensored)
        {
            while (high - low > tolerance)
            {
                var midpoint = low + ((high - low) / 2d);
                if (Evaluate(midpoint).Completed)
                {
                    high = midpoint;
                }
                else
                {
                    low = midpoint;
                }
            }

            deficit = high;
        }

        var monotonicityViolated = HasMonotonicityViolation(evaluations);
        return new CampaignSignedDeficitSeedObservation(
            campaignIndex,
            campaignSeed,
            zero.Completed,
            deficit,
            leftCensored,
            rightCensored,
            monotonicityViolated,
            evaluations.Count,
            zero.TerminalNodeId);
    }

    private static bool HasMonotonicityViolation(
        IReadOnlyDictionary<double, CampaignCompletionObservation> evaluations)
    {
        var seenClear = false;
        foreach (var pair in evaluations.OrderBy(value => value.Key))
        {
            if (pair.Value.Completed)
            {
                seenClear = true;
            }
            else if (seenClear)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<CampaignDeficitQuantile> BuildQuantiles(
        IReadOnlyList<double> sortedDeficits)
    {
        var probabilities = new[] { 0d, 0.01d, 0.05d, 0.10d, 0.25d, 0.50d, 0.75d, 0.90d, 0.95d, 0.99d, 1d };
        return probabilities.Select(probability =>
        {
            var rank = probability <= 0d
                ? 0
                : Math.Min(
                    sortedDeficits.Count - 1,
                    Math.Max(0, (int)Math.Ceiling(probability * sortedDeficits.Count) - 1));
            return new CampaignDeficitQuantile(
                probability,
                sortedDeficits[rank],
                (rank + 1d) / sortedDeficits.Count);
        }).ToArray();
    }

    private static string Sha256Hex(string value)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value))
            .Select(item => item.ToString("x2", CultureInfo.InvariantCulture)));
    }
}
