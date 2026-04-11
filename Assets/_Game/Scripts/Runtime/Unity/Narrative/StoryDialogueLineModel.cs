namespace SM.Unity.Narrative;

public readonly record struct StoryDialogueLineModel(
    string SpeakerId,
    string SpeakerNameText,
    StorySpeakerSide SpeakerSide,
    string EmoteId,
    string EmotionText,
    string LineText,
    bool IsNarrator);
