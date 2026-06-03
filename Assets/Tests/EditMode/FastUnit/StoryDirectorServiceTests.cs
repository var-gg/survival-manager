using System;
using System.Linq;
using NUnit.Framework;
using SM.Core;
using SM.Meta;

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
            Array.Empty<StoryConditionSpec>(),
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
    public void Advance_DialogueScenePresentation_UsesExactAuthoredSequenceIdBeforeLegacyNormalizer()
    {
        var sequence = CreateDialogueSequence(
            "dialogue_scene_ashen_gate_intro",
            CreateDialogueLine("line_1", "guide", "loc.guide"));
        var storyEvent = CreateStoryEvent(
            "evt.site.intro",
            NarrativeMoment.SiteEntered,
            100,
            StoryOncePolicy.OncePerProfile,
            Array.Empty<StoryConditionSpec>(),
            new[]
            {
                CreateStoryEffect("fx.present.site", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueScene)),
            },
            "dialogue_scene_ashen_gate_intro");
        var director = CreateStoryDirector(new[] { storyEvent }, new[] { sequence });

        director.Advance(NarrativeMoment.SiteEntered, StoryMomentContext.Empty);

        Assert.That(director.Progress.PendingPresentations, Has.Length.EqualTo(1));
        Assert.That(director.Progress.PendingPresentations[0].PresentationKey, Is.EqualTo("dialogue_scene_ashen_gate_intro"));
        Assert.That(director.Progress.PendingPresentations[0].SpeakerIds, Is.EqualTo(new[] { "guide" }));
    }

    [Test]
    public void Advance_EvaluatesHigherPriorityFirst_AndLowerPriorityEventSeesSamePassFlag()
    {
        var seedEvent = CreateStoryEvent(
            "evt.seed.flag",
            NarrativeMoment.RewardCommitted,
            100,
            StoryOncePolicy.Repeatable,
            Array.Empty<StoryConditionSpec>(),
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
        var director = CreateStoryDirector(new[] { seedEvent, followupEvent }, Array.Empty<DialogueSequenceSpec>());

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
            Array.Empty<StoryConditionSpec>(),
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
            Array.Empty<StoryConditionSpec>(),
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

    [Test]
    public void SetFlag_AddsFlagToProgress_ForBattleOutcomeStamp()
    {
        var director = CreateStoryDirector(Array.Empty<StoryEventSpec>(), Array.Empty<DialogueSequenceSpec>());

        director.SetFlag("story_flag_chapter_glass_forest_squad_costly");

        Assert.That(director.Progress.StoryFlags, Does.Contain("story_flag_chapter_glass_forest_squad_costly"));
    }

    [Test]
    public void SetFlag_IsIdempotent_AndIgnoresBlank()
    {
        var director = CreateStoryDirector(Array.Empty<StoryEventSpec>(), Array.Empty<DialogueSequenceSpec>());

        director.SetFlag("story_flag_squad_costly");
        director.SetFlag("story_flag_squad_costly");
        director.SetFlag("");
        director.SetFlag("   ");

        Assert.That(director.Progress.StoryFlags, Is.EqualTo(new[] { "story_flag_squad_costly" }));
    }

    [Test]
    public void SetFlag_FlagIsReadableByFlagSetCondition_OnSubsequentAdvance()
    {
        // P1b 계약: settlement이 stamp한 outcome flag를 이후 Advance의 FlagSet condition이 읽는다.
        var gatedEvent = CreateStoryEvent(
            "evt.town.squad_loss",
            NarrativeMoment.TownEntered,
            160,
            StoryOncePolicy.OncePerProfile,
            new[]
            {
                CreateStoryCondition("cond.squad.costly", StoryConditionKind.FlagSet, "story_flag_chapter_glass_forest_squad_costly"),
            },
            new[]
            {
                CreateStoryEffect("fx.present.loss", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueOverlay)),
            },
            "dialogue_overlay_ch4_squad_loss");
        var sequence = CreateDialogueSequence(
            "dialogue_overlay_ch4_squad_loss",
            CreateDialogueLine("line_1", "danlin", "loc.danlin.loss"));
        var director = CreateStoryDirector(new[] { gatedEvent }, new[] { sequence });

        // flag 미설정 — 게이트 닫힘.
        director.Advance(NarrativeMoment.TownEntered, StoryMomentContext.Empty);
        Assert.That(director.Progress.PendingPresentations, Is.Empty);

        // settlement stamp 시뮬레이션 후 — 게이트 열림.
        director.SetFlag("story_flag_chapter_glass_forest_squad_costly");
        director.Advance(NarrativeMoment.TownEntered, StoryMomentContext.Empty);
        Assert.That(director.Progress.PendingPresentations, Has.Length.EqualTo(1));
        Assert.That(director.Progress.PendingPresentations[0].PresentationKey, Is.EqualTo("dialogue_overlay_ch4_squad_loss"));
    }

    private static StoryDirectorService CreateStoryDirector(
        StoryEventSpec[] storyEvents,
        DialogueSequenceSpec[] dialogueSequences)
    {
        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            storyEvents,
            new DialogueAssemblyService(dialogueSequences, Array.Empty<HeroLoreSpec>()));
    }

    private static StoryEventSpec CreateStoryEvent(
        string id,
        NarrativeMoment moment,
        int priority,
        StoryOncePolicy oncePolicy,
        StoryConditionSpec[] conditions,
        StoryEffectSpec[] effects,
        string presentationKey)
    {
        return new StoryEventSpec(id, moment, priority, oncePolicy, conditions, effects, presentationKey);
    }

    private static StoryConditionSpec CreateStoryCondition(string id, StoryConditionKind kind, string operandA)
    {
        return new StoryConditionSpec(id, kind, operandA, string.Empty);
    }

    private static StoryEffectSpec CreateStoryEffect(string id, StoryEffectKind kind, string payload)
    {
        return new StoryEffectSpec(id, kind, payload);
    }

    private static DialogueSequenceSpec CreateDialogueSequence(string id, params DialogueLineSpec[] lines)
    {
        return new DialogueSequenceSpec(id, lines);
    }

    private static DialogueLineSpec CreateDialogueLine(string id, string speakerId, string textKey)
    {
        return new DialogueLineSpec(id, speakerId, textKey, string.Empty, string.Empty, 0f);
    }
}
