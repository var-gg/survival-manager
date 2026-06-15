using System;
using NUnit.Framework;
using SM.Core;
using SM.Meta;
using SM.Tests.EditMode.Fakes;

namespace SM.Tests.EditMode;

/// <summary>
/// narrative 연출 drain seam — INarrativePresentationSink(RecordingNarrativeSink)로 큐를 씬·입력 없이
/// 자기완결 drain함을 검증한다. 캡쳐 하네스가 인트로 컷씬에서 무한대기하던 근본 원인의 headless 등가 해소:
/// 즉시-ack sink면 advance→dequeue→present 루프가 입력 대기 없이 종료한다.
///
/// StoryDirectorService(순수 SM.Meta)를 직접 구동 — bridge(MonoBehaviour)의 펌프 로직을 순수 등가로 모사.
/// </summary>
[Category("FastUnit")]
public sealed class NarrativeDrainSeamFastTests
{
    [Test]
    public void Drain_OnEmptyDirector_CompletesWithoutPresentingOrBlocking()
    {
        var director = CreateDirector(Array.Empty<StoryEventSpec>(), Array.Empty<DialogueSequenceSpec>());
        var sink = new RecordingNarrativeSink();

        Drain(director, NarrativeMoment.TownEntered, StoryMomentContext.Empty, sink);

        Assert.That(sink.Presented, Is.Empty, "seed 없는 director는 어떤 연출도 dispatch하지 않는다.");
        Assert.That(director.Progress.PendingPresentations, Is.Empty, "빈 큐 drain은 무한대기 없이 종료.");
    }

    [Test]
    public void Drain_OnSeededMoment_PresentsEachRequest_AcksAndEmptiesQueue()
    {
        var sequence = new DialogueSequenceSpec(
            "dlg.intro",
            new[] { new DialogueLineSpec("line_1", "guide", "loc.guide", string.Empty, string.Empty, 0f) });
        var storyEvent = new StoryEventSpec(
            "evt.intro",
            NarrativeMoment.TownEntered,
            100,
            StoryOncePolicy.OncePerProfile,
            Array.Empty<StoryConditionSpec>(),
            new[]
            {
                new StoryEffectSpec("fx.present", StoryEffectKind.EnqueuePresentation, nameof(StoryPresentationKind.DialogueOverlay)),
            },
            "dlg.intro");
        var director = CreateDirector(new[] { storyEvent }, new[] { sequence });
        var sink = new RecordingNarrativeSink();

        Drain(director, NarrativeMoment.TownEntered, StoryMomentContext.Empty, sink);

        Assert.That(sink.Presented, Has.Count.EqualTo(1), "enqueue된 연출 1건이 sink로 dispatch된다.");
        Assert.That(sink.Last!.PresentationKey, Is.EqualTo("dlg.intro"));
        Assert.That(sink.AckCount, Is.EqualTo(1), "각 연출이 ack되어(입력 대기 0) 펌프가 진행된다.");
        Assert.That(director.Progress.PendingPresentations, Is.Empty, "drain이 큐를 비웠다.");
    }

    // bridge.TryPumpQueue + sink.Present의 순수 등가 — advance 후 큐를 sink로 흘려보낸다.
    private static void Drain(StoryDirectorService director, NarrativeMoment moment, StoryMomentContext context, RecordingNarrativeSink sink)
    {
        director.Advance(moment, context);
        while (director.TryDequeuePendingPresentation(out var request) && request != null)
        {
            sink.Present(request, () => { });
        }
    }

    private static StoryDirectorService CreateDirector(StoryEventSpec[] storyEvents, DialogueSequenceSpec[] dialogueSequences)
    {
        return new StoryDirectorService(
            NarrativeProgressRecord.Empty,
            storyEvents,
            new DialogueAssemblyService(dialogueSequences, Array.Empty<HeroLoreSpec>()));
    }
}
