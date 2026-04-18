using System;
using System.Collections.Generic;
using SM.Core;

namespace SM.Meta;

public sealed class DialogueAssemblyService
{
    private readonly Dictionary<string, DialogueSequenceSpec> _dialogueSequencesById;
    private readonly Dictionary<string, HeroLoreSpec> _heroLoreById;

    public DialogueAssemblyService(
        IReadOnlyList<DialogueSequenceSpec> dialogueSequences,
        IReadOnlyList<HeroLoreSpec> heroLoreDefinitions)
    {
        _dialogueSequencesById = BuildDialogueIndex(dialogueSequences);
        _heroLoreById = BuildHeroLoreIndex(heroLoreDefinitions);
    }

    public StoryPresentationRequest Assemble(
        StoryPresentationKind presentationKind,
        string presentationKey,
        int priority,
        NarrativeProgressRecord progress)
    {
        _ = progress;

        return presentationKind switch
        {
            StoryPresentationKind.DialogueOverlay => BuildDialogueRequest(presentationKind, presentationKey, priority),
            StoryPresentationKind.DialogueScene => BuildDialogueRequest(presentationKind, presentationKey, priority),
            StoryPresentationKind.StoryCard => BuildStoryCardRequest(presentationKey, priority),
            StoryPresentationKind.ToastBanner => new StoryPresentationRequest
            {
                PresentationKind = StoryPresentationKind.ToastBanner,
                PresentationKey = presentationKey,
                Priority = priority,
                SpeakerIds = Array.Empty<string>(),
            },
            _ => throw new InvalidOperationException($"Unsupported story presentation kind '{presentationKind}'."),
        };
    }

    private StoryPresentationRequest BuildDialogueRequest(
        StoryPresentationKind presentationKind,
        string presentationKey,
        int priority)
    {
        var sequenceId = NarrativePresentationKeyNormalizer.ToDialogueSequenceId(presentationKey);
        if (!_dialogueSequencesById.TryGetValue(sequenceId, out var sequence))
        {
            throw new InvalidOperationException($"Dialogue sequence '{sequenceId}' (from presentation key '{presentationKey}') was not found.");
        }

        var speakers = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in sequence.Lines ?? Array.Empty<DialogueLineSpec>())
        {
            if (line == null || string.IsNullOrWhiteSpace(line.SpeakerId) || !seen.Add(line.SpeakerId))
            {
                continue;
            }

            speakers.Add(line.SpeakerId);
        }

        return new StoryPresentationRequest
        {
            PresentationKind = presentationKind,
            PresentationKey = presentationKey,
            Priority = priority,
            SpeakerIds = speakers.ToArray(),
        };
    }

    private StoryPresentationRequest BuildStoryCardRequest(string presentationKey, int priority)
    {
        var speakerIds = Array.Empty<string>();
        if (_heroLoreById.TryGetValue(presentationKey, out var heroLore)
            && !string.IsNullOrWhiteSpace(heroLore.HeroId))
        {
            speakerIds = new[] { heroLore.HeroId };
        }

        return new StoryPresentationRequest
        {
            PresentationKind = StoryPresentationKind.StoryCard,
            PresentationKey = presentationKey,
            Priority = priority,
            SpeakerIds = speakerIds,
        };
    }

    private static Dictionary<string, DialogueSequenceSpec> BuildDialogueIndex(IReadOnlyList<DialogueSequenceSpec> dialogueSequences)
    {
        var index = new Dictionary<string, DialogueSequenceSpec>(StringComparer.Ordinal);
        foreach (var sequence in dialogueSequences ?? Array.Empty<DialogueSequenceSpec>())
        {
            if (sequence == null)
            {
                continue;
            }

            var stableId = sequence.Id;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new InvalidOperationException("Dialogue sequence stable id is missing.");
            }

            if (!index.TryAdd(stableId, sequence))
            {
                throw new InvalidOperationException($"Duplicate dialogue sequence stable id '{stableId}'.");
            }
        }

        return index;
    }

    private static Dictionary<string, HeroLoreSpec> BuildHeroLoreIndex(IReadOnlyList<HeroLoreSpec> heroLoreDefinitions)
    {
        var index = new Dictionary<string, HeroLoreSpec>(StringComparer.Ordinal);
        foreach (var heroLore in heroLoreDefinitions ?? Array.Empty<HeroLoreSpec>())
        {
            if (heroLore == null)
            {
                continue;
            }

            var stableId = heroLore.Id;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new InvalidOperationException("Hero lore stable id is missing.");
            }

            if (!index.TryAdd(stableId, heroLore))
            {
                throw new InvalidOperationException($"Duplicate hero lore stable id '{stableId}'.");
            }
        }

        return index;
    }
}
