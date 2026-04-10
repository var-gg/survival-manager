namespace SM.Unity.Narrative;

public readonly record struct StorySpeakerModel(
    string CharacterId,
    string DisplayNameText,
    StorySpeakerSide Side,
    string DefaultEmoteId);
