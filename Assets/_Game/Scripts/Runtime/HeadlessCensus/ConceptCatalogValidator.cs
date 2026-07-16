using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessCensus;

public static class ConceptCatalogValidator
{
    public static void RequireValid(ConceptCatalog catalog, IEnumerable<string> observablePayoffWitnesses)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        var failures = new List<string>();
        var witnesses = (observablePayoffWitnesses ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        if (catalog.OwnerAnchors.Count != catalog.AnchorDerivations.Count)
        {
            failures.Add("every owner anchor must have exactly one derivation result");
        }

        if (!catalog.RatificationPending || catalog.OwnerAnchors.Any(anchor => !anchor.RatificationPending))
        {
            failures.Add("owner draft must remain ratification_pending");
        }

        foreach (var derivation in catalog.AnchorDerivations)
        {
            if (derivation.DerivationGap == (derivation.LegalRecipeCount > 0))
            {
                failures.Add($"{derivation.AnchorId} must expose either legal recipes or a derivation_gap");
            }

            if (!derivation.DerivationGap && derivation.Variants.Count == 0)
            {
                failures.Add($"{derivation.AnchorId} has recipes without a medoid variant");
            }
        }

        foreach (var variant in catalog.AnchorDerivations.SelectMany(value => value.Variants)
                     .Concat(catalog.SystemDerivedMedoids))
        {
            if (!witnesses.Contains(variant.Contract.PayoffWitness))
            {
                failures.Add($"{variant.VariantId} uses unobservable payoff witness {variant.Contract.PayoffWitness}");
            }

            if (variant.Contract.AvailabilityTier is not ConceptAvailabilityTier.Core
                and not ConceptAvailabilityTier.Aspirational)
            {
                failures.Add($"{variant.VariantId} has invalid availability tier {variant.Contract.AvailabilityTier}");
            }
        }

        if (catalog.Summary.UnreachableThresholdReferenceCount != 0)
        {
            failures.Add($"unreachable threshold references={catalog.Summary.UnreachableThresholdReferenceCount}");
        }

        if (catalog.Summary.UnobservablePayoffWitnessCount != 0)
        {
            failures.Add($"unobservable payoff witnesses={catalog.Summary.UnobservablePayoffWitnessCount}");
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Concept catalog contract failed: {string.Join("; ", failures)}");
        }
    }
}
