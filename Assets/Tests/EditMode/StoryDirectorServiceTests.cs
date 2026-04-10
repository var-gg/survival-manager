using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SM.Content;
using SM.Core;
using SM.Meta;
using UnityEngine;

namespace SM.Tests.EditMode;

[Category("FastUnit")]
public sealed class StoryDirectorServiceTests
{
    [Test]
    public void Advance_OncePerProfileEvent_FiresOnlyOnce_AndBuildsDialogueRequest()
    {
        var sequence = CreateDialogueSequence(
            "dlg.town.intro",
            CreateDialogueLine("line_1", "guide", "loc.guide"),
            CreateDialogueLine("line_2", "hero", "loc.hero"),
            CreateDialogueLine("line_3", "guide", "loc.guide.repeat"));
        var storyEvent = CreateStoryEvent(
            "evt.town.intro",
            NarrativeMoment.TownEntered,
            100,
            StoryOncePolicy.OncePerProfile,
            Array.Empty<StoryConditionDefinition>(),
            new[]
            {
                CreateStoryEffect("fx.set.intro", StoryEffectKind.SetFlag, "story:intro"),
                CreateStoryEffect("fx.present.intro", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueOverlay)),
            },
            "dlg.town.intro");
        var director = CreateStoryDirector(new[] { storyEvent }, new[] { sequence });

        director.Advance(NarrativeMoment.TownEntered, StoryMomentContext.Empty);

        Assert.That(director.Progress.SeenEventIds, Is.EqualTo(new[] { "evt.town.intro" }));
        Assert.That(director.Progress.ResolvedEventIds, Is.EqualTo(new[] { "evt.town.intro" }));
        Assert.That(director.Progress.StoryFlags, Is.EqualTo(new[] { "story:intro" }));
        Assert.That(director.Progress.PendingPresentations, Has.Length.EqualTo(1));
        Assert.That(director.Progress.PendingPresentations[0].PresentationKind, Is.EqualTo(StoryPresentationKind.DialogueOverlay));
        Assert.That(director.Progress.PendingPresentations[0].PresentationKey, Is.EqualTo("dlg.town.intro"));
        Assert.That(director.Progress.PendingPresentations[0].SpeakerIds, Is.EqualTo(new[] { "guide", "hero" }));

        director.Advance(NarrativeMoment.TownEntered, StoryMomentContext.Empty);

