using System;
using System.Collections.Generic;
using System.Linq;
using SM.Core;

namespace SM.Meta;

public sealed class StoryDirectorService
{
    private readonly DialogueAssemblyService _dialogueAssemblyService;
    private readonly Dictionary<NarrativeMoment, StoryEventSpec[]> _eventsByMoment;
    private readonly Dictionary<string, StoryEventStateRecord> _eventStates;
    private long _evaluationTick;

    public NarrativeProgressRecord Progress { get; private set; }

    public StoryDirectorService(
        NarrativeProgressRecord? initialProgress,
        IReadOnlyList<StoryEventSpec> storyEvents,
        DialogueAssemblyService dialogueAssemblyService)
    {
        _dialogueAssemblyService = dialogueAssemblyService ?? throw new ArgumentNullException(nameof(dialogueAssemblyService));
        _eventsByMoment = BucketEvents(storyEvents);
        _eventStates = new Dictionary<string, StoryEventStateRecord>(StringComparer.Ordinal);
        Progress = NarrativeProgressRecord.Normalize(initialProgress);
    }

    public void Advance(NarrativeMoment moment, StoryMomentContext context)
    {
        var requestedContext = context ?? StoryMomentContext.Empty;
        var nextChapterId = string.IsNullOrWhiteSpace(requestedContext.ChapterId)
            ? Progress.CurrentChapterId
            : requestedContext.ChapterId;
        var nextSiteId = string.IsNullOrWhiteSpace(requestedContext.SiteId)
            ? Progress.CurrentSiteId
            : requestedContext.SiteId;
        var effectiveContext = requestedContext with
        {
            ChapterId = nextChapterId,
            SiteId = nextSiteId,
        };

        var seenIds = new HashSet<string>(Progress.SeenEventIds, StringComparer.Ordinal);
        var resolvedIds = new HashSet<string>(Progress.ResolvedEventIds, StringComparer.Ordinal);
        var storyFlags = new HashSet<string>(Progress.StoryFlags, StringComparer.Ordinal);
        var unlockedHeroIds = new HashSet<string>(Progress.UnlockedStoryHeroIds, StringComparer.Ordinal);
        var pending = new List<StoryPresentationRequest>(Progress.PendingPresentations);

        var workingProgress = Progress with
        {
            CurrentChapterId = nextChapterId,
            CurrentSiteId = nextSiteId,
        };

        if (!_eventsByMoment.TryGetValue(moment, out var bucket))
        {
            Progress = workingProgress;
            return;
        }

        foreach (var definition in bucket)
        {
            if (definition == null)
            {
                continue;
            }

            var eventId = GetStableEventId(definition);
            if (string.IsNullOrWhiteSpace(eventId))
            {
                throw new InvalidOperationException("Story event stable id is missing.");
            }

            if (IsBlockedByOncePolicy(eventId, definition.OncePolicy, seenIds, resolvedIds))
            {
                continue;
            }

            var conditionsPass = true;
            foreach (var condition in definition.Conditions ?? Array.Empty<StoryConditionSpec>())
            {
                if (condition == null)
                {
                    throw new InvalidOperationException($"Story event '{eventId}' references a missing condition.");
                }

                if (!EvaluateCondition(condition, effectiveContext, storyFlags, unlockedHeroIds))
                {
                    conditionsPass = false;
                    break;
                }
            }

            if (!conditionsPass)
            {
                continue;
            }

            seenIds.Add(eventId);
            if (definition.OncePolicy != StoryOncePolicy.Repeatable)
            {
                resolvedIds.Add(eventId);
            }

            var priorState = _eventStates.TryGetValue(eventId, out var state)
                ? state
                : default;

            foreach (var effect in definition.Effects ?? Array.Empty<StoryEffectSpec>())
            {
                if (effect == null)
                {
                    throw new InvalidOperationException($"Story event '{eventId}' references a missing effect.");
                }

                switch (effect.Kind)
                {
                    case StoryEffectKind.UnlockHero:
                        if (string.IsNullOrWhiteSpace(effect.Payload))
                        {
                            throw new InvalidOperationException($"Story event '{eventId}' has an UnlockHero effect without payload.");
                        }

                        unlockedHeroIds.Add(effect.Payload);
                        break;

                    case StoryEffectKind.SetFlag:
                        if (string.IsNullOrWhiteSpace(effect.Payload))
                        {
                            throw new InvalidOperationException($"Story event '{eventId}' has a SetFlag effect without payload.");
                        }

                        storyFlags.Add(effect.Payload);
                        break;

                    case StoryEffectKind.ClearFlag:
                        if (string.IsNullOrWhiteSpace(effect.Payload))
                        {
                            throw new InvalidOperationException($"Story event '{eventId}' has a ClearFlag effect without payload.");
                        }

                        storyFlags.Remove(effect.Payload);
                        break;

                    case StoryEffectKind.EnqueuePresentation:
                    {
                        var snapshot = workingProgress with
                        {
                            SeenEventIds = ToSortedArray(seenIds),
                            ResolvedEventIds = ToSortedArray(resolvedIds),
                            StoryFlags = ToSortedArray(storyFlags),
                            UnlockedStoryHeroIds = ToSortedArray(unlockedHeroIds),
                            PendingPresentations = pending.ToArray(),
                        };
                        pending.Add(BuildPresentationRequest(definition, effect, snapshot));
                        break;
                    }

                    case StoryEffectKind.UnlockMode:
                        if (string.IsNullOrWhiteSpace(effect.Payload))
                        {
                            throw new InvalidOperationException($"Story event '{eventId}' has an UnlockMode effect without payload.");
                        }

                        storyFlags.Add($"mode:{effect.Payload}");
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported story effect kind '{effect.Kind}'.");
                }
            }

            _evaluationTick += 1;
            _eventStates[eventId] = new StoryEventStateRecord(
                priorState.SeenCount + 1,
                _evaluationTick,
                definition.OncePolicy != StoryOncePolicy.Repeatable);

            workingProgress = workingProgress with
            {
                SeenEventIds = ToSortedArray(seenIds),
                ResolvedEventIds = ToSortedArray(resolvedIds),
                StoryFlags = ToSortedArray(storyFlags),
                UnlockedStoryHeroIds = ToSortedArray(unlockedHeroIds),
                PendingPresentations = pending.ToArray(),
            };
        }

        Progress = workingProgress;
    }

