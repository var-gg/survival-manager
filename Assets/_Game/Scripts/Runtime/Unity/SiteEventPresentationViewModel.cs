using System.Collections.Generic;

namespace SM.Unity;

public sealed record SiteEventChoiceViewModel(
    string Id,
    string LabelKey);

public sealed record SiteEventPresentationViewModel(
    string EventId,
    string SetupKey,
    IReadOnlyList<SiteEventChoiceViewModel> Choices);
