using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

/// <summary>owner anchor와 system motif를 분리한 결정적 BT1 concept catalog를 만든다.</summary>
public static class ConceptCatalogDeriver
{
    public const string SchemaVersion = "concept-catalog-bt1-v1";

    public static ConceptCatalog Derive(
        IEnumerable<OwnerConceptAnchor> ownerAnchors,
        BuildSpaceCensus census,
        BuildGrammarTruthGraph truthGraph,
        IEnumerable<string> observablePayoffWitnesses)
    {
        var anchors = ValidateAnchors(ownerAnchors);
        var enumeration = ConceptMotifEnumerator.Enumerate(census, truthGraph, observablePayoffWitnesses);
        var allClusters = ConceptClusterer.Cluster(enumeration.Candidates);
        var matchedFingerprints = new HashSet<string>(StringComparer.Ordinal);
        var derivations = new List<OwnerConceptDerivation>();
        foreach (var anchor in anchors)
        {
            var matched = ConceptAnchorMatcher.Match(anchor, enumeration.Candidates);
            var clusters = ConceptClusterer.Cluster(matched);
            foreach (var cluster in clusters)
            {
                matchedFingerprints.Add(cluster.Fingerprint.Signature);
            }

            var variants = clusters.Select(cluster => ToVariant(anchor.AnchorId, cluster)).ToArray();
            var legalRecipeCount = ClampCount(clusters.Sum(cluster => (long)cluster.RecipeCount));
            var gap = variants.Length == 0;
            derivations.Add(new OwnerConceptDerivation(
                anchor.AnchorId,
                clusters.Select(cluster => ConceptAnchorMatcher.MappingLabel(cluster.Fingerprint))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                legalRecipeCount,
                gap,
                gap ? "No observable, reachable truth-graph motif matched this fantasy without inventing a recipe or witness." : string.Empty,
                variants));
        }

        var systemMedoids = allClusters
            .Where(cluster => !matchedFingerprints.Contains(cluster.Fingerprint.Signature))
            .Select(cluster => ToVariant("system", cluster))
            .OrderBy(variant => variant.Fingerprint.Signature, StringComparer.Ordinal)
            .ToArray();
        var allVariants = derivations.SelectMany(derivation => derivation.Variants).Concat(systemMedoids).ToArray();
        var candidateRecipes = ClampCount(enumeration.Candidates.Sum(candidate => (long)candidate.EquivalentRecipeCount));
        var summary = new ConceptCatalogSummary(
            anchors.Length,
            derivations.Count(derivation => !derivation.DerivationGap),
            derivations.Count(derivation => derivation.DerivationGap),
            derivations.Sum(derivation => derivation.Variants.Count),
            systemMedoids.Length,
            candidateRecipes,
            allClusters.Count,
            Math.Max(0, candidateRecipes - allClusters.Count),
            enumeration.RawStatOnlyExcludedCount,
            enumeration.UnobservablePayoffWitnessCount,
            enumeration.UnreachableThresholdReferenceCount,
            allVariants.Count(variant => variant.Contract.AvailabilityTier == ConceptAvailabilityTier.Core),
            allVariants.Count(variant => variant.Contract.AvailabilityTier == ConceptAvailabilityTier.Aspirational));
        var catalog = new ConceptCatalog(
            SchemaVersion,
            anchors.All(anchor => anchor.RatificationPending),
            anchors,
            derivations.OrderBy(derivation => derivation.AnchorId, StringComparer.Ordinal).ToArray(),
            systemMedoids,
            summary);
        ConceptCatalogValidator.RequireValid(catalog, observablePayoffWitnesses);
        return catalog;
    }

    private static OwnerConceptAnchor[] ValidateAnchors(IEnumerable<OwnerConceptAnchor> anchors)
    {
        if (anchors == null)
        {
            throw new ArgumentNullException(nameof(anchors));
        }

        var ordered = anchors.OrderBy(anchor => anchor.AnchorId, StringComparer.Ordinal).ToArray();
        if (ordered.Any(anchor => anchor == null
                                  || string.IsNullOrWhiteSpace(anchor.AnchorId)
                                  || string.IsNullOrWhiteSpace(anchor.DisplayName)
                                  || string.IsNullOrWhiteSpace(anchor.Fantasy)))
        {
            throw new ArgumentException("Owner concept anchor identity and fantasy must be non-empty.", nameof(anchors));
        }

        if (ordered.Select(anchor => anchor.AnchorId).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException("Owner concept anchor ids must be unique.", nameof(anchors));
        }

        return ordered;
    }

    private static ConceptVariant ToVariant(string ownerId, ConceptCluster cluster)
    {
        var variantId = ConceptStableId.Create("concept", ownerId, cluster.Fingerprint.Signature);
        return new ConceptVariant(
            variantId,
            cluster.Fingerprint,
            cluster.Medoid.Recipe,
            cluster.RecipeCount,
            cluster.Medoid.Contract);
    }

    private static int ClampCount(long value) => (int)Math.Min(int.MaxValue, Math.Max(0L, value));
}
