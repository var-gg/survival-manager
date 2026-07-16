using System.Collections.Generic;

namespace SM.HeadlessCensus;

internal sealed record ConceptCandidate(
    ConceptFingerprint Fingerprint,
    ConceptRecipe Recipe,
    ConceptContract Contract,
    int EquivalentRecipeCount,
    IReadOnlyList<string> MedoidTokens,
    int FrontlineCount,
    int ProtectedSlotCount,
    double BacklineAccessibility,
    double ExposureScore);
