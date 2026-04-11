using SM.Content.Definitions;

namespace SM.Content;

public static class NarrativeLocalizationKeys
{
    public static string DialogueLine(string sequenceId, int lineIndex)
    {
        return $"loc.story.dialogue.{sequenceId}.{lineIndex:D3}";
    }

    public static string PresentationTitle(string presentationKey)
    {
        return $"loc.story.presentation.{ContentLocalizationTables.NormalizeId(presentationKey)}.title";
    }

    public static string PresentationBody(string presentationKey)
    {
        return $"loc.story.presentation.{ContentLocalizationTables.NormalizeId(presentationKey)}.body";
    }

    public static string Emotion(string emotionId)
    {
        return $"loc.story.emotion.{emotionId}";
    }

    public static string ArchiveLabel(string eventId)
    {
        return $"loc.story.archive.{ContentLocalizationTables.NormalizeId(eventId)}.label";
    }
}
