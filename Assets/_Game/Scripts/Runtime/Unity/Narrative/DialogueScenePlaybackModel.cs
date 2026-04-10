using System;
using System.Collections.Generic;

namespace SM.Unity.Narrative;

public sealed class DialogueScenePlaybackModel
{
    public StorySpeakerModel? LeftSpeaker { get; init; }
    public StorySpeakerModel? RightSpeaker { get; init; }
    public IReadOnlyList<StoryDialogueLineModel> Lines { get; init; } = Array.Empty<StoryDialogueLineModel>();
    public bool EnableTypingEffect { get; init; }
    public float CharactersPerSecond { get; init; }
    public bool RequireSkipConfirmation { get; init; }
    public string SkipConfirmTitleText { get; init; } = string.Empty;
    public string SkipConfirmBodyText { get; init; } = string.Empty;
}
