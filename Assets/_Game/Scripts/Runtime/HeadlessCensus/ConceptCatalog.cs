using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>discovery lane에는 비공개이며 evaluator/coverage adapter만 쓰는 BT1 카탈로그.</summary>
public sealed record ConceptCatalog(
    string SchemaVersion,
    bool RatificationPending,
    IReadOnlyList<OwnerConceptAnchor> OwnerAnchors,
    IReadOnlyList<OwnerConceptDerivation> AnchorDerivations,
    IReadOnlyList<ConceptVariant> SystemDerivedMedoids,
    ConceptCatalogSummary Summary);
