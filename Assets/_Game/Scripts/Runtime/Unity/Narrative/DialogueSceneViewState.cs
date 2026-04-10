using UnityEngine;

namespace SM.Unity.Narrative;

public readonly record struct DialogueSceneViewState(
    string SpeakerNameText,
    string LineText,
    bool IsNarrator,
    StorySpeakerSide ActiveSpeakerSide,
    Sprite? LeftPortrait,
    bool ShowLeftPortrait,
    Sprite? RightPortrait,
    bool ShowRightPortrait,
    bool ShowSkipAll,
    bool ShowSkipConfirmation,
    string SkipConfirmTitleText,
    string SkipConfirmBodyText,
    bool IsTyping,
    bool ShowContinueHint);
