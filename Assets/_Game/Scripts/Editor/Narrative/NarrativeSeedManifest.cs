using System;
using System.Collections.Generic;
using SM.Core;

namespace SM.Editor.Narrative;

public sealed record NarrativeSeedManifest(
    int Version,
    string SourceHash,
    IReadOnlyList<NarrativeStoryEventDto> StoryEvents,
    IReadOnlyList<NarrativeDialogueSequenceDto> DialogueSequences,
    IReadOnlyList<NarrativePresentationDto> Presentations,
    IReadOnlyList<NarrativeArchiveEntryDto> ArchiveEntries,
    IReadOnlyList<NarrativeDiagnostic> Diagnostics);

public sealed record NarrativeStoryEventDto(
    string EventId,
    string ChapterId,
    string SiteId,
    NarrativeMoment Moment,
    int Priority,
    StoryOncePolicy OncePolicy,
    string PresentationKey,
    StoryPresentationKind PresentationKind,
    IReadOnlyList<NarrativeConditionDto> Conditions,
    IReadOnlyList<NarrativeEffectDto> Effects,
    int SourceOrder);

public sealed record NarrativeConditionDto(
    string KindToken,
    string OperandA);

public sealed record NarrativeEffectDto(
    string KindToken,
    string Payload);

public sealed record NarrativeDialogueSequenceDto(
    string SequenceId,
    string PresentationKey,
    StoryPresentationKind PresentationKind,
    NarrativeRuntimeContextKind RuntimeContext,
    IReadOnlyList<NarrativeDialogueLineDto> Lines);

public sealed record NarrativeDialogueLineDto(
    int LineIndex,
    string SpeakerAlias,
    string SpeakerId,
    string EmotionId,
    string EmoteId,
    string Text);

public sealed record NarrativePresentationDto(
    string PresentationKey,
    StoryPresentationKind Kind,
    NarrativeRuntimeContextKind RuntimeContext,
    string Title,
    string? Body,
    string? IconId);

public sealed record NarrativeArchiveEntryDto(
    string EventId,
    string ChapterId,
    string SiteId,
    string PresentationKey,
    StoryPresentationKind Kind,
    NarrativeRuntimeContextKind RuntimeContext,
    string DisplayTitle,
    int SourceOrder);
