using System.Collections.Generic;

namespace SM.Unity;

public enum SiteEventOutcomePreviewCategory
{
    NoChange,
    Item,
    Echo,
    Experience,
    WoundRecovery,
    WoundRisk,
    Route,
    Recruit,
    Consumable,
    ExtractBonus,
    Unknown,
}

public enum SiteEventOutcomePreviewCertainty
{
    Certain,
    TargetVaries,
    Unknown,
}

public sealed record SiteEventOutcomePreviewViewModel(
    SiteEventOutcomePreviewCategory Category,
    int IntensityPips,
    bool IsCost,
    SiteEventOutcomePreviewCertainty Certainty);

public sealed record SiteEventChoiceViewModel(
    string Id,
    string LabelKey,
    string IconId,
    IReadOnlyList<SiteEventOutcomePreviewViewModel> OutcomePreviews);

public sealed record SiteEventPresentationViewModel(
    string EventId,
    string SetupKey,
    IReadOnlyList<SiteEventChoiceViewModel> Choices);
