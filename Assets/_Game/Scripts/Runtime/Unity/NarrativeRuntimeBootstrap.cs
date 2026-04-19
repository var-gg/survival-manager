using System;
using System.Linq;
using SM.Content;
using SM.Meta;
using SM.Unity.ContentConversion;
using UnityEngine;

namespace SM.Unity;

internal sealed class NarrativeRuntimeBootstrap
{
    private const string StoryEventsResourcesPath = "_Game/Content/Definitions/StoryEvents";
    private const string DialogueSequencesResourcesPath = "_Game/Content/Definitions/DialogueSequences";
    private const string HeroLoreResourcesPath = "_Game/Content/Definitions/HeroLore";

    private readonly StoryEventDefinition[] _storyEvents;
    private readonly DialogueSequenceDefinition[] _dialogueSequences;
    private readonly HeroLoreDefinition[] _heroLoreDefinitions;

    private NarrativeRuntimeBootstrap(
        StoryEventDefinition[] storyEvents,
        DialogueSequenceDefinition[] dialogueSequences,
        HeroLoreDefinition[] heroLoreDefinitions)
    {
        _storyEvents = storyEvents ?? Array.Empty<StoryEventDefinition>();
        _dialogueSequences = dialogueSequences ?? Array.Empty<DialogueSequenceDefinition>();
        _heroLoreDefinitions = heroLoreDefinitions ?? Array.Empty<HeroLoreDefinition>();
    }

    internal static NarrativeRuntimeBootstrap LoadFromResources()
    {
        return new NarrativeRuntimeBootstrap(
            Resources.LoadAll<StoryEventDefinition>(StoryEventsResourcesPath)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray(),
            Resources.LoadAll<DialogueSequenceDefinition>(DialogueSequencesResourcesPath)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray(),
            Resources.LoadAll<HeroLoreDefinition>(HeroLoreResourcesPath)
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray());
    }

    internal static NarrativeRuntimeBootstrap CreateEmpty()
    {
        return new NarrativeRuntimeBootstrap(
            Array.Empty<StoryEventDefinition>(),
            Array.Empty<DialogueSequenceDefinition>(),
            Array.Empty<HeroLoreDefinition>());
    }

    internal StoryDirectorService CreateStoryDirector(NarrativeProgressRecord? initialProgress)
    {
        var assemblyService = new DialogueAssemblyService(
            _dialogueSequences.Select(NarrativeRuntimeContentAdapter.ToSpec).ToArray(),
            _heroLoreDefinitions.Select(NarrativeRuntimeContentAdapter.ToSpec).ToArray());
        return new StoryDirectorService(
            initialProgress,
            _storyEvents.Select(NarrativeRuntimeContentAdapter.ToSpec).ToArray(),
            assemblyService);
    }
}
