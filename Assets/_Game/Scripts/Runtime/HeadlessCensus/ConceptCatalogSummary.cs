namespace SM.HeadlessCensus;

public sealed record ConceptCatalogSummary(
    int OwnerAnchorCount,
    int OwnerAnchorWithRecipeCount,
    int DerivationGapCount,
    int OwnerVariantCount,
    int SystemDerivedMedoidCount,
    int CandidateRecipeCount,
    int IsomorphicClusterCount,
    int IsomorphicDuplicateCount,
    int RawStatOnlyExcludedCount,
    int UnobservablePayoffWitnessCount,
    int UnreachableThresholdReferenceCount,
    int CoreVariantCount,
    int AspirationalVariantCount);
