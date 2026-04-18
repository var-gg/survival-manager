using System;
using System.Collections.Generic;
using SM.Core;

namespace SM.Meta;

public sealed record StoryConditionSpec(
    string Id,
    StoryConditionKind Kind,
    string OperandA,
    string OperandB);

public sealed record StoryEffectSpec(
    string Id,
    StoryEffectKind Kind,
    string Payload);

public sealed record StoryEventSpec(
    string Id,
    NarrativeMoment Moment,
    int Priority,
    StoryOncePolicy OncePolicy,
    IReadOnlyList<StoryConditionSpec> Conditions,
    IReadOnlyList<StoryEffectSpec> Effects,
    string PresentationKey);

public sealed record DialogueLineSpec(
    string Id,
    string SpeakerId,
    string TextKey,
    string Emote,
    string EmotionTextKey,
    float AutoAdvanceHint);

public sealed record DialogueSequenceSpec(
    string Id,
    IReadOnlyList<DialogueLineSpec> Lines);

public sealed record HeroLoreSpec(
    string Id,
    string HeroId,
    NarrativeTier Tier,
    int BeatBudget,
    string CanonBio,
    string UnresolvedHook);

public sealed record StoryArchiveEntrySpec(
    string EventId,
    string ChapterId,
    string SiteId,
    string PresentationKey,
    StoryPresentationKind PresentationKind,
    NarrativeRuntimeContextKind RuntimeContext,
    StoryArchiveReplayPolicy ReplayPolicy,
    string LabelTextKey,
    int SourceOrder);

public sealed record StoryArchiveCatalogSpec(
    IReadOnlyList<StoryArchiveEntrySpec> Entries)
{
    public static StoryArchiveCatalogSpec Empty { get; } = new(Array.Empty<StoryArchiveEntrySpec>());
}
