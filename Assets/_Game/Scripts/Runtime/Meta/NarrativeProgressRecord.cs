using System;
using System.Collections.Generic;
using System.Linq;

namespace SM.Meta;

public sealed record NarrativeProgressRecord
{
    public static NarrativeProgressRecord Empty { get; } = new();

    public string CurrentChapterId { get; init; } = string.Empty;
    public string CurrentSiteId { get; init; } = string.Empty;
    public string[] SeenEventIds { get; init; } = Array.Empty<string>();
    public string[] ResolvedEventIds { get; init; } = Array.Empty<string>();
    public string[] StoryFlags { get; init; } = Array.Empty<string>();
    public string[] UnlockedStoryHeroIds { get; init; } = Array.Empty<string>();
    public RecruitConversionStateRecord RecruitConversions { get; init; } = RecruitConversionStateRecord.Empty;
    public StoryPresentationRequest[] PendingPresentations { get; init; } = Array.Empty<StoryPresentationRequest>();
    public EndlessCycleStateRecord EndlessCycle { get; init; } = EndlessCycleStateRecord.Empty;

    public static NarrativeProgressRecord Normalize(NarrativeProgressRecord? narrative)
    {
        if (narrative is null)
        {
            return Empty;
        }

        var conversions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in narrative.RecruitConversions?.Conversions ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            conversions[pair.Key] = pair.Value;
        }

        var modifiers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in narrative.EndlessCycle?.Modifiers ?? new Dictionary<string, int>())
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            modifiers[pair.Key] = pair.Value;
        }

        return new NarrativeProgressRecord
        {
            CurrentChapterId = narrative.CurrentChapterId ?? string.Empty,
            CurrentSiteId = narrative.CurrentSiteId ?? string.Empty,
            SeenEventIds = NormalizeSortedSet(narrative.SeenEventIds),
            ResolvedEventIds = NormalizeSortedSet(narrative.ResolvedEventIds),
            StoryFlags = NormalizeSortedSet(narrative.StoryFlags),
            UnlockedStoryHeroIds = NormalizeSortedSet(narrative.UnlockedStoryHeroIds),
            RecruitConversions = new RecruitConversionStateRecord
            {
                Conversions = conversions,
            },
            PendingPresentations = NormalizePendingPresentations(narrative.PendingPresentations),
            EndlessCycle = new EndlessCycleStateRecord
            {
                CycleIndex = narrative.EndlessCycle?.CycleIndex ?? 0,
                Heat = narrative.EndlessCycle?.Heat ?? 0,
                Modifiers = modifiers,
            },
        };
    }

    private static string[] NormalizeSortedSet(string[]? values)
    {
        if (values is not { Length: > 0 })
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static StoryPresentationRequest[] NormalizePendingPresentations(StoryPresentationRequest[]? requests)
    {
        if (requests is not { Length: > 0 })
        {
            return Array.Empty<StoryPresentationRequest>();
        }

        var normalized = new List<StoryPresentationRequest>(requests.Length);
        foreach (var request in requests)
        {
            if (request is null)
            {
                continue;
            }

            normalized.Add(new StoryPresentationRequest
            {
                PresentationKind = request.PresentationKind,
                PresentationKey = request.PresentationKey ?? string.Empty,
                Priority = request.Priority,
                SpeakerIds = NormalizeSpeakerIds(request.SpeakerIds),
            });
        }

        return normalized.ToArray();
    }

    private static string[] NormalizeSpeakerIds(string[]? speakerIds)
    {
        if (speakerIds is not { Length: > 0 })
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var normalized = new List<string>(speakerIds.Length);
        foreach (var speakerId in speakerIds)
        {
            if (string.IsNullOrWhiteSpace(speakerId) || !seen.Add(speakerId))
            {
                continue;
            }

            normalized.Add(speakerId);
        }

        return normalized.ToArray();
    }
}
