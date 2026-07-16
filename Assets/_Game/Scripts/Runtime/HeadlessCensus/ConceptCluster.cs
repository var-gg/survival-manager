using System.Collections.Generic;

namespace SM.HeadlessCensus;

internal sealed record ConceptCluster(
    ConceptFingerprint Fingerprint,
    ConceptCandidate Medoid,
    int RecipeCount,
    IReadOnlyList<ConceptCandidate> Members);
