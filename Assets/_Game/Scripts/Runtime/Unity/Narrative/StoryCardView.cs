using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.Narrative;

public sealed class StoryCardView : IDisposable
{
    private readonly Image _backgroundImage;
    private readonly VisualElement _overlay;
    private readonly Label _titleLabel;
    private readonly ScrollView _bodyScroll;
    private readonly Label _bodyLabel;
    private readonly Button _skipButton;
    private readonly Label _continueHintLabel;
    private readonly EventCallback<PointerUpEvent> _rootPointerUpHandler;
    private readonly EventCallback<PointerUpEvent> _skipPointerUpHandler;
    private readonly Action _skipClickedHandler;

    public StoryCardView(VisualElement root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _backgroundImage = Require<Image>(root, "story-card-background");
        _overlay = Require<VisualElement>(root, "story-card-overlay");
        _titleLabel = Require<Label>(root, "story-card-title");
        _bodyScroll = Require<ScrollView>(root, "story-card-body-scroll");
        _bodyLabel = Require<Label>(root, "story-card-body");
        _skipButton = Require<Button>(root, "story-card-skip-button");
        _continueHintLabel = Require<Label>(root, "story-card-continue-hint");

        _backgroundImage.scaleMode = ScaleMode.ScaleAndCrop;
        _rootPointerUpHandler = HandleRootPointerUp;
        _skipPointerUpHandler = StopPointerPropagation;
        _skipClickedHandler = HandleSkipClicked;

        Root.RegisterCallback<PointerUpEvent>(_rootPointerUpHandler);
        _skipButton.RegisterCallback<PointerUpEvent>(_skipPointerUpHandler);
        _skipButton.clicked += _skipClickedHandler;
        Hide();
    }

    public VisualElement Root { get; }

    public event Action? ContinueRequested;
    public event Action? SkipRequested;

    public void Render(in StoryCardViewState state)
    {
        _titleLabel.text = state.TitleText;
        _bodyLabel.text = state.BodyText;
        _backgroundImage.sprite = state.BackgroundSprite;
        _backgroundImage.style.display = state.BackgroundSprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        _backgroundImage.visible = state.BackgroundSprite != null;
        _overlay.style.backgroundColor = new StyleColor(state.BackgroundTint);
        _skipButton.style.display = state.ShowSkip ? DisplayStyle.Flex : DisplayStyle.None;
        _skipButton.visible = state.ShowSkip;
        _continueHintLabel.style.display = state.ShowContinueHint ? DisplayStyle.Flex : DisplayStyle.None;
        _continueHintLabel.visible = state.ShowContinueHint;
        _bodyScroll.scrollOffset = Vector2.zero;
    }

    public void Show()
    {
        Root.style.display = DisplayStyle.Flex;
        Root.visible = true;
        Root.pickingMode = PickingMode.Position;
        Root.AddToClassList("story-layer--visible");
        _bodyScroll.scrollOffset = Vector2.zero;
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
        Root.UnregisterCallback<PointerUpEvent>(_rootPointerUpHandler);
        _skipButton.UnregisterCallback<PointerUpEvent>(_skipPointerUpHandler);
        _skipButton.clicked -= _skipClickedHandler;
    }

    private void HandleRootPointerUp(PointerUpEvent evt)
    {
        ContinueRequested?.Invoke();
    }

    private void HandleSkipClicked()
    {
        SkipRequested?.Invoke();
    }

    private static void StopPointerPropagation(PointerUpEvent evt)
    {
        evt.StopPropagation();
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
