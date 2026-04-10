using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.Narrative;

public sealed class DialogueSceneView : IDisposable
{
    private readonly VisualElement _leftSlot;
    private readonly Image _leftPortrait;
    private readonly Label _nameplateLabel;
    private readonly Label _lineLabel;
    private readonly Label _continueHintLabel;
    private readonly Button _skipAllButton;
    private readonly VisualElement _rightSlot;
    private readonly Image _rightPortrait;
    private readonly VisualElement _skipConfirmOverlay;
    private readonly Label _skipConfirmTitleLabel;
    private readonly Label _skipConfirmBodyLabel;
    private readonly Button _skipConfirmAcceptButton;
    private readonly Button _skipConfirmCancelButton;
    private readonly EventCallback<PointerUpEvent> _rootPointerUpHandler;
    private readonly EventCallback<PointerUpEvent> _skipPointerUpHandler;
    private readonly EventCallback<PointerUpEvent> _confirmAcceptPointerUpHandler;
    private readonly EventCallback<PointerUpEvent> _confirmCancelPointerUpHandler;
    private readonly Action _skipClickedHandler;
    private readonly Action _confirmAcceptClickedHandler;
    private readonly Action _confirmCancelClickedHandler;

    public DialogueSceneView(VisualElement root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _leftSlot = Require<VisualElement>(root, "dialogue-scene-left-slot");
        _leftPortrait = Require<Image>(root, "dialogue-scene-left-portrait");
        _nameplateLabel = Require<Label>(root, "dialogue-scene-nameplate");
        _lineLabel = Require<Label>(root, "dialogue-scene-line");
        _continueHintLabel = Require<Label>(root, "dialogue-scene-continue-hint");
        _skipAllButton = Require<Button>(root, "dialogue-scene-skip-all-button");
        _rightSlot = Require<VisualElement>(root, "dialogue-scene-right-slot");
        _rightPortrait = Require<Image>(root, "dialogue-scene-right-portrait");
        _skipConfirmOverlay = Require<VisualElement>(root, "dialogue-scene-skip-confirm-overlay");
        _skipConfirmTitleLabel = Require<Label>(root, "dialogue-scene-skip-confirm-title");
        _skipConfirmBodyLabel = Require<Label>(root, "dialogue-scene-skip-confirm-body");
        _skipConfirmAcceptButton = Require<Button>(root, "dialogue-scene-skip-confirm-accept-button");
        _skipConfirmCancelButton = Require<Button>(root, "dialogue-scene-skip-confirm-cancel-button");

        _leftPortrait.scaleMode = ScaleMode.ScaleAndCrop;
        _rightPortrait.scaleMode = ScaleMode.ScaleAndCrop;

        _rootPointerUpHandler = HandleRootPointerUp;
        _skipPointerUpHandler = StopPointerPropagation;
        _confirmAcceptPointerUpHandler = StopPointerPropagation;
        _confirmCancelPointerUpHandler = StopPointerPropagation;
        _skipClickedHandler = HandleSkipRequested;
        _confirmAcceptClickedHandler = HandleSkipConfirmed;
        _confirmCancelClickedHandler = HandleSkipCancelled;

        Root.RegisterCallback<PointerUpEvent>(_rootPointerUpHandler);
        _skipAllButton.RegisterCallback<PointerUpEvent>(_skipPointerUpHandler);
        _skipConfirmAcceptButton.RegisterCallback<PointerUpEvent>(_confirmAcceptPointerUpHandler);
        _skipConfirmCancelButton.RegisterCallback<PointerUpEvent>(_confirmCancelPointerUpHandler);
        _skipAllButton.clicked += _skipClickedHandler;
        _skipConfirmAcceptButton.clicked += _confirmAcceptClickedHandler;
        _skipConfirmCancelButton.clicked += _confirmCancelClickedHandler;
        Hide();
    }

    public VisualElement Root { get; }

    public event Action? NextRequested;
    public event Action? SkipAllRequested;
    public event Action? SkipConfirmed;
    public event Action? SkipCancelled;

