using System;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// headless 테스트용 IEquipmentRefitView — 실제 VisualElement 렌더 대신 마지막 ViewState와 렌더 횟수를 기록한다.
/// presenter의 item-level command 경로(OnPoolItemSelected/OnRefitConfirmed→Refresh→Render)가 씬 없이 구동됨을 검증한다.
/// </summary>
public sealed class RecordingEquipmentRefitView : IEquipmentRefitView
{
    public EquipmentRefitViewState? LastState { get; private set; }
    public int RenderCount { get; private set; }

    public void Bind(IEquipmentRefitActions actions)
    {
    }

    public void BindClose(Action close)
    {
    }

    public void Open()
    {
    }

    public void Close()
    {
    }

    public void Render(EquipmentRefitViewState state)
    {
        LastState = state;
        RenderCount++;
    }
}
