using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>정규화 L1 distance와 deterministic farthest-first + cluster medoid refinement를 사용한다.</summary>
public static class FormationMedoidSelector
{
    private const double Epsilon = 0.000000001d;
    private const int MaxRefinementIterations = 32;

    public static IReadOnlyList<FormationMedoid> Select(
        IReadOnlyList<FormationPlacement> placements,
        int medoidCount)
    {
        if (placements == null || placements.Count == 0)
        {
            throw new ArgumentException("Formation placements are empty.", nameof(placements));
        }

        if (medoidCount <= 0 || medoidCount > placements.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(medoidCount));
        }

        var points = Normalize(placements);
        var medoids = Initialize(points, medoidCount);
        for (var iteration = 0; iteration < MaxRefinementIterations; iteration++)
        {
            var clusters = Assign(points, medoids);
            var refined = new List<Point>(medoids.Count);
            for (var clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                refined.Add(clusters[clusterIndex].Count == 0
                    ? medoids[clusterIndex]
                    : SelectClusterMedoid(clusters[clusterIndex]));
            }

            if (refined.Select(point => point.Placement.Signature)
                .SequenceEqual(medoids.Select(point => point.Placement.Signature), StringComparer.Ordinal))
            {
                medoids = refined;
                break;
            }

            medoids = refined;
        }

        var finalClusters = Assign(points, medoids);
        return medoids.Select((medoid, index) => new FormationMedoid(
                medoid.Placement,
                finalClusters[index].Count,
                Round(finalClusters[index].Sum(point => Distance(point.Vector, medoid.Vector)))))
            .OrderBy(medoid => medoid.Placement.Signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<Point> Normalize(IReadOnlyList<FormationPlacement> placements)
    {
        var raw = placements.Select(placement => placement.Features.ToMedoidVector()).ToArray();
        var dimensionCount = raw[0].Length;
        var minima = new double[dimensionCount];
        var maxima = new double[dimensionCount];
        for (var dimension = 0; dimension < dimensionCount; dimension++)
        {
            minima[dimension] = raw.Min(vector => vector[dimension]);
            maxima[dimension] = raw.Max(vector => vector[dimension]);
        }

        return placements.Select((placement, index) =>
        {
            var vector = new double[dimensionCount];
            for (var dimension = 0; dimension < dimensionCount; dimension++)
            {
                var range = maxima[dimension] - minima[dimension];
                vector[dimension] = range <= Epsilon ? 0d : (raw[index][dimension] - minima[dimension]) / range;
            }

            return new Point(placement, vector);
        }).OrderBy(point => point.Placement.Signature, StringComparer.Ordinal).ToArray();
    }

    private static List<Point> Initialize(IReadOnlyList<Point> points, int count)
    {
        var medoids = new List<Point>(count)
        {
            points.OrderBy(point => points.Sum(other => Distance(point.Vector, other.Vector)))
                .ThenBy(point => point.Placement.Signature, StringComparer.Ordinal)
                .First()
        };

        while (medoids.Count < count)
        {
            var next = points.Where(point => medoids.All(medoid => medoid.Placement.Signature != point.Placement.Signature))
                .Select(point => new
                {
                    Point = point,
                    NearestDistance = medoids.Min(medoid => Distance(point.Vector, medoid.Vector)),
                })
                .OrderByDescending(candidate => candidate.NearestDistance)
                .ThenBy(candidate => candidate.Point.Placement.Signature, StringComparer.Ordinal)
                .First();
            medoids.Add(next.Point);
        }

        return medoids;
    }

    private static IReadOnlyList<List<Point>> Assign(IReadOnlyList<Point> points, IReadOnlyList<Point> medoids)
    {
        var clusters = Enumerable.Range(0, medoids.Count).Select(_ => new List<Point>()).ToArray();
        foreach (var point in points)
        {
            var bestIndex = 0;
            var bestDistance = Distance(point.Vector, medoids[0].Vector);
            for (var index = 1; index < medoids.Count; index++)
            {
                var distance = Distance(point.Vector, medoids[index].Vector);
                if (distance < bestDistance - Epsilon
                    || Math.Abs(distance - bestDistance) <= Epsilon
                    && string.CompareOrdinal(medoids[index].Placement.Signature, medoids[bestIndex].Placement.Signature) < 0)
                {
                    bestIndex = index;
                    bestDistance = distance;
                }
            }

            clusters[bestIndex].Add(point);
        }

        return clusters;
    }

    private static Point SelectClusterMedoid(IReadOnlyList<Point> cluster)
    {
        return cluster.Select(candidate => new
            {
                Point = candidate,
                TotalDistance = cluster.Sum(other => Distance(candidate.Vector, other.Vector)),
            })
            .OrderBy(candidate => candidate.TotalDistance)
            .ThenBy(candidate => candidate.Point.Placement.Signature, StringComparer.Ordinal)
            .First().Point;
    }

    private static double Distance(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var distance = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            distance += Math.Abs(left[index] - right[index]);
        }

        return distance;
    }

    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);

    private sealed record Point(FormationPlacement Placement, double[] Vector);
}
