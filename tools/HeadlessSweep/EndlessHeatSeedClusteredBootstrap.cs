using System.Security.Cryptography;
using System.Text;

internal readonly record struct EndlessHeatMeanCluster(
    int SeedSalt,
    double Value);

internal readonly record struct EndlessHeatRatioCluster(
    int SeedSalt,
    double Numerator,
    double Denominator);

internal static class EndlessHeatSeedClusteredBootstrap
{
    internal const int Replicates = 10_000;

    internal static EndlessHeatConfidenceInterval EstimateMean(
        IReadOnlyList<EndlessHeatMeanCluster> clusters,
        string identity)
    {
        RequireClusters(clusters.Count);
        return Bootstrap(
            clusters.Count,
            identity,
            indices => indices.Average(index => clusters[index].Value));
    }

    internal static EndlessHeatPairedDelta PairedMeanDelta(
        IReadOnlyList<EndlessHeatMeanCluster> current,
        IReadOnlyList<EndlessHeatMeanCluster> baseline,
        string identity)
    {
        RequireAligned(current.Select(value => value.SeedSalt), baseline.Select(value => value.SeedSalt));
        var estimate = current.Average(value => value.Value)
                       - baseline.Average(value => value.Value);
        var interval = Bootstrap(
            current.Count,
            identity,
            indices => indices.Average(index =>
                current[index].Value - baseline[index].Value));
        return new EndlessHeatPairedDelta(estimate, interval);
    }

    internal static EndlessHeatConfidenceInterval EstimateRatio(
        IReadOnlyList<EndlessHeatRatioCluster> clusters,
        string identity)
    {
        RequireClusters(clusters.Count);
        return Bootstrap(
            clusters.Count,
            identity,
            indices => Ratio(
                indices.Sum(index => clusters[index].Numerator),
                indices.Sum(index => clusters[index].Denominator)));
    }

    internal static EndlessHeatPairedDelta PairedRatioDelta(
        IReadOnlyList<EndlessHeatRatioCluster> current,
        IReadOnlyList<EndlessHeatRatioCluster> baseline,
        string identity)
    {
        RequireAligned(current.Select(value => value.SeedSalt), baseline.Select(value => value.SeedSalt));
        var estimate = Ratio(
                           current.Sum(value => value.Numerator),
                           current.Sum(value => value.Denominator))
                       - Ratio(
                           baseline.Sum(value => value.Numerator),
                           baseline.Sum(value => value.Denominator));
        var interval = Bootstrap(
            current.Count,
            identity,
            indices => Ratio(
                           indices.Sum(index => current[index].Numerator),
                           indices.Sum(index => current[index].Denominator))
                       - Ratio(
                           indices.Sum(index => baseline[index].Numerator),
                           indices.Sum(index => baseline[index].Denominator)));
        return new EndlessHeatPairedDelta(estimate, interval);
    }

    internal static double Ratio(double numerator, double denominator)
    {
        if (denominator <= 0d)
        {
            throw new InvalidDataException(
                $"Seed-clustered ratio denominator must be positive, found {denominator:R}.");
        }

        return numerator / denominator;
    }

    private static EndlessHeatConfidenceInterval Bootstrap(
        int clusterCount,
        string identity,
        Func<IReadOnlyList<int>, double> statistic)
    {
        var random = new Random(StableSeed(identity));
        var indices = new int[clusterCount];
        var values = new double[Replicates];
        for (var replicate = 0; replicate < values.Length; replicate++)
        {
            for (var index = 0; index < indices.Length; index++)
            {
                indices[index] = random.Next(clusterCount);
            }

            values[replicate] = statistic(indices);
        }

        Array.Sort(values);
        return new EndlessHeatConfidenceInterval(
            values[PercentileIndex(values.Length, 0.025d)],
            values[PercentileIndex(values.Length, 0.975d)]);
    }

    private static int StableSeed(string identity)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
    }

    private static int PercentileIndex(int count, double percentile)
        => Math.Clamp(
            (int)Math.Round((count - 1) * percentile, MidpointRounding.AwayFromZero),
            0,
            count - 1);

    private static void RequireAligned(
        IEnumerable<int> current,
        IEnumerable<int> baseline)
    {
        var currentArray = current.ToArray();
        var baselineArray = baseline.ToArray();
        RequireClusters(currentArray.Length);
        if (!currentArray.SequenceEqual(baselineArray))
        {
            throw new InvalidDataException(
                "Paired bootstrap requires identical ordered seed-cluster vectors.");
        }
    }

    private static void RequireClusters(int count)
    {
        if (count <= 1)
        {
            throw new InvalidDataException(
                $"Seed-clustered bootstrap requires at least two seeds, found {count}.");
        }
    }
}
