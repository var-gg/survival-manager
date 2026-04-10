using UnityEngine;

namespace SM.Unity.Narrative;

public readonly record struct StoryCardViewState(
    string TitleText,
    string BodyText,
    Sprite? BackgroundSprite,
    Color BackgroundTint,
    bool ShowSkip,
    bool ShowContinueHint);
