using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace SM.HeadlessPolicies;

public static class HeadlessPolicyFactory
{
    public const string CoverageId = "qa-formation-coverage-v1";
    public const string RandomLegalId = "random-legal-v1";
    public const string GreedyId = "greedy-v1";
    public const string DoctrineId = "competent-doctrine-v1";
    public const string FormationId = "competent-formation-v1";
    public const string CounterAdaptiveId = "competent-counter-adaptive-v1";
    public const string SearchPlannerId = "competent-search-planner-v1";
    public const string PreviewGroundedConceptId = ConceptCommitPolicy.PreviewGroundedPolicyId;

    /// <summary>H100 성능 비교 cohort. Coverage는 발동 가능성을 표본화하는 QA 정책이라 제외한다.</summary>
    public static IReadOnlyList<string> ProductionPolicyIds { get; } = new[]
    {
        RandomLegalId,
        GreedyId,
        DoctrineId,
        FormationId,
        CounterAdaptiveId,
        SearchPlannerId,
    };

    public static IReadOnlyList<string> AllPolicyIds { get; } = new[]
    {
        CoverageId,
        RandomLegalId,
        GreedyId,
        DoctrineId,
        FormationId,
        CounterAdaptiveId,
        SearchPlannerId,
    };

    /// <summary>기존 비교 cohort를 바꾸지 않고 별도 실험 lane에 등록된 정책 ID.</summary>
    public static IReadOnlyList<string> RegisteredPolicyIds { get; } = AllPolicyIds
        .Concat(new[] { PreviewGroundedConceptId })
        .ToArray();

    public static IHeadlessPolicy Create(string? policyId)
    {
        return NormalizePolicyId(policyId) switch
        {
            CoverageId => new CoveragePolicy(),
            RandomLegalId => new RandomLegalPolicy(),
            GreedyId => new GreedyPolicy(),
            DoctrineId => new DoctrinePolicy(),
            FormationId => new FormationPolicy(),
            CounterAdaptiveId => new CounterAdaptivePolicy(),
            SearchPlannerId => new SearchPlannerPolicy(),
            PreviewGroundedConceptId => ConceptCommitPolicy.CreatePreviewGrounded(),
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
            "coverage" or "formation-coverage" or CoverageId => CoverageId,
            "random" or "random-legal" or RandomLegalId => RandomLegalId,
            "greedy" or "scripted-player-view-v1" or GreedyId => GreedyId,
            "doctrine" or "doctrine-v1" or DoctrineId => DoctrineId,
            "formation" or "formation-v1" or FormationId => FormationId,
            "counter" or "counter-adaptive" or "counter-adaptive-v1" or CounterAdaptiveId => CounterAdaptiveId,
            "search" or "planner" or "search-planner" or "search-planner-v1" or SearchPlannerId => SearchPlannerId,
            "preview-grounded" or "concept-preview" or PreviewGroundedConceptId => PreviewGroundedConceptId,
            _ => throw new InvalidOperationException(
                $"Unknown H100 policy '{policyId}'. Expected one of: {string.Join(", ", RegisteredPolicyIds.OrderBy(id => id, StringComparer.Ordinal))}."),
        };
    }
}
