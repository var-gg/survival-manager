using System;
using System.Collections.Generic;

namespace SM.HeadlessMetrics;

/// <summary>BT3 네 hard metric과 피드백 폐루프 보조 지표를 보존하는 결정적 결과.</summary>
public sealed record InformationSurfaceAuditResult
{
    public const string CurrentSchemaVersion = "information-surface-audit-v1";

    public string SchemaVersion { get; init; } = CurrentSchemaVersion;
    public int ActionableEdgeCount { get; init; }
    public int ActionableSubjectCount { get; init; }
    public int VisibleSemanticCount { get; init; }
    public int VisibleTokenCount { get; init; }
    public int ActionableOfferMissingSemantics { get; init; }
    public int UndefinedVisibleToken { get; init; }
    public int HiddenPrerequisite { get; init; }
    public int DescriptionBehaviorMismatchCount { get; init; }
    public int FeedbackRequiredEdgeCount { get; init; }
    public int FeedbackWitnessedEdgeCount { get; init; }
    public double InteractionFeedbackCoverage { get; init; }
    public string DiscoverabilityNote { get; init; } =
        "v1 approximates normal-play discoverability by whether the E01 fact projection can reach the surface; catalog unlock timing is deferred.";
    public IReadOnlyList<InformationSurfaceGap> Gaps { get; init; } = Array.Empty<InformationSurfaceGap>();

    public IReadOnlyList<H100GateEvaluator.ExternalObservation> ToBt3Observations()
        => new[]
        {
            new H100GateEvaluator.ExternalObservation(
                "actionable_offer_missing_semantics",
                ActionableOfferMissingSemantics,
                ActionableSubjectCount,
                InformationSurfaceAuditArtifactWriter.FileName),
            new H100GateEvaluator.ExternalObservation(
                "undefined_visible_token",
                UndefinedVisibleToken,
                VisibleTokenCount,
                InformationSurfaceAuditArtifactWriter.FileName),
            new H100GateEvaluator.ExternalObservation(
                "hidden_prerequisite",
                HiddenPrerequisite,
                ActionableEdgeCount,
                InformationSurfaceAuditArtifactWriter.FileName),
            new H100GateEvaluator.ExternalObservation(
                "description_behavior_mismatch_count",
                DescriptionBehaviorMismatchCount,
                ActionableEdgeCount,
                InformationSurfaceAuditArtifactWriter.FileName),
            new H100GateEvaluator.ExternalObservation(
                "interaction_feedback_coverage",
                InteractionFeedbackCoverage,
                FeedbackRequiredEdgeCount,
                InformationSurfaceAuditArtifactWriter.FileName),
        };
}
