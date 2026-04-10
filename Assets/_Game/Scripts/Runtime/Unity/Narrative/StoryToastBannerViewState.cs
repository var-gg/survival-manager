namespace SM.Unity.Narrative;

public readonly record struct StoryToastBannerViewState(
    string TitleText,
    string BodyText,
    bool AllowTapDismiss);
