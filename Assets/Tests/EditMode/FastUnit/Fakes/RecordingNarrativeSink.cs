using System;
using System.Collections.Generic;
using SM.Meta;
using SM.Unity.Narrative;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// headless 테스트용 INarrativePresentationSink — VisualElement 연출 대신 dispatch된 request를 순서대로
/// 기록하고 onPresented를 즉시(동기) 호출해 ack 체인을 진행시킨다. 입력 대기 0 → 골든이 narrative 게이트를
/// 무한대기 없이 통과(캡쳐 하네스가 인트로 컷씬에서 멈췄던 근본 원인의 headless 등가 해소).
/// nav seam의 RecordingNavSink와 동형.
/// </summary>
public sealed class RecordingNarrativeSink : INarrativePresentationSink
{
    public readonly List<StoryPresentationRequest> Presented = new();
    public int AckCount { get; private set; }

    public void Present(StoryPresentationRequest request, Action onPresented)
    {
        Presented.Add(request);
        onPresented();   // 즉시 ack → 펌프 계속, 입력 대기 없음
        AckCount++;
    }

    public StoryPresentationRequest? Last => Presented.Count == 0 ? null : Presented[^1];

    public bool PresentedKey(string presentationKey)
        => Presented.Exists(request => request.PresentationKey == presentationKey);
}
