namespace SM.HeadlessCensus;

public sealed record ConceptVariant(
    string VariantId,
    ConceptFingerprint Fingerprint,
    ConceptRecipe MedoidRecipe,
    int IsomorphicRecipeCount,
    ConceptContract Contract);
