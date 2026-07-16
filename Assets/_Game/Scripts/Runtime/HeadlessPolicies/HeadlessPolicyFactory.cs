using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace SM.HeadlessPolicies;

public static class HeadlessPolicyFactory
{
    public const string RandomLegalId = "random-legal-v1";
    public const string GreedyId = "greedy-v1";
    public const string DoctrineId = "competent-doctrine-v1";
    public const string FormationId = "competent-formation-v1";
    public const string CounterAdaptiveId = "competent-counter-adaptive-v1";
    public const string SearchPlannerId = "competent-search-planner-v1";

    public static IReadOnlyList<string> AllPolicyIds { get; } = new[]
    {
        RandomLegalId,
        GreedyId,
        DoctrineId,
        FormationId,
        CounterAdaptiveId,
        SearchPlannerId,
    };

    public static IHeadlessPolicy Create(string? policyId)
    {
        return NormalizePolicyId(policyId) switch
        {
            RandomLegalId => new RandomLegalPolicy(),
            GreedyId => new GreedyPolicy(),
            DoctrineId => new DoctrinePolicy(),
            FormationId => new FormationPolicy(),
            CounterAdaptiveId => new CounterAdaptivePolicy(),
            SearchPlannerId => new SearchPlannerPolicy(),
            var normalized => throw new InvalidOperationException($"Unsupported H100 policy '{normalized}'."),
        };
    }

    public static string NormalizePolicyId(string? policyId)
    {
        var value = string.IsNullOrWhiteSpace(policyId)
            ? GreedyId
            : policyId.Trim().ToLowerInvariant();
        return value switch
        {
            "random" or "random-legal" or RandomLegalId => RandomLegalId,
            "greedy" or "scripted-player-view-v1" or GreedyId => GreedyId,
            "doctrine" or "doctrine-v1" or DoctrineId => DoctrineId,
            "formation" or "formation-v1" or FormationId => FormationId,
            "counter" or "counter-adaptive" or "counter-adaptive-v1" or CounterAdaptiveId => CounterAdaptiveId,
            "search" or "planner" or "search-planner" or "search-planner-v1" or SearchPlannerId => SearchPlannerId,
            _ => throw new InvalidOperationException(
                $"Unknown H100 policy '{policyId}'. Expected one of: {string.Join(", ", AllPolicyIds.OrderBy(id => id, StringComparer.Ordinal))}."),
        };
    }
}
