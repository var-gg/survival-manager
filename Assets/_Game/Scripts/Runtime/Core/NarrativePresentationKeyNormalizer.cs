using System;

namespace SM.Core;

public static class NarrativePresentationKeyNormalizer
{
    private const string DialogueScenePrefix = "dialogue_scene_";
    private const string DialogueOverlayPrefix = "dialogue_overlay_";
    private const string DialogueSequencePrefix = "dialogue_seq_";

    public static string ToDialogueSequenceId(string presentationKey)
    {
        if (string.IsNullOrWhiteSpace(presentationKey))
        {
            return presentationKey;
        }

        if (presentationKey.StartsWith(DialogueScenePrefix, StringComparison.Ordinal))
        {
            return DialogueSequencePrefix + presentationKey.Substring(DialogueScenePrefix.Length);
        }

        if (presentationKey.StartsWith(DialogueOverlayPrefix, StringComparison.Ordinal))
        {
            return DialogueSequencePrefix + presentationKey.Substring(DialogueOverlayPrefix.Length);
        }

        return presentationKey;
    }

    public static StoryPresentationKind ParseAuthoringKind(string authoringKind)
    {
        if (string.IsNullOrWhiteSpace(authoringKind))
        {
            throw new ArgumentException("Authoring kind is empty.", nameof(authoringKind));
        }

        return authoringKind.Trim() switch
        {
            "dialogue-scene" or "DialogueScene" => StoryPresentationKind.DialogueScene,
            "dialogue-overlay" or "DialogueOverlay" => StoryPresentationKind.DialogueOverlay,
            "story-card" or "StoryCard" => StoryPresentationKind.StoryCard,
            "toast-banner" or "ToastBanner" => StoryPresentationKind.ToastBanner,
            _ => throw new ArgumentException($"Unknown authoring presentation kind '{authoringKind}'."),
        };
    }

    public static StoryConditionKind ParseConditionKindAlias(string tokenKind)
    {
        if (string.IsNullOrWhiteSpace(tokenKind))
        {
            throw new ArgumentException("Condition kind token is empty.", nameof(tokenKind));
        }

        return tokenKind.Trim() switch
        {
            "ChapterIs" => StoryConditionKind.ChapterIs,
            "SiteIs" => StoryConditionKind.SiteIs,
            "NodeIs" => StoryConditionKind.NodeIs,
            "FlagIs" or "FlagSet" => StoryConditionKind.FlagSet,
            "FlagNot" or "FlagNotSet" => StoryConditionKind.FlagNotSet,
            "HeroUnlocked" => StoryConditionKind.HeroUnlocked,
            "HeroNotUnlocked" => StoryConditionKind.HeroNotUnlocked,
            _ => throw new ArgumentException($"Unknown condition kind alias '{tokenKind}'."),
        };
    }

    public static StoryEffectKind ParseEffectKindAlias(string tokenKind)
    {
        if (string.IsNullOrWhiteSpace(tokenKind))
        {
            throw new ArgumentException("Effect kind token is empty.", nameof(tokenKind));
        }

        return tokenKind.Trim() switch
        {
            "UnlockHero" => StoryEffectKind.UnlockHero,
            "SetFlag" => StoryEffectKind.SetFlag,
            "ClearFlag" => StoryEffectKind.ClearFlag,
            "EnqueuePresentation" => StoryEffectKind.EnqueuePresentation,
            "UnlockMode" => StoryEffectKind.UnlockMode,
            _ => throw new ArgumentException($"Unknown effect kind alias '{tokenKind}'."),
        };
    }
}
