using System;
using System.Collections.Generic;
using SM.Core;

namespace SM.Meta;

public sealed class StoryArchiveReplayAssembler
{
    private readonly DialogueAssemblyService _assemblyService;

    public StoryArchiveReplayAssembler(DialogueAssemblyService assemblyService)
    {
        _assemblyService = assemblyService ?? throw new ArgumentNullException(nameof(assemblyService));
    }

    public IReadOnlyList<StoryPresentationRequest> BuildReplayBatch(StoryArchiveEntryRecord entry)
    {
        if (entry.ReplayPolicy == StoryArchiveReplayPolicy.Skip)
        {
            return Array.Empty<StoryPresentationRequest>();
        }

        if (entry.PresentationKind == StoryPresentationKind.DialogueOverlay ||
            entry.PresentationKind == StoryPresentationKind.DialogueScene)
        {
            var request = _assemblyService.Assemble(
                entry.PresentationKind,
                entry.PresentationKey,
                0,
                NarrativeProgressRecord.Empty);

            return new[] { request };
        }

        var directRequest = new StoryPresentationRequest
        {
            PresentationKind = entry.PresentationKind,
            PresentationKey = entry.PresentationKey,
            Priority = 0,
            SpeakerIds = Array.Empty<string>(),
        };

        return new[] { directRequest };
    }
}
