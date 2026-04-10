using System;
using UnityEngine.UIElements;

namespace SM.Unity.Narrative;

public sealed class StoryToastBannerView : IDisposable
{
    private readonly VisualElement _panel;
    private readonly Label _titleLabel;
    private readonly Label _bodyLabel;
    private bool _allowTapDismiss;

    public StoryToastBannerView(VisualElement root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _panel = Require<VisualElement>(root, "story-toast-panel");
        _titleLabel = Require<Label>(root, "story-toast-title");
        _bodyLabel = Require<Label>(root, "story-toast-body");

        Root.pickingMode = PickingMode.Ignore;
        _panel.pickingMode = PickingMode.Position;
        _panel.RegisterCallback<PointerUpEvent>(HandlePanelPointerUp);
        Hide();
    }

    public VisualElement Root { get; }

    public event Action? DismissRequested;

    public void Render(in StoryToastBannerViewState state)
    {
        _titleLabel.text = state.TitleText;
        _bodyLabel.text = state.BodyText;
        _allowTapDismiss = state.AllowTapDismiss;
        _panel.pickingMode = _allowTapDismiss ? PickingMode.Position : PickingMode.Ignore;
    }

    public void Show()
    {
        Root.style.display = DisplayStyle.Flex;
        Root.visible = true;
        Root.AddToClassList("story-layer--visible");
    }

    public void Hide()
    {
        Root.RemoveFromClassList("story-layer--visible");
        Root.style.display = DisplayStyle.None;
        Root.visible = false;
        Root.pickingMode = PickingMode.Ignore;
    }

    public void Dispose()
    {
        _panel.UnregisterCallback<PointerUpEvent>(HandlePanelPointerUp);
    }

    private void HandlePanelPointerUp(PointerUpEvent evt)
    {
        evt.StopPropagation();
        if (_allowTapDismiss)
        {
            DismissRequested?.Invoke();
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
