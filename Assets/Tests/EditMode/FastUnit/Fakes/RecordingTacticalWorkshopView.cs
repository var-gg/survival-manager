using System;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// TacticalWorkshopPresenter 계약 테스트용 view fake — scene/UITK 없이
/// presenter 커맨드 경로(액션 → 세션 mutate → Render)가 도는지 기록으로 증명한다.
/// </summary>
public sealed class RecordingTacticalWorkshopView : ITacticalWorkshopView
{
    public TacticalWorkshopViewState? LastState { get; private set; }
    public int RenderCount { get; private set; }
    public int OpenCount { get; private set; }
    public int CloseCount { get; private set; }
    public ITacticalWorkshopActions? BoundActions { get; private set; }
    public Action? BoundClose { get; private set; }

    public void Bind(ITacticalWorkshopActions actions) => BoundActions = actions;

    public void BindClose(Action close) => BoundClose = close;

    public void Open() => OpenCount++;

    public void Close() => CloseCount++;

    public void Render(TacticalWorkshopViewState state)
    {
        LastState = state;
        RenderCount++;
    }
}
