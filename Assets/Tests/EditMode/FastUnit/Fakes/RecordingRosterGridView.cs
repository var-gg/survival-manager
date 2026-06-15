using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// headless 테스트용 IRosterGridView — 실제 VisualElement 렌더 대신 마지막 ViewState와 렌더 횟수를 기록한다.
/// presenter의 command 경로(OnFilterSelected→Refresh→Render)가 씬 없이 구동됨을 검증할 수 있게 한다.
/// </summary>
public sealed class RecordingRosterGridView : IRosterGridView
{
    public RosterGridViewState? LastState { get; private set; }
    public int RenderCount { get; private set; }

    public void Bind(IRosterGridActions actions)
    {
    }

    public void Render(RosterGridViewState state)
    {
        LastState = state;
        RenderCount++;
    }
}
