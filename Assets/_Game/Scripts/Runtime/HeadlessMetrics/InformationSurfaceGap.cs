namespace SM.HeadlessMetrics;

/// <summary>자동 수정 없이 owner/content envelope 후보로 전달할 surface 위반 한 건.</summary>
public sealed record InformationSurfaceGap(
    string Kind,
    string SubjectId,
    string Missing,
    string OwnerContentCandidate);

public static class InformationSurfaceGapKind
{
    public const string ActionableOfferMissingSemantics = "actionable_offer_missing_semantics";
    public const string UndefinedVisibleToken = "undefined_visible_token";
    public const string HiddenPrerequisite = "hidden_prerequisite";
    public const string DescriptionBehaviorMismatch = "description_behavior_mismatch";
    public const string InteractionFeedbackMissing = "interaction_feedback_missing";
}
