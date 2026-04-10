namespace SM.Unity.Narrative;

public readonly record struct StoryDialogueLineModel(
    string SpeakerId,
    string SpeakerNameText,
    StorySpeakerSide SpeakerSide,
    string EmoteId,
    string LineText,
    bool IsNarrator);