    public void Render(in DialogueSceneViewState state)
    {
        _nameplateLabel.text = state.SpeakerNameText;
        _lineLabel.text = state.LineText;
        _skipConfirmTitleLabel.text = state.SkipConfirmTitleText;
        _skipConfirmBodyLabel.text = state.SkipConfirmBodyText;

        SetVisible(_nameplateLabel, !state.IsNarrator);
        SetVisible(_continueHintLabel, state.ShowContinueHint);
        ApplyDialogueTextClass(_lineLabel, state.IsNarrator);

        ApplyPortrait(
            _leftSlot,
            _leftPortrait,
            state.ShowLeftPortrait,
            state.LeftPortrait,
            state.ActiveSpeakerSide == StorySpeakerSide.Left && !state.IsNarrator);
        ApplyPortrait(
            _rightSlot,
            _rightPortrait,
            state.ShowRightPortrait,
            state.RightPortrait,
            state.ActiveSpeakerSide == StorySpeakerSide.Right && !state.IsNarrator);

        _skipAllButton.style.display = state.ShowSkipAll ? DisplayStyle.Flex : DisplayStyle.None;
        _skipAllButton.visible = state.ShowSkipAll;

        _skipConfirmOverlay.style.display = state.ShowSkipConfirmation ? DisplayStyle.Flex : DisplayStyle.None;
        _skipConfirmOverlay.visible = state.ShowSkipConfirmation;
        _skipConfirmOverlay.pickingMode = state.ShowSkipConfirmation ? PickingMode.Position : PickingMode.Ignore;
    }

    public void SetDisplayedText(string visibleText)
    {
        _lineLabel.text = visibleText ?? string.Empty;
    }

    public void Show()
    {
        Root.style.display = DisplayStyle.Flex;
        Root.visible = true;
        Root.pickingMode = PickingMode.Position;
        Root.AddToClassList("story-layer--visible");
    }

    public void Hide()
    {
        Root.RemoveFromClassList("story-layer--visible");
        Root.style.display = DisplayStyle.None;
        Root.visible = false;
        Root.pickingMode = PickingMode.Ignore;
        _skipConfirmOverlay.pickingMode = PickingMode.Ignore;
    }

    public void Dispose()
    {
        Root.UnregisterCallback<PointerUpEvent>(_rootPointerUpHandler);
        _skipAllButton.UnregisterCallback<PointerUpEvent>(_skipPointerUpHandler);
        _skipConfirmAcceptButton.UnregisterCallback<PointerUpEvent>(_confirmAcceptPointerUpHandler);
        _skipConfirmCancelButton.UnregisterCallback<PointerUpEvent>(_confirmCancelPointerUpHandler);
        _skipAllButton.clicked -= _skipClickedHandler;
        _skipConfirmAcceptButton.clicked -= _confirmAcceptClickedHandler;
        _skipConfirmCancelButton.clicked -= _confirmCancelClickedHandler;
    }

    private void HandleRootPointerUp(PointerUpEvent evt)
    {
        if (_skipConfirmOverlay.visible && _skipConfirmOverlay.style.display != DisplayStyle.None)
        {
            evt.StopPropagation();
            return;
        }

        NextRequested?.Invoke();
    }

    private void HandleSkipRequested()
    {
        SkipAllRequested?.Invoke();
    }

    private void HandleSkipConfirmed()
    {
        SkipConfirmed?.Invoke();
    }

    private void HandleSkipCancelled()
    {
        SkipCancelled?.Invoke();
    }

    private static void StopPointerPropagation(PointerUpEvent evt)
    {
        evt.StopPropagation();
    }

    private static void ApplyDialogueTextClass(VisualElement label, bool isNarrator)
    {
        ToggleClass(label, "story-text--narrator", isNarrator);
        ToggleClass(label, "story-text--character", !isNarrator);
    }

    private static void ApplyPortrait(VisualElement slot, Image image, bool show, Sprite? sprite, bool active)
    {
        slot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        slot.visible = show;
        ToggleClass(slot, "story-portrait-slot--hidden", !show);
        image.sprite = sprite;
        ToggleClass(image, "story-portrait--active", show && active);
        ToggleClass(image, "story-portrait--inactive", show && !active);
    }

    private static void SetVisible(VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        element.visible = visible;
        ToggleClass(element, "story-speaker--hidden", !visible);
    }

    private static void ToggleClass(VisualElement element, string className, bool enabled)
    {
        if (enabled)
        {
            element.AddToClassList(className);
        }
        else
        {
            element.RemoveFromClassList(className);
        }
    }

    private static T Require<T>(VisualElement root, string name) where T : VisualElement
    {
        return root.Q<T>(name) ?? throw new InvalidOperationException($"Missing UITK element '{name}'.");
    }
}
