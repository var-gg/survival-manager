using System;
using System.Linq;
using SM.Content;
using SM.Meta;

namespace SM.Unity.ContentConversion;

internal static class NarrativeRuntimeContentAdapter
{
    internal static StoryEventSpec ToSpec(StoryEventDefinition definition)
    {
        return new StoryEventSpec(
            definition.Id,
            definition.Moment,
            definition.Priority,
            definition.OncePolicy,
            (definition.Conditions ?? Array.Empty<StoryConditionDefinition>())
                .Where(condition => condition != null)
                .Select(ToSpec)
                .ToArray(),
            (definition.Effects ?? Array.Empty<StoryEffectDefinition>())
                .Where(effect => effect != null)
                .Select(ToSpec)
                .ToArray(),
            definition.PresentationKey);
    }

    internal static StoryConditionSpec ToSpec(StoryConditionDefinition definition)
    {
        return new StoryConditionSpec(
            definition.Id,
            definition.Kind,
            definition.OperandA,
            definition.OperandB);
    }

    internal static StoryEffectSpec ToSpec(StoryEffectDefinition definition)
    {
        return new StoryEffectSpec(
            definition.Id,
            definition.Kind,
            definition.Payload);
    }

    internal static DialogueSequenceSpec ToSpec(DialogueSequenceDefinition definition)
    {
        return new DialogueSequenceSpec(
            definition.Id,
            (definition.Lines ?? Array.Empty<DialogueLineDefinition>())
                .Where(line => line != null)
                .Select(ToSpec)
                .ToArray());
    }

    internal static DialogueLineSpec ToSpec(DialogueLineDefinition definition)
    {
        return new DialogueLineSpec(
            definition.Id,
            definition.SpeakerId,
            definition.TextKey,
            definition.Emote,
            definition.EmotionTextKey,
            definition.AutoAdvanceHint);
    }

    internal static HeroLoreSpec ToSpec(HeroLoreDefinition definition)
    {
        return new HeroLoreSpec(
            definition.Id,
            definition.HeroId,
            definition.Tier,
            definition.BeatBudget,
            definition.CanonBio,
            definition.UnresolvedHook);
    }

    internal static StoryArchiveCatalogSpec ToSpec(StoryArchiveCatalogDefinition? catalog)
    {
        if (catalog == null)
        {
            return StoryArchiveCatalogSpec.Empty;
        }

        return new StoryArchiveCatalogSpec(
            catalog.Entries
                .Where(entry => entry != null)
                .Select(ToSpec)
                .ToArray());
    }

    private static StoryArchiveEntrySpec ToSpec(StoryArchiveEntryDefinition definition)
    {
        return new StoryArchiveEntrySpec(
            definition.EventId,
            definition.ChapterId,
            definition.SiteId,
            definition.PresentationKey,
            definition.PresentationKind,
            definition.RuntimeContext,
            definition.ReplayPolicy,
            definition.LabelTextKey,
            definition.SourceOrder);
    }
}
