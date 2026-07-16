using System;
using System.Linq;
using SM.HeadlessCensus;
using SM.HeadlessPolicies;

namespace SM.Editor.Validation;

/// <summary>E03 evaluator 계약 하나만 문자열 intent DTO로 낮춰 coverage policy에 주입한다.</summary>
internal static class H100ConceptIntentProjector
{
    public static HeadlessConceptIntent ProjectSingle(
        ConceptCatalog catalog,
        string requestedAnchorId)
    {
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }

        var derivation = string.IsNullOrWhiteSpace(requestedAnchorId)
            ? catalog.AnchorDerivations.OrderBy(value => value.AnchorId, StringComparer.Ordinal).FirstOrDefault()
            : catalog.AnchorDerivations.SingleOrDefault(value =>
                string.Equals(value.AnchorId, requestedAnchorId, StringComparison.Ordinal));
        if (derivation == null)
        {
            throw new InvalidOperationException($"Coverage anchor '{requestedAnchorId}' is not present in the evaluator catalog.");
        }

        // E03 deriver가 이미 fingerprint/recipe 기준의 결정적 순서를 보장한다. 그 첫 계약을 그대로 투영해
        // adapter에서 variant id 재정렬로 owner anchor의 대표 계약을 바꾸지 않는다.
        var variant = derivation.Variants.FirstOrDefault();
        if (derivation.DerivationGap || variant == null)
        {
            throw new InvalidOperationException($"Coverage anchor '{derivation.AnchorId}' has no legal intent variant.");
        }

        return Project(variant.Contract, $"coverage-{derivation.AnchorId}-{variant.VariantId}");
    }

    public static HeadlessConceptIntent Project(ConceptContract contract, string intentId)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        return new HeadlessConceptIntent(
            intentId,
            "coverage",
            contract.IdentityPredicates,
            contract.ProgressMilestones,
            contract.PayoffWitness,
            contract.AllowedSubstitutions,
            contract.FlexSlots,
            contract.CounterAffordances,
            contract.AvailabilityTier,
            contract.PivotConditions);
    }
}
