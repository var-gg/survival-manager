using System.Collections.Generic;

namespace SM.HeadlessCensus;

internal sealed record ConceptMotifEnumerationResult(
    IReadOnlyList<ConceptCandidate> Candidates,
    int RawStatOnlyExcludedCount,
    int UnobservablePayoffWitnessCount,
    int UnreachableThresholdReferenceCount);
