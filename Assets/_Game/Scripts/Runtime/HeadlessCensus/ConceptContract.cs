using System.Collections.Generic;

namespace SM.HeadlessCensus;

/// <summary>BT6/BT7 coverage lane이 소비할 수 있는 evaluator-only 컨셉 계약.</summary>
public sealed record ConceptContract(
    IReadOnlyList<string> IdentityPredicates,
    IReadOnlyList<string> ProgressMilestones,
    string PayoffWitness,
    IReadOnlyList<string> AllowedSubstitutions,
    IReadOnlyList<string> FlexSlots,
    IReadOnlyList<string> CounterAffordances,
    string AvailabilityTier,
    IReadOnlyList<string> PivotConditions);
