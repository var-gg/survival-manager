using System;
using UnityEngine;

namespace SM.Unity.Narrative;

public sealed class DialogueOverlayPresenter : IDisposable
{
    private readonly DialogueOverlayView _view;
    private readonly IStoryPortraitResolver _portraitResolver;
    private DialogueOverlayPlaybackModel? _model;
    private Action? _onCompleted;

    public DialogueOverlayPresenter(DialogueOverlayView view, IStoryPortraitResolver portraitResolver)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _portraitResolver = portraitResolver ?? throw new ArgumentNullException(nameof(portraitResolver));
        _view.NextRequested += AdvanceLine;
        _view.SkipAllRequested += SkipAll;
    }

    public bool IsPlaying { get; private set; }
    public int CurrentLineIndex { get; private set; } = -1;

    public void Present(DialogueOverlayPlaybackModel model, Action? onCompleted)
    {
        HideImmediate();

        if (model == null || model.Lines.Count == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        _model = model;
        _onCompleted = onCompleted;
        CurrentLineIndex = 0;
        IsPlaying = true;
        _view.Show();
        RenderCurrentLine();
    }

    public void AdvanceLine()
    {
        if (!IsPlaying || _model == null)
        {
            return;
        }

        if (CurrentLineIndex >= _model.Lines.Count - 1)
        {
            Complete();
            return;
        }

        CurrentLineIndex++;
        RenderCurrentLine();
    }

    public void SkipAll()
    {
        if (!IsPlaying)
        {
            return;
        }

        Complete();
    }

    public void HideImmediate()
    {
        _model = null;
        _onCompleted = null;
        CurrentLineIndex = -1;
        IsPlaying = false;
        _view.Hide();
    }

    public void Dispose()
    {
        _view.NextRequested -= AdvanceLine;
        _view.SkipAllRequested -= SkipAll;
        _view.Dispose();
    }

    private void Complete()
    {
        if (!IsPlaying)
        {
            return;
        }

        var callback = _onCompleted;
        HideImmediate();
        callback?.Invoke();
    }

    private void RenderCurrentLine()
    {
        if (_model == null || CurrentLineIndex < 0 || CurrentLineIndex >= _model.Lines.Count)
        {
            return;
        }

        var line = _model.Lines[CurrentLineIndex];
        var leftPortrait = ResolvePortrait(_model.LeftSpeaker, line, StorySpeakerSide.Left);
        var rightPortrait = ResolvePortrait(_model.RightSpeaker, line, StorySpeakerSide.Right);
        var activeSide = line.IsNarrator ? StorySpeakerSide.None : line.SpeakerSide;

        _view.Render(new DialogueOverlayViewState(
            line.IsNarrator ? string.Empty : line.SpeakerNameText,
            line.IsNarrator ? line.LineText : Quote(line.LineText),
            line.IsNarrator,
            leftPortrait,
            _model.LeftSpeaker.HasValue && leftPortrait != null,
            activeSide == StorySpeakerSide.Left,
            rightPortrait,
            _model.RightSpeaker.HasValue && rightPortrait != null,
            activeSide == StorySpeakerSide.Right,
            _model.ShowSkipAll,
            ShowContinueHint: true));
    }

    private Sprite? ResolvePortrait(StorySpeakerModel? speaker, StoryDialogueLineModel line, StorySpeakerSide side)
    {
        if (!speaker.HasValue)
        {
            return null;
        }

        var resolvedSpeaker = speaker.Value;
        var emoteId = line.IsNarrator
            ? resolvedSpeaker.DefaultEmoteId
            : line.SpeakerSide == side
                ? line.EmoteId
                : resolvedSpeaker.DefaultEmoteId;

        return _portraitResolver.TryResolve(resolvedSpeaker.CharacterId, emoteId, out var portrait)
            ? portrait
            : null;
    }

    private static string Quote(string lineText)
    {
        return $"\"{lineText}\"";
    }
}
