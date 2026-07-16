using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.HeadlessPolicies;

/// <summary>
/// coverage adapter가 evaluator 계약 하나를 낮춰 주입하거나 discovery policy가 가시 fact에서 만드는 순수 의도 DTO.
/// 원본 카탈로그 타입이나 evaluator assembly를 노출하지 않는다.
/// </summary>
public sealed class HeadlessConceptIntent
{
    public HeadlessConceptIntent(
        string intentId,
        string sourceLane,
        IReadOnlyList<string> identityPredicates,
        IReadOnlyList<string> progressMilestones,
        string payoffWitnessId,
        IReadOnlyList<string> allowedSubstitutions,
        IReadOnlyList<string> flexSlots,
        IReadOnlyList<string> counterAffordances,
        string availabilityTier,
        IReadOnlyList<string> pivotConditions)
    {
        IntentId = intentId ?? string.Empty;
        SourceLane = sourceLane ?? string.Empty;
        IdentityPredicates = Normalize(identityPredicates);
        ProgressMilestones = Normalize(progressMilestones);
        PayoffWitnessId = payoffWitnessId ?? string.Empty;
        AllowedSubstitutions = Normalize(allowedSubstitutions);
        FlexSlots = Normalize(flexSlots);
        CounterAffordances = Normalize(counterAffordances);
        AvailabilityTier = availabilityTier ?? string.Empty;
        PivotConditions = Normalize(pivotConditions);
    }

    public string IntentId { get; }
    public string SourceLane { get; }
    public IReadOnlyList<string> IdentityPredicates { get; }
    public IReadOnlyList<string> ProgressMilestones { get; }
    public string PayoffWitnessId { get; }
    public IReadOnlyList<string> AllowedSubstitutions { get; }
    public IReadOnlyList<string> FlexSlots { get; }
    public IReadOnlyList<string> CounterAffordances { get; }
    public string AvailabilityTier { get; }
    public IReadOnlyList<string> PivotConditions { get; }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values)
        => (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
}
