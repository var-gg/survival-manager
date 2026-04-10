using SM.Core;

namespace SM.Editor.SeedData;

internal sealed record StoryStringSeed(
    string Key,
    string Ko,
    string En = "");

internal sealed record StoryConditionSeed(
    StoryConditionKind Kind,
    string OperandA,
    string OperandB = "");

internal sealed record StoryEffectSeed(
    StoryEffectKind Kind,
    string Payload);

internal sealed record StoryEventSeed(
    string Id,
    NarrativeMoment Moment,
    int Priority,
    string PresentationKey,
    StoryConditionSeed[] Conditions,
    StoryEffectSeed[] Effects,
    StoryOncePolicy OncePolicy = StoryOncePolicy.OncePerProfile);

internal sealed record DialogueLineSeed(
    string SpeakerId,
    string Ko,
    string En = "",
    string Emotion = "",
    float AutoAdvanceHint = 0f);

internal sealed record DialogueSequenceSeed(
    string Id,
    DialogueLineSeed[] Lines);

internal sealed record ChapterBeatSeed(
    string Id,
    string ChapterId,
    string SiteId,
    int NodeIndex,
    string BeatLabel,
    float TensionTarget,
    float ReliefTarget,
    float HumorTarget,
    float CatharsisTarget);

internal sealed record HeroLoreSeed(
    string Id,
    string HeroId,
    NarrativeTier Tier,
    int BeatBudget,
    string UnresolvedHook,
    string TitleKo,
    string SummaryKo,
    string[] BodyKo,
    string TitleEn = "",
    string SummaryEn = "",
    string[]? BodyEn = null);

internal sealed record PresentationTextSeed(
    string PresentationKey,
    string KoTitle,
    string KoBody,
    string EnTitle = "",
    string EnBody = "");
