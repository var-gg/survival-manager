using System.Security.Cryptography;
using System.Text;

internal static class RefitFarmTailBootstrap
{
    internal static RefitFarmTailMetricResult Evaluate(
        string slice,
        IReadOnlyList<RefitFarmScenarioResult> observations,
        int replicates,
        bool enoughActivity)
    {
        if (observations.Count == 0)
        {
            throw new InvalidDataException($"Tail metric slice '{slice}' has no observations.");
        }

        if (replicates < 2_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replicates),
                replicates,
                "Tail metric bootstrap requires at least 2,000 replicates.");
        }

        var clusters = observations
            .GroupBy(value => value.SeedSalt)
            .OrderBy(group => group.Key)
            .Select(group => group.ToArray())
            .ToArray();
        if (clusters.Length < 2)
        {
            throw new InvalidDataException(
                $"Tail metric slice '{slice}' requires at least two seed clusters.");
        }

        var point = Calculate(observations);
        var random = new Random(StableSeed($"refit-tail|{slice}|r={replicates}"));
        var resampled = new List<RefitFarmScenarioResult>(observations.Count);
        var aValues = new List<double>(replicates);
        var tValues = new List<double>(replicates);
        for (var replicate = 0; replicate < replicates; replicate++)
        {
            resampled.Clear();
            for (var index = 0; index < clusters.Length; index++)
            {
                var selected = clusters[random.Next(clusters.Length)];
                resampled.AddRange(selected);
            }

            var metric = Calculate(resampled);
            if (metric.ALow.HasValue)
            {
                aValues.Add(metric.ALow.Value);
            }

            if (metric.TTop.HasValue)
            {
                tValues.Add(metric.TTop.Value);
            }
        }

        var aInterval = Interval(aValues, replicates);
        var tInterval = Interval(tValues, replicates);
        var aPass = aInterval is { } a
                    && a.Lower >= 0.50d
                    && a.Upper <= 0.65d;
        var aFail = aInterval is { } af
                    && (af.Upper < 0.50d || af.Lower > 0.65d);
        var tPass = tInterval is { } t && t.Lower >= 0.80d;
        var tFail = tInterval is { } tf && tf.Upper < 0.80d;
        return new RefitFarmTailMetricResult(
            slice,
            point.ALow,
            new double?[] { aInterval?.Lower, aInterval?.Upper },
            aPass,
            point.TTop,
            new double?[] { tInterval?.Lower, tInterval?.Upper },
            tPass,
            enoughActivity && (aPass || aFail) && (tPass || tFail));
    }

    private static (double? ALow, double? TTop) Calculate(
        IReadOnlyList<RefitFarmScenarioResult> observations)
    {
        var drops = observations
            .Select(value => (value.DropsOnlyPower / value.InitialPower) - 1d)
            .ToArray();
        var refit = observations
            .Select(value => (value.DropsAndRefitPower / value.InitialPower) - 1d)
            .ToArray();
        var dropsLowWidth = Quantile(drops, 0.50d) - Quantile(drops, 0.10d);
        var dropsTopWidth = Quantile(drops, 0.90d) - Quantile(drops, 0.50d);
        double? aLow = Math.Abs(dropsLowWidth) <= 1e-15d
            ? null
            : 1d - ((Quantile(refit, 0.50d) - Quantile(refit, 0.10d)) / dropsLowWidth);
        double? tTop = Math.Abs(dropsTopWidth) <= 1e-15d
            ? null
            : (Quantile(refit, 0.90d) - Quantile(refit, 0.50d)) / dropsTopWidth;
        if (aLow.HasValue && !double.IsFinite(aLow.Value))
        {
            aLow = null;
        }

        if (tTop.HasValue && !double.IsFinite(tTop.Value))
        {
            tTop = null;
        }

        return (aLow, tTop);
    }

    private static double Quantile(IReadOnlyList<double> values, double probability)
    {
        if (values.Count == 0)
        {
            throw new InvalidDataException("Quantile requires at least one value.");
        }

        var ordered = values.OrderBy(value => value).ToArray();
        var index = Math.Clamp(
            (int)Math.Round(
                (ordered.Length - 1) * probability,
                MidpointRounding.AwayFromZero),
            0,
            ordered.Length - 1);
        return ordered[index];
    }

    private static (double Lower, double Upper)? Interval(
        List<double> values,
        int requestedReplicates)
    {
        if (values.Count < requestedReplicates * 0.95d)
        {
            return null;
        }

        values.Sort();
        return (
            values[PercentileIndex(values.Count, 0.025d)],
            values[PercentileIndex(values.Count, 0.975d)]);
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
}
