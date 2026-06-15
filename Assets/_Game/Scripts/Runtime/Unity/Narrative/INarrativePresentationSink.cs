using System;
using SM.Meta;

namespace SM.Unity.Narrative;

/// <summary>
/// 연출 dispatch seam — "무엇을 보여줄지"(StoryPresentationRequest, SM.Meta 순수)와
/// "어떻게 렌더하고 입력을 기다릴지"(StoryPresentationRunner / VisualElement)를 분리한다.
/// nav seam의 INavSink와 동형: 결정·데이터는 순수, 운반만 구현이 갈린다.
/// 런타임 구현 = StorySceneFlowBridge가 StoryPresentationRunner로 연출(입력 대기),
/// 테스트 구현 = RecordingNarrativeSink가 기록 후 즉시 ack(입력 대기 0 → headless 완주).
/// </summary>
public interface INarrativePresentationSink
{
    /// <summary>한 request를 dispatch하고, 연출 종료 시 onPresented를 호출한다(headless는 동기 즉시 호출).</summary>
    void Present(StoryPresentationRequest request, Action onPresented);
}
