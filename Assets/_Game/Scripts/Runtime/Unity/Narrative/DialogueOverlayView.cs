using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace SM.Unity.Narrative;

public sealed class DialogueOverlayView : IDisposable
{
    private readonly VisualElement _leftSlot;
    private readonly Image _leftPortrait;
    private readonly Label _speakerLabel;
    private readonly Label _lineLabel;
    private readonly Label _continueHintLabel;
    private readonly VisualElement _rightSlot;
    private readonly Image _rightPortrait;
    private readonly Button _skipAllButton;
    private readonly EventCallback<PointerUpEvent> _rootPointerUpHandler;
    private readonly EventCallback<PointerUpEvent> _skipPointerUpHandler;
    private readonly Action _skipClickedHandler;

    public DialogueOverlayView(VisualElement root)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        _leftSlot = Require<VisualElement>(root, "dialogue-overlay-left-slot");
        _leftPortrait = Require<Image>(root, "dialogue-overlay-left-portrait");
        _speakerLabel = Require<Label>(root, "dialogue-overlay-speaker");
        _lineLabel = Require<Label>(root, "dialogue-overlay-line");
        _continueHintLabel = Require<Label>(root, "dialogue-overlay-continue-hint");
        _rightSlot = Require<VisualElement>(root, "dialogue-overlay-right-slot");
        _rightPortrait = Require<Image>(root, "dialogue-overlay-right-portrait");
        _skipAllButton = Require<Button>(root, "dialogue-overlay-skip-all-button");

        _leftPortrait.scaleMode = ScaleMode.ScaleAndCrop;
        _rightPortrait.scaleMode = ScaleMode.ScaleAndCrop;
        _rootPointerUpHandler = HandleRootPointerUp;
        _skipPointerUpHandler = StopButtonPropagation;
        _skipClickedHandler = HandleSkipAllClicked;

        Root.RegisterCallback<PointerUpEvent>(_rootPointerUpHandler);
        _skipAllButton.RegisterCallback<PointerUpEvent>(_skipPointerUpHandler);
        _skipAllButton.clicked += _skipClickedHandler;
        Hide();
    }

    public VisualElement Root { get; }

    public event Action? NextRequested;
    public event Action? SkipAllRequested;

    public void Render(in DialogueOverlayViewState state)
    {
        _speakerLabel.text = state.SpeakerNameText;
        _lineLabel.text = state.LineText;
        SetVisible(_speakerLabel, !state.IsNarrator);
        SetVisible(_continueHintLabel, state.ShowContinueHint);

        ApplyDialogueTextClass(_lineLabel, state.IsNarrator);
        ApplyPortrait(_leftSlot, _leftPortrait, state.ShowLeftPortrait, state.LeftPortrait, state.HighlightLeftPortrait);
        ApplyPortrait(_rightSlot, _rightPortrait, state.ShowRightPortrait, state.RightPortrait, state.HighlightRightPortrait);

        _skipAllButton.style.display = state.ShowSkipAll ? DisplayStyle.Flex : DisplayStyle.None;
        _skipAllButton.visible = state.ShowSkipAll;
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
    }

    public void Dispose()
    {
        Root.UnregisterCallback<PointerUpEvent>(_rootPointerUpHandler);
        _skipAllButton.UnregisterCallback<PointerUpEvent>(_skipPointerUpHandler);
        _skipAllButton.clicked -= _skipClickedHandler;
    }

    private void HandleRootPointerUp(PointerUpEvent evt)
    {
        NextRequested?.Invoke();
    }

    private void HandleSkipAllClicked()
    {
        SkipAllRequested?.Invoke();
    }

    private static void StopButtonPropagation(PointerUpEvent evt)
    {
        evt.StopPropagation();
    }

    private static void ApplyDialogueTextClass(VisualElement label, bool isNarrator)
    {
        ToggleClass(label, "story-text--narrator", isNarrator);
        ToggleClass(label, "story-text--character", !isNarrator);
    }

    private static void ApplyPortrait(VisualElement slot, Image image, bool show, Sprite? sprite, bool highlight)
    {
        slot.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        slot.visible = show;
        ToggleClass(slot, "story-portrait-slot--hidden", !show);
        image.sprite = sprite;
        ToggleClass(image, "story-portrait--active", show && highlight);
        ToggleClass(image, "story-portrait--inactive", show && !highlight);
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
