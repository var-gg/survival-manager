using SM.Core;

namespace SM.Meta;

public sealed record StoryArchiveEntryRecord(
    string EventId,
    string ChapterId,
    string SiteId,
    string PresentationKey,
    StoryPresentationKind PresentationKind,
    string LabelText,
    bool Unlocked,
    NarrativeRuntimeContextKind RuntimeContext,
    StoryArchiveReplayPolicy ReplayPolicy,
    int SourceOrder);