        Assert.That(director.Progress.PendingPresentations, Has.Length.EqualTo(1));
        Assert.That(director.Progress.SeenEventIds, Is.EqualTo(new[] { "evt.town.intro" }));
        Assert.That(director.Progress.StoryFlags, Is.EqualTo(new[] { "story:intro" }));
    }

    [Test]
    public void Advance_EvaluatesHigherPriorityFirst_AndLowerPriorityEventSeesSamePassFlag()
    {
        var seedEvent = CreateStoryEvent(
            "evt.seed.flag",
            NarrativeMoment.RewardCommitted,
            100,
            StoryOncePolicy.Repeatable,
            Array.Empty<StoryConditionDefinition>(),
            new[]
            {
                CreateStoryEffect("fx.set.branch", StoryEffectKind.SetFlag, "branch:open"),
                CreateStoryEffect("fx.present.toast", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.ToastBanner)),
            },
            "toast.seed");
        var followupEvent = CreateStoryEvent(
            "evt.branch.followup",
            NarrativeMoment.RewardCommitted,
            10,
            StoryOncePolicy.Repeatable,
            new[]
            {
                CreateStoryCondition("cond.branch.open", StoryConditionKind.FlagSet, "branch:open"),
            },
            new[]
            {
                CreateStoryEffect("fx.present.card", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.StoryCard)),
            },
            "card.branch.followup");
        var director = CreateStoryDirector(new[] { seedEvent, followupEvent }, Array.Empty<DialogueSequenceDefinition>());

        director.Advance(NarrativeMoment.RewardCommitted, StoryMomentContext.Empty);

        Assert.That(director.Progress.StoryFlags, Is.EqualTo(new[] { "branch:open" }));
        Assert.That(director.Progress.PendingPresentations, Has.Length.EqualTo(2));
        Assert.That(director.Progress.PendingPresentations[0].PresentationKey, Is.EqualTo("toast.seed"));
        Assert.That(director.Progress.PendingPresentations[1].PresentationKey, Is.EqualTo("card.branch.followup"));
    }

    [Test]
    public void ResetRunScopedProgress_ReenablesOncePerRun_ButKeepsOncePerProfileLocked()
    {
        var runSequence = CreateDialogueSequence(
            "dlg.run.only",
            CreateDialogueLine("line_run", "guide", "loc.run"));
        var profileSequence = CreateDialogueSequence(
            "dlg.profile.only",
            CreateDialogueLine("line_profile", "hero", "loc.profile"));
        var oncePerRun = CreateStoryEvent(
            "evt.run.only",
            NarrativeMoment.SiteEntered,
            50,
            StoryOncePolicy.OncePerRun,
            Array.Empty<StoryConditionDefinition>(),
            new[]
            {
                CreateStoryEffect("fx.present.run", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueOverlay)),
            },
            "dlg.run.only");
        var oncePerProfile = CreateStoryEvent(
            "evt.profile.only",
            NarrativeMoment.SiteEntered,
            40,
            StoryOncePolicy.OncePerProfile,
            Array.Empty<StoryConditionDefinition>(),
            new[]
            {
                CreateStoryEffect("fx.present.profile", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueOverlay)),
            },
            "dlg.profile.only");
        var director = CreateStoryDirector(
            new[] { oncePerRun, oncePerProfile },
            new[] { runSequence, profileSequence });

        director.Advance(NarrativeMoment.SiteEntered, StoryMomentContext.Empty);

        Assert.That(director.Progress.PendingPresentations.Select(request => request.PresentationKey).ToArray(),
            Is.EqualTo(new[] { "dlg.run.only", "dlg.profile.only" }));

        while (director.TryDequeuePendingPresentation(out _))
        {
        }

        director.Advance(NarrativeMoment.SiteEntered, StoryMomentContext.Empty);

        Assert.That(director.Progress.PendingPresentations, Is.Empty);

        director.ResetRunScopedProgress();

        Assert.That(director.Progress.ResolvedEventIds, Is.Empty);
        Assert.That(director.Progress.SeenEventIds, Is.EqualTo(new[] { "evt.profile.only", "evt.run.only" }));

        director.Advance(NarrativeMoment.SiteEntered, StoryMomentContext.Empty);

        Assert.That(director.Progress.PendingPresentations, Has.Length.EqualTo(1));
        Assert.That(director.Progress.PendingPresentations[0].PresentationKey, Is.EqualTo("dlg.run.only"));
        Assert.That(director.Progress.ResolvedEventIds, Is.EqualTo(new[] { "evt.run.only" }));
        Assert.That(director.Progress.SeenEventIds, Is.EqualTo(new[] { "evt.profile.only", "evt.run.only" }));
    }

    private static StoryDirectorService CreateStoryDirector(
        StoryEventDefinition[] storyEvents,
        DialogueSequenceDefinition[] dialogueSequences)
    {
        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            storyEvents,
            new DialogueAssemblyService(dialogueSequences, Array.Empty<HeroLoreDefinition>()));
    }

    private static StoryEventDefinition CreateStoryEvent(
        string id,
        NarrativeMoment moment,
        int priority,
        StoryOncePolicy oncePolicy,
        StoryConditionDefinition[] conditions,
        StoryEffectDefinition[] effects,
        string presentationKey)
    {
        return CreateAsset<StoryEventDefinition>(
            ("_id", id),
            ("_moment", moment),
            ("_priority", priority),
            ("_oncePolicy", oncePolicy),
            ("_conditions", conditions),
            ("_effects", effects),
            ("_presentationKey", presentationKey));
    }

    private static StoryConditionDefinition CreateStoryCondition(string id, StoryConditionKind kind, string operandA)
    {
        return CreateAsset<StoryConditionDefinition>(
            ("_id", id),
            ("_kind", kind),
            ("_operandA", operandA),
            ("_operandB", string.Empty));
    }

    private static StoryEffectDefinition CreateStoryEffect(string id, StoryEffectKind kind, string payload)
    {
        return CreateAsset<StoryEffectDefinition>(
            ("_id", id),
            ("_kind", kind),
            ("_payload", payload));
    }

    private static DialogueSequenceDefinition CreateDialogueSequence(string id, params DialogueLineDefinition[] lines)
    {
        return CreateAsset<DialogueSequenceDefinition>(
            ("_id", id),
            ("_lines", lines));
    }

    private static DialogueLineDefinition CreateDialogueLine(string id, string speakerId, string textKey)
    {
        return CreateAsset<DialogueLineDefinition>(
            ("_id", id),
            ("_speakerId", speakerId),
            ("_textKey", textKey),
            ("_emote", string.Empty),
            ("_autoAdvanceHint", 0f));
    }

    private static T CreateAsset<T>(params (string FieldName, object Value)[] assignments) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        foreach (var assignment in assignments)
        {
            SetField(asset, assignment.FieldName, assignment.Value);
        }

        return asset;
    }

    private static void SetField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' was not found on '{target.GetType().Name}'.");
        }

        field.SetValue(target, value);
    }
}
