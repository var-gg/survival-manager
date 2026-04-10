using System;
using System.Collections.Generic;

namespace SM.Unity.Narrative;

public sealed class DialogueOverlayPlaybackModel
{
    public StorySpeakerModel? LeftSpeaker { get; init; }
    public StorySpeakerModel? RightSpeaker { get; init; }
    public IReadOnlyList<StoryDialogueLineModel> Lines { get; init; } = Array.Empty<StoryDialogueLineModel>();
    public bool ShowSkipAll { get; init; }
}
