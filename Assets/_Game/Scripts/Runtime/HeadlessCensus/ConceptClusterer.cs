using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>동형 fingerprint cluster에서 weighted token distance medoid를 결정적으로 고른다.</summary>
internal static class ConceptClusterer
{
    public static IReadOnlyList<ConceptCluster> Cluster(IEnumerable<ConceptCandidate> candidates)
    {
        return (candidates ?? Array.Empty<ConceptCandidate>())
            .OrderBy(candidate => candidate.Fingerprint.Signature, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Recipe.RecipeId, StringComparer.Ordinal)
            .GroupBy(candidate => candidate.Fingerprint.Signature, StringComparer.Ordinal)
            .Select(BuildCluster)
            .OrderBy(cluster => cluster.Fingerprint.Signature, StringComparer.Ordinal)
            .ToArray();
    }

    private static ConceptCluster BuildCluster(IGrouping<string, ConceptCandidate> group)
    {
        var members = group.OrderBy(candidate => candidate.Recipe.RecipeId, StringComparer.Ordinal).ToArray();
        var tokenWeights = new Dictionary<string, long>(StringComparer.Ordinal);
        long totalWeight = 0;
        foreach (var member in members)
        {
            totalWeight += member.EquivalentRecipeCount;
            foreach (var token in member.MedoidTokens.Distinct(StringComparer.Ordinal))
            {
                tokenWeights[token] = tokenWeights.TryGetValue(token, out var current)
                    ? current + member.EquivalentRecipeCount
                    : member.EquivalentRecipeCount;
            }
        }

        var medoid = members.Select(candidate => new
            {
                Candidate = candidate,
                Distance = WeightedDistance(candidate, tokenWeights, totalWeight),
            })
            .OrderBy(value => value.Distance)
            .ThenBy(value => value.Candidate.Recipe.RecipeId, StringComparer.Ordinal)
            .First().Candidate;
        return new ConceptCluster(
            medoid.Fingerprint,
            medoid,
            checked((int)Math.Min(int.MaxValue, totalWeight)),
            members);
    }

    private static long WeightedDistance(
        ConceptCandidate candidate,
        IReadOnlyDictionary<string, long> tokenWeights,
        long totalWeight)
    {
        var selected = candidate.MedoidTokens.Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        long distance = 0;
        foreach (var pair in tokenWeights)
        {
            distance += selected.Contains(pair.Key) ? totalWeight - pair.Value : pair.Value;
        }

        return distance;
    }
}
