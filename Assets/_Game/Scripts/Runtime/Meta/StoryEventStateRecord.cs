namespace SM.Meta;

public readonly record struct StoryEventStateRecord(
    int SeenCount,
    long LastPlayedTick,
    bool Resolved);
