using UnityEngine;

namespace SM.Unity.Narrative;

public readonly record struct DialogueOverlayViewState(
    string SpeakerNameText,
    string LineText,
    bool IsNarrator,
    Sprite? LeftPortrait,
    bool ShowLeftPortrait,
    bool HighlightLeftPortrait,
    Sprite? RightPortrait,
    bool ShowRightPortrait,
    bool HighlightRightPortrait,
    bool ShowSkipAll,
    bool ShowContinueHint);
