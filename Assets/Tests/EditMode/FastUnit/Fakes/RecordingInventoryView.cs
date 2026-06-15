using System;
using SM.Unity.UI.Town.Preview;

namespace SM.Tests.EditMode.Fakes;

/// <summary>
/// headless 테스트용 IInventoryView — 실제 VisualElement 렌더 대신 마지막 ViewState와 렌더 횟수를 기록한다.
/// presenter의 command 경로(OnEquipItem/OnCategorySelected→Refresh→Render)가 씬 없이 구동됨을 검증한다.
/// </summary>
public sealed class RecordingInventoryView : IInventoryView
{
    public InventoryViewState? LastState { get; private set; }
    public int RenderCount { get; private set; }

    public void Bind(IInventoryActions actions)
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

    public void Render(InventoryViewState state)
    {
        LastState = state;
        RenderCount++;
    }
}
