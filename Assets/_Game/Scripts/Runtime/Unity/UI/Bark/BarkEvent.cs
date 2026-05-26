namespace SM.Unity.UI.Bark;

/// <summary>
/// BarkBus payload — face cluster 위에 잠깐 떠오르는 단발성 line.
/// sourceId는 NpcId 또는 HeroId. emotionKey는 HeroFaceCard.uss `--{emotion}` swap과 일치.
/// barkText는 localized 후 전달 (BarkBus는 i18n 모름).
/// </summary>
public sealed record BarkEvent(
    string SourceId,
    string EmotionKey,
    string BarkText,
    float DurationSeconds = 2.4f);
