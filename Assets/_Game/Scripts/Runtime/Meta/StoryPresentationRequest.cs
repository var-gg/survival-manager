using System;
using SM.Core;

namespace SM.Meta;

public sealed record StoryPresentationRequest
{
    public StoryPresentationKind PresentationKind { get; init; }
    public string PresentationKey { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string[] SpeakerIds { get; init; } = Array.Empty<string>();
    public StoryPresentationContextRecord ContextSnapshot { get; init; } = StoryPresentationContextRecord.Empty;
}
