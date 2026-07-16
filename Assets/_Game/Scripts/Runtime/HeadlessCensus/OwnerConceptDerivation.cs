using System.Collections.Generic;

namespace SM.HeadlessCensus;

public sealed record OwnerConceptDerivation(
    string AnchorId,
    IReadOnlyList<string> MappedMotifs,
    int LegalRecipeCount,
    bool DerivationGap,
    string DerivationGapReason,
    IReadOnlyList<ConceptVariant> Variants);