    public bool TryDequeuePendingPresentation(out StoryPresentationRequest? request)
    {
        if (Progress.PendingPresentations.Length == 0)
        {
            request = null;
            return false;
        }

        request = Progress.PendingPresentations[0];
        Progress = Progress with
        {
            PendingPresentations = Progress.PendingPresentations.Skip(1).ToArray(),
        };
        return true;
    }

    public void ResetRunScopedProgress()
    {
        Progress = Progress with
        {
            ResolvedEventIds = Array.Empty<string>(),
            PendingPresentations = Array.Empty<StoryPresentationRequest>(),
        };

        foreach (var eventId in _eventStates.Keys.ToArray())
        {
            _eventStates[eventId] = _eventStates[eventId] with { Resolved = false };
        }
    }

    private static bool IsBlockedByOncePolicy(
        string eventId,
        StoryOncePolicy oncePolicy,
        HashSet<string> seenIds,
        HashSet<string> resolvedIds)
    {
        return oncePolicy switch
        {
            StoryOncePolicy.OncePerProfile => seenIds.Contains(eventId),
            StoryOncePolicy.OncePerRun => resolvedIds.Contains(eventId),
            StoryOncePolicy.Repeatable => false,
            _ => throw new InvalidOperationException($"Unsupported once policy '{oncePolicy}'."),
        };
    }

    private static bool EvaluateCondition(
        StoryConditionSpec definition,
        StoryMomentContext context,
        HashSet<string> storyFlags,
        HashSet<string> unlockedHeroIds)
    {
        var operandA = definition.OperandA;
        if (string.IsNullOrWhiteSpace(operandA))
        {
            throw new InvalidOperationException($"Story condition '{definition.Id}' is missing operandA.");
        }

        return definition.Kind switch
        {
            StoryConditionKind.ChapterIs => string.Equals(context.ChapterId, operandA, StringComparison.Ordinal),
            StoryConditionKind.SiteIs => string.Equals(context.SiteId, operandA, StringComparison.Ordinal),
            StoryConditionKind.NodeIs => context.NodeIndex == ParseNodeIndex(operandA),
            StoryConditionKind.FlagSet => storyFlags.Contains(operandA),
            StoryConditionKind.FlagNotSet => !storyFlags.Contains(operandA),
            StoryConditionKind.HeroUnlocked => unlockedHeroIds.Contains(operandA),
            StoryConditionKind.HeroNotUnlocked => !unlockedHeroIds.Contains(operandA),
            _ => throw new InvalidOperationException($"Unsupported story condition kind '{definition.Kind}'."),
        };
    }

    private StoryPresentationRequest BuildPresentationRequest(
        StoryEventSpec definition,
        StoryEffectSpec effect,
        NarrativeProgressRecord workingProgress)
    {
        if (string.IsNullOrWhiteSpace(effect.Payload))
        {
            throw new InvalidOperationException($"Story event '{definition.Id}' has an EnqueuePresentation effect without payload.");
        }

        if (string.IsNullOrWhiteSpace(definition.PresentationKey))
        {
            throw new InvalidOperationException($"Story event '{definition.Id}' is missing PresentationKey.");
        }

        return _dialogueAssemblyService.Assemble(
            ParsePresentationKind(effect.Payload),
            definition.PresentationKey,
            definition.Priority,
            workingProgress);
    }

    private static StoryPresentationKind ParsePresentationKind(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)
            || !Enum.TryParse(payload, false, out StoryPresentationKind kind))
        {
            throw new InvalidOperationException($"Unsupported story presentation payload '{payload}'.");
        }

        return kind;
    }

    private static string[] ToSortedArray(HashSet<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetStableEventId(StoryEventSpec definition)
    {
        return definition.Id;
    }

    private static int ParseNodeIndex(string value)
    {
        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"Story condition node index '{value}' is invalid.");
        }

        return parsed;
    }

    private static Dictionary<NarrativeMoment, StoryEventSpec[]> BucketEvents(IReadOnlyList<StoryEventSpec> storyEvents)
    {
        var buckets = new Dictionary<NarrativeMoment, List<StoryEventSpec>>();
        foreach (var definition in storyEvents ?? Array.Empty<StoryEventSpec>())
        {
            if (definition == null)
            {
                continue;
            }

            if (!buckets.TryGetValue(definition.Moment, out var bucket))
            {
                bucket = new List<StoryEventSpec>();
                buckets[definition.Moment] = bucket;
            }

            bucket.Add(definition);
        }

        return buckets.ToDictionary(
            pair => pair.Key,
            pair => pair.Value
                .OrderByDescending(definition => definition.Priority)
                .ThenBy(GetStableEventId, StringComparer.Ordinal)
                .ToArray());
    }
}
