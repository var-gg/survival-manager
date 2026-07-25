using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using SM.Core.Content;
using SM.Meta.Model;
using SM.Meta.Serialization;
using SM.Meta.Services;

internal static class AffixMagnitudeAuditRunner
{
    private const string SnapshotRelativePath = "Assets/Resources/_Game/Content/content-snapshot.json";
    private const string DefaultOutputRelativePath = "Temp/HeadlessSweep/affix-magnitude-audit.json";

    internal static int Run(string repositoryRoot, IReadOnlyList<string> arguments)
    {
        try
        {
            var options = Parse(arguments);
            ContentSnapshotFreshnessGuard.EnsureFresh(repositoryRoot);
            var snapshotPath = Resolve(repositoryRoot, SnapshotRelativePath);
            var snapshot = ContentSnapshotJsonSerializer.Deserialize(File.ReadAllText(snapshotPath));
            var lookup = new SnapshotSessionContentLookup(snapshot);
            var gradeStepBudget = snapshot.DropTables?.Values
                                      .Select(table => table.GradeStepBudgetScore)
                                      .FirstOrDefault(value => value > 0f)
                                  ?? 12.3f;

            var affixDrift = BuildAffixDrift(snapshot, options.AffixSamples);
            var profiles = BuildProfiles(lookup, gradeStepBudget, options.ProfileSamples);
            var stableHash = BuildMagnitudeHash(snapshot, options.HashSeeds);
            var report = new
            {
                schema_version = "affix-magnitude-audit-v1",
                distribution = "uniform over recentered authored range",
                profile_samples = options.ProfileSamples,
                affix_samples = options.AffixSamples,
                hash_seeds = options.HashSeeds,
                rolled_magnitude_hash = stableHash,
                mean_preservation = new
                {
                    per_affix = affixDrift,
                    aggregate_expected_drift_pct = AggregateExpectedDriftPct(affixDrift),
                    holds = affixDrift.All(value => Math.Abs(value.ExpectedDriftPct) <= 1e-5d),
                },
                variance = new
                {
                    q70_equals_q80_count_before = profiles.Count(value => value.baseline_q70 == value.baseline_q80),
                    q70_equals_q80_count_after = profiles.Count(value => value.q70 == value.q80),
                    per_grade = profiles
                        .GroupBy(value => value.grade, StringComparer.Ordinal)
                        .OrderBy(group => GradeOrder(group.Key))
                        .Select(group => new
                        {
                            grade = group.Key,
                            profiles = group.Count(),
                            q70_equals_q80_count_before = group.Count(value => value.baseline_q70 == value.baseline_q80),
                            q70_equals_q80_count_after = group.Count(value => value.q70 == value.q80),
                            baseline_mean_q10 = group.Average(value => value.baseline_q10),
                            baseline_mean_q50 = group.Average(value => value.baseline_q50),
                            baseline_mean_q70 = group.Average(value => value.baseline_q70),
                            baseline_mean_q80 = group.Average(value => value.baseline_q80),
                            baseline_mean_q90 = group.Average(value => value.baseline_q90),
                            baseline_mean_q90_minus_q10 = group.Average(value => value.baseline_q90 - value.baseline_q10),
                            mean_q10 = group.Average(value => value.q10),
                            mean_q50 = group.Average(value => value.q50),
                            mean_q70 = group.Average(value => value.q70),
                            mean_q80 = group.Average(value => value.q80),
                            mean_q90 = group.Average(value => value.q90),
                            mean_q90_minus_q10 = group.Average(value => value.q90 - value.q10),
                        }),
                    profiles,
                },
            };

            var outputPath = Resolve(repositoryRoot, options.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            var json = JsonConvert.SerializeObject(report, Formatting.Indented);
            File.WriteAllText(outputPath, json + Environment.NewLine, new UTF8Encoding(false));
            Console.WriteLine(
                $"affix-magnitude-audit profiles={profiles.Count} "
                + $"q70_eq_q80_after={profiles.Count(value => value.q70 == value.q80)} "
                + $"hash={stableHash} output={outputPath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"affix-magnitude-audit ERROR: {exception}");
            return 2;
        }
    }

    private static IReadOnlyList<AffixDrift> BuildAffixDrift(
        CombatContentSnapshot snapshot,
        int samples)
    {
        if (snapshot.AffixCatalog == null)
        {
            throw new InvalidDataException("Affix catalog is absent.");
        }

        var results = new List<AffixDrift>();
        foreach (var template in snapshot.AffixCatalog.Values.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            if (!snapshot.AffixPackages.TryGetValue(template.Id, out var package)
                || package.Modifiers.Count == 0)
            {
                throw new InvalidDataException($"Affix '{template.Id}' has no numeric package.");
            }

            var baseline = package.Modifiers[0].Value;
            var expected = AffixMagnitudeRoller.ExpectedMagnitude(template.ValueMin, template.ValueMax);
            var sampleMean = Enumerable.Range(0, samples)
                .Average(seed => AffixMagnitudeRoller.Roll(
                    seed,
                    template.Id,
                    0,
                    template.ValueMin,
                    template.ValueMax));
            var expectedDriftPct = PercentDrift(expected, baseline);
            var sampleDriftPct = PercentDrift(sampleMean, baseline);
            results.Add(new AffixDrift(
                template.Id,
                baseline,
                template.ValueMin,
                template.ValueMax,
                expected,
                expectedDriftPct,
                sampleMean,
                sampleDriftPct));
        }

        return results;
    }

    private static List<ProfileQuantiles> BuildProfiles(
        SnapshotSessionContentLookup lookup,
        float gradeStepBudget,
        int samples)
    {
        var results = new List<ProfileQuantiles>();
        foreach (var itemBaseId in lookup.GetCanonicalItemIds().OrderBy(value => value, StringComparer.Ordinal))
        {
            foreach (var grade in Enum.GetValues(typeof(ItemRarityTierValue))
                         .Cast<ItemRarityTierValue>()
                         .OrderBy(value => (int)value))
            {
                var baselinePower = new double[samples];
                var power = new double[samples];
                for (var seed = 0; seed < samples; seed++)
                {
                    var affixIds = GeneratedItemAffixSelector.Select(
                        lookup,
                        itemBaseId,
                        seed,
                        grade,
                        gradeStepBudget);
                    var score = 0d;
                    var baselineScore = 0d;
                    for (var affixIndex = 0; affixIndex < affixIds.Count; affixIndex++)
                    {
                        var affixId = affixIds[affixIndex];
                        var template = lookup.Snapshot.AffixCatalog![affixId];
                        var package = lookup.Snapshot.AffixPackages[affixId];
                        var baseline = package.Modifiers[0].Value;
                        var rolled = AffixMagnitudeRoller.Roll(
                            seed,
                            affixId,
                            affixIndex,
                            template.ValueMin,
                            template.ValueMax);
                        var scale = baseline == 0f ? 1d : rolled / baseline;
                        baselineScore += template.BudgetScore;
                        score += template.BudgetScore * scale;
                    }

                    baselinePower[seed] = baselineScore;
                    power[seed] = score;
                }

                Array.Sort(baselinePower);
                Array.Sort(power);
                results.Add(new ProfileQuantiles(
                    $"{itemBaseId}|{grade}",
                    itemBaseId,
                    grade.ToString(),
                    Quantile(baselinePower, 0.10d),
                    Quantile(baselinePower, 0.50d),
                    Quantile(baselinePower, 0.70d),
                    Quantile(baselinePower, 0.80d),
                    Quantile(baselinePower, 0.90d),
                    Quantile(power, 0.10d),
                    Quantile(power, 0.50d),
                    Quantile(power, 0.70d),
                    Quantile(power, 0.80d),
                    Quantile(power, 0.90d)));
            }
        }

        return results;
    }

    private static string BuildMagnitudeHash(CombatContentSnapshot snapshot, int seeds)
    {
        if (snapshot.AffixCatalog == null)
        {
            throw new InvalidDataException("Affix catalog is absent.");
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var template in snapshot.AffixCatalog.Values.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            for (var seed = 0; seed < seeds; seed++)
            {
                var magnitude = AffixMagnitudeRoller.Roll(
                    seed,
                    template.Id,
                    0,
                    template.ValueMin,
                    template.ValueMax);
                var line =
                    $"{template.Id}|{seed.ToString(CultureInfo.InvariantCulture)}|"
                    + $"{unchecked((uint)BitConverter.SingleToInt32Bits(magnitude)).ToString("X8", CultureInfo.InvariantCulture)}\n";
                hash.AppendData(Encoding.UTF8.GetBytes(line));
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static double AggregateExpectedDriftPct(IReadOnlyList<AffixDrift> values)
    {
        var baseline = values.Sum(value => (double)value.LegacyAppliedValue);
        var expected = values.Sum(value => value.ExpectedMean);
        return PercentDrift(expected, baseline);
    }

    private static double PercentDrift(double value, double baseline)
        => baseline == 0d ? (value == 0d ? 0d : double.PositiveInfinity) : ((value / baseline) - 1d) * 100d;

    private static double Quantile(IReadOnlyList<double> sorted, double probability)
    {
        var index = Math.Max(0, (int)Math.Ceiling(probability * sorted.Count) - 1);
        return sorted[Math.Min(index, sorted.Count - 1)];
    }

    private static int GradeOrder(string grade)
        => Enum.TryParse<ItemRarityTierValue>(grade, out var parsed) ? (int)parsed : int.MaxValue;

    private static Options Parse(IReadOnlyList<string> arguments)
    {
        var profileSamples = 4096;
        var affixSamples = 100_000;
        var hashSeeds = 4096;
        var output = DefaultOutputRelativePath;
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--profile-samples", StringComparison.Ordinal) && index + 1 < arguments.Count)
            {
                profileSamples = ParsePositive(arguments[++index], argument);
            }
            else if (string.Equals(argument, "--affix-samples", StringComparison.Ordinal) && index + 1 < arguments.Count)
            {
                affixSamples = ParsePositive(arguments[++index], argument);
            }
            else if (string.Equals(argument, "--hash-seeds", StringComparison.Ordinal) && index + 1 < arguments.Count)
            {
                hashSeeds = ParsePositive(arguments[++index], argument);
            }
            else if (string.Equals(argument, "--output", StringComparison.Ordinal) && index + 1 < arguments.Count)
            {
                output = arguments[++index];
            }
            else
            {
                throw new ArgumentException($"Unknown affix-magnitude-audit argument: {argument}");
            }
        }

        return new Options(profileSamples, affixSamples, hashSeeds, output);
    }

    private static int ParsePositive(string value, string argument)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new ArgumentOutOfRangeException(argument, value, "Expected a positive integer.");
        }

        return parsed;
    }

    private static string Resolve(string repositoryRoot, string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)));

    private sealed record Options(
        int ProfileSamples,
        int AffixSamples,
        int HashSeeds,
        string OutputPath);

    private sealed record AffixDrift(
        [property: JsonProperty("affix_id")] string AffixId,
        [property: JsonProperty("legacy_applied_value")] float LegacyAppliedValue,
        [property: JsonProperty("value_min")] float ValueMin,
        [property: JsonProperty("value_max")] float ValueMax,
        [property: JsonProperty("expected_mean")] double ExpectedMean,
        [property: JsonProperty("expected_drift_pct")] double ExpectedDriftPct,
        [property: JsonProperty("measured_sample_mean")] double MeasuredSampleMean,
        [property: JsonProperty("measured_sample_drift_pct")] double MeasuredSampleDriftPct);

    private sealed record ProfileQuantiles(
        string profile,
        string item_base_id,
        string grade,
        double baseline_q10,
        double baseline_q50,
        double baseline_q70,
        double baseline_q80,
        double baseline_q90,
        double q10,
        double q50,
        double q70,
        double q80,
        double q90);
}
